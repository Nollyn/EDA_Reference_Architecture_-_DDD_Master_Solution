using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BuildingBlocks.Observability;
using OrderService.Infrastructure.Messaging.RabbitMq;
using OrderService.Infrastructure.Observability;
using OrderService.Infrastructure.Persistence;
using RabbitMQ.Client;

namespace OrderService.Infrastructure.Messaging.Outbox;

public interface IOutboxProcessor
{
    Task<int> ProcessOutboxMessagesAsync(int batchSize = 20, CancellationToken cancellationToken = default);
}

public sealed class OutboxProcessor(
    OrderDbContext dbContext,
    IRabbitMqConnectionManager connectionManager,
    IOptions<RabbitMqOptions> options,
    ILogger<OutboxProcessor> logger)
    : IOutboxProcessor
{
    private readonly RabbitMqOptions _options = options.Value;

    public async Task<int> ProcessOutboxMessagesAsync(int batchSize = 20, CancellationToken cancellationToken = default)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var pendingMessages = await dbContext.OutboxMessages
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

            logger.LogInformation("Processing {Count} outbox messages...", pendingMessages.Count);

            await using var channel = await connectionManager.CreateChannelAsync(enablePublisherConfirms: true, cancellationToken);

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
                    logger.LogDebug("Published outbox message {MessageId} ({Type})", message.Id, message.Type);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to publish outbox message {MessageId} ({Type})", message.Id, message.Type);
                    message.Error = ex.ToString();
                    message.RetryCount += 1;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return pendingMessages.Count;
        });
    }
}