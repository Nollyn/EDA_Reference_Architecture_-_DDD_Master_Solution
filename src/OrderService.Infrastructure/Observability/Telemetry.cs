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

public static class TraceContextPropagator
{
    public static void InjectTraceContext(IDictionary<string, object?> headers, Activity? activity = null)
    {
        activity ??= Activity.Current;
        if (activity is null)
            return;

        headers["traceparent"] = activity.Id;
        if (!string.IsNullOrWhiteSpace(activity.TraceStateString))
        {
            headers["tracestate"] = activity.TraceStateString;
        }
    }

    public static ActivityContext ExtractTraceContext(IDictionary<string, object?> headers)
    {
        string? traceParent = null;
        string? traceState = null;

        if (headers.TryGetValue("traceparent", out var tpObj))
        {
            traceParent = tpObj switch
            {
                byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
                string s => s,
                _ => tpObj?.ToString()
            };
        }

        if (headers.TryGetValue("tracestate", out var tsObj))
        {
            traceState = tsObj switch
            {
                byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
                string s => s,
                _ => tsObj?.ToString()
            };
        }

        if (string.IsNullOrWhiteSpace(traceParent))
            return default;

        return ActivityContext.TryParse(traceParent, traceState, out var context) ? context : default;
    }
}
