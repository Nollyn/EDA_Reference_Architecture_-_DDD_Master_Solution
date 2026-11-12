using System.Diagnostics;
using System.Text.Json;
using OrderService.Application.Events;
using OrderService.Application.Ports;
using OrderService.Infrastructure.Observability;
using OrderService.Infrastructure.Persistence;
using OrderService.Infrastructure.Persistence.Entities;

namespace OrderService.Infrastructure.Messaging.Outbox;

public sealed class OutboxIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly OrderDbContext _dbContext;

    public OutboxIntegrationEventPublisher(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : class, IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        var currentActivity = Activity.Current;
        var traceParent = currentActivity?.Id;
        var traceState = currentActivity?.TraceStateString;

        var content = JsonSerializer.Serialize(@event, @event.GetType());

        var message = new OutboxMessage
        {
            Id = @event.Id,
            Type = @event.EventType,
            Content = content,
            OccurredOn = @event.OccurredOn,
            ProcessedOn = null,
            Error = null,
            RetryCount = 0,
            TraceParent = traceParent,
            TraceState = traceState
        };

        await _dbContext.OutboxMessages.AddAsync(message, cancellationToken);
        Telemetry.OutboxPendingMessagesCounter.Add(1);

        if (@event is OrderCreatedIntegrationEvent)
        {
            Telemetry.OrdersCreatedCounter.Add(1);
        }
    }
}
