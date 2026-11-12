using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BuildingBlocks.Observability;
using OrderService.Application.Commands;
using OrderService.Application.Common;
using OrderService.Application.Events;
using OrderService.Infrastructure.Messaging.RabbitMq;
using OrderService.Infrastructure.Observability;
using OrderService.Infrastructure.Persistence;
using OrderService.Infrastructure.Persistence.Entities;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrderService.Infrastructure.Messaging.Inbox;

public interface IRabbitMqInboxConsumer
{
    Task StartConsumingAsync(CancellationToken cancellationToken = default);
}

public sealed class RabbitMqInboxConsumer : IRabbitMqInboxConsumer
{
    private readonly IRabbitMqConnectionManager _connectionManager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqInboxConsumer> _logger;
    private IChannel? _channel;

    public RabbitMqInboxConsumer(
        IRabbitMqConnectionManager connectionManager,
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqInboxConsumer> logger)
    {
        _connectionManager = connectionManager;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartConsumingAsync(CancellationToken cancellationToken = default)
    {
        _channel = await _connectionManager.CreateChannelAsync(enablePublisherConfirms: false, cancellationToken);

        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 20, global: false, cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnMessageReceivedAsync;

        _logger.LogInformation("Starting consumer for queue '{Queue}'...", _options.InventoryEventsQueue);
        await _channel.BasicConsumeAsync(
            queue: _options.InventoryEventsQueue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        var headers = ea.BasicProperties.Headers ?? new Dictionary<string, object?>();
        var parentContext = TraceContextPropagator.ExtractTraceContext(headers);

        var eventType = ea.RoutingKey;
        if (headers.TryGetValue("eventType", out var eventTypeObj) && eventTypeObj is not null)
        {
            eventType = eventTypeObj switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                string s => s,
                _ => eventTypeObj.ToString() ?? ea.RoutingKey
            };
        }

        using var activity = Telemetry.ActivitySource.StartActivity(
            $"InboxConsume {eventType}",
            ActivityKind.Consumer,
            parentContext);

        var messageIdStr = ea.BasicProperties.MessageId;
        if (string.IsNullOrWhiteSpace(messageIdStr) && headers.TryGetValue("messageId", out var msgIdObj) && msgIdObj is not null)
        {
            messageIdStr = msgIdObj switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                string s => s,
                _ => msgIdObj.ToString()
            };
        }

        if (!Guid.TryParse(messageIdStr, out var messageId))
        {
            messageId = Guid.NewGuid();
        }

        var retryCount = 0;
        if (headers.TryGetValue("x-retry-count", out var retryObj) && retryObj is not null)
        {
            retryCount = retryObj switch
            {
                int i => i,
                long l => (int)l,
                byte[] b => int.TryParse(Encoding.UTF8.GetString(b), out var parsed) ? parsed : 0,
                string s => int.TryParse(s, out var parsed) ? parsed : 0,
                _ => Convert.ToInt32(retryObj)
            };
        }

        var json = Encoding.UTF8.GetString(ea.Body.ToArray());
        _logger.LogInformation("Inbox received message {MessageId} ({EventType}), attempt {Attempt}", messageId, eventType, retryCount + 1);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync();

            // Check Inbox for Idempotency
            var isAlreadyProcessed = await dbContext.ProcessedMessages.AnyAsync(m => m.MessageId == messageId);
            if (isAlreadyProcessed)
            {
                _logger.LogInformation("Message {MessageId} was already processed. Acknowledging idempotently.", messageId);
                await transaction.CommitAsync();
                await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
                return;
            }

            // Dispatch event to appropriate application command handler
            var handled = await DispatchEventAsync(eventType, json, mediator);

            if (handled)
            {
                dbContext.ProcessedMessages.Add(new ProcessedMessage
                {
                    MessageId = messageId,
                    EventType = eventType,
                    ProcessedAt = DateTimeOffset.UtcNow
                });

                await dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully processed and recorded message {MessageId} in inbox.", messageId);
                await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            else
            {
                _logger.LogWarning("Message {MessageId} with type {EventType} was not handled by any registered saga command.", messageId, eventType);
                await transaction.CommitAsync();
                await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing inbox message {MessageId} ({EventType}). Current retry count: {RetryCount}", messageId, eventType, retryCount);

            if (retryCount + 1 < _options.MaxDeliveryAttempts)
            {
                await RouteToRetryExchangeAsync(ea, headers, retryCount + 1);
            }
            else
            {
                await RouteToDeadLetterQueueAsync(ea, headers, ex.Message);
            }

            await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
        }
    }

    private static async Task<bool> DispatchEventAsync(string eventType, string json, IMediator mediator)
    {
        if (eventType == InventoryReservationFailedIntegrationEvent.Type || eventType.EndsWith("InventoryReservationFailed", StringComparison.OrdinalIgnoreCase))
        {
            var @event = JsonSerializer.Deserialize<InventoryReservationFailedIntegrationEvent>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (@event is not null)
            {
                await mediator.SendAsync(new ProcessInventoryReservationFailedCommand(@event.OrderId, @event.Reason));
                return true;
            }
        }
        else if (eventType == InventoryReservedIntegrationEvent.Type || eventType.EndsWith("InventoryReserved", StringComparison.OrdinalIgnoreCase))
        {
            var @event = JsonSerializer.Deserialize<InventoryReservedIntegrationEvent>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (@event is not null)
            {
                await mediator.SendAsync(new ProcessInventoryReservedCommand(@event.OrderId, @event.ReservationId));
                return true;
            }
        }

        return false;
    }

    private async Task RouteToRetryExchangeAsync(BasicDeliverEventArgs ea, IDictionary<string, object?> headers, int nextRetryCount)
    {
        _logger.LogWarning("Routing message {MessageId} to retry exchange '{RetryExchange}' with attempt count {Attempt}...", ea.BasicProperties.MessageId, _options.RetryExchange, nextRetryCount);

        headers["x-retry-count"] = nextRetryCount;

        var properties = new BasicProperties
        {
            MessageId = ea.BasicProperties.MessageId,
            Type = ea.BasicProperties.Type,
            ContentType = ea.BasicProperties.ContentType ?? "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Headers = headers
        };

        await _channel!.BasicPublishAsync(
            exchange: _options.RetryExchange,
            routingKey: _options.RetryQueue,
            mandatory: false,
            basicProperties: properties,
            body: ea.Body);
    }

    private async Task RouteToDeadLetterQueueAsync(BasicDeliverEventArgs ea, IDictionary<string, object?> headers, string exceptionMessage)
    {
        _logger.LogError("Message {MessageId} exceeded maximum delivery attempts ({MaxAttempts}). Routing to DLQ '{DLQ}'...", ea.BasicProperties.MessageId, _options.MaxDeliveryAttempts, _options.DeadLetterQueue);

        headers["x-death-reason"] = exceptionMessage;
        headers["x-death-timestamp"] = DateTimeOffset.UtcNow.ToString("O");

        var properties = new BasicProperties
        {
            MessageId = ea.BasicProperties.MessageId,
            Type = ea.BasicProperties.Type,
            ContentType = ea.BasicProperties.ContentType ?? "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Headers = headers
        };

        await _channel!.BasicPublishAsync(
            exchange: _options.DeadLetterExchange,
            routingKey: _options.DeadLetterQueue,
            mandatory: false,
            basicProperties: properties,
            body: ea.Body);

        Telemetry.DeadLetterQueueMessagesCounter.Add(1);
    }
}
