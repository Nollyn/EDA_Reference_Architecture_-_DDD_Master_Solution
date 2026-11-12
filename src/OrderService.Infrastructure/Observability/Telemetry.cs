using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OrderService.Infrastructure.Observability;

public static class Telemetry
{
    public const string ServiceName = "OrderService";
    public const string ServiceVersion = "1.0.0";

    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion);
    public static readonly Meter Meter = new(ServiceName, ServiceVersion);

    // Metrics
    public static readonly Counter<long> OrdersCreatedCounter = Meter.CreateCounter<long>(
        "orders.created.count",
        description: "Total number of orders created");

    public static readonly Counter<long> DeadLetterQueueMessagesCounter = Meter.CreateCounter<long>(
        "rabbitmq.dlq.messages",
        description: "Total number of messages routed to the RabbitMQ Dead Letter Queue");

    public static readonly UpDownCounter<long> OutboxPendingMessagesCounter = Meter.CreateUpDownCounter<long>(
        "outbox.pending.messages",
        description: "Current estimate of pending outbox messages waiting to be published");
}
