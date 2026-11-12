using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderService.Infrastructure.Messaging.RabbitMq;
using OrderService.Infrastructure.Observability;
using OrderService.Infrastructure.Persistence;
using OrderService.Infrastructure.Persistence.Entities;
using RabbitMQ.Client;

namespace OrderService.Infrastructure.Messaging.Outbox;

public interface IOutboxProcessor
{
    Task<int> ProcessOutboxMessagesAsync(int batchSize = 20, CancellationToken cancellationToken = default);
}

public sealed class OutboxProcessor : IOutboxProcessor
{
    private readonly OrderDbContext _dbContext;
    private readonly IRabbitMqConnectionManager _connectionManager;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        OrderDbContext dbContext,
        IRabbitMqConnectionManager connectionManager,
        IOptions<RabbitMqOptions> options,
        ILogger<OutboxProcessor> logger)
    {
        _dbContext = dbContext;
        _connectionManager = connectionManager;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> ProcessOutboxMessagesAsync(int batchSize = 20, CancellationToken cancellationToken = default)
    {
        // Execute in transaction with FOR UPDATE SKIP LOCKED for multi-worker concurrency
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var pendingMessages = await _dbContext.OutboxMessages
            .FromSqlRaw("""
                SELECT * FROM outbox_messages
                WHERE processed_on IS NULL
                ORDER BY occurred_on ASC
                LIMIT {0}
                FOR UPDATE SKIP LOCKED
                """, batchSize)
            .ToListAsync(cancellationToken);

        if (pendingMessages.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return 0;
        }

        _logger.LogInformation("Processing {Count} outbox messages...", pendingMessages.Count);

        using var channel = await _connectionManager.CreateChannelAsync(enablePublisherConfirms: true, cancellationToken);

        foreach (var message in pendingMessages)
        {
            var activityContext = default(ActivityContext);
            if (!string.IsNullOrWhiteSpace(message.TraceParent))
            {
                ActivityContext.TryParse(message.TraceParent, message.TraceState, out activityContext);
            }

            using var activity = Telemetry.ActivitySource.StartActivity(
                $"OutboxPublish {message.Type}",
                ActivityKind.Producer,
                activityContext);

            try
            {
                var body = Encoding.UTF8.GetBytes(message.Content);
                var routingKey = message.Type;

                var headers = new Dictionary<string, object?>
                {
                    ["messageId"] = message.Id.ToString(),
                    ["eventType"] = message.Type,
                    ["occurredOn"] = message.OccurredOn.ToString("O")
                };

                if (activity is not null)
                {
                    TraceContextPropagator.InjectTraceContext(headers, activity);
                }
                else if (!string.IsNullOrWhiteSpace(message.TraceParent))
                {
                    headers["traceparent"] = message.TraceParent;
                    if (!string.IsNullOrWhiteSpace(message.TraceState))
                        headers["tracestate"] = message.TraceState;
                }

                var properties = new BasicProperties
                {
                    MessageId = message.Id.ToString(),
                    Type = message.Type,
                    ContentType = "application/json",
                    DeliveryMode = DeliveryModes.Persistent,
                    Timestamp = new AmqpTimestamp(message.OccurredOn.ToUnixTimeSeconds()),
                    Headers = headers
                };

                await channel.BasicPublishAsync(
                    exchange: _options.MainExchange,
                    routingKey: routingKey,
                    mandatory: false,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: cancellationToken);

                message.ProcessedOn = DateTimeOffset.UtcNow;
                message.Error = null;

                Telemetry.OutboxPendingMessagesCounter.Add(-1);
                _logger.LogDebug("Published outbox message {MessageId} ({Type})", message.Id, message.Type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish outbox message {MessageId} ({Type})", message.Id, message.Type);
                message.Error = ex.ToString();
                message.RetryCount += 1;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return pendingMessages.Count;
    }
}
