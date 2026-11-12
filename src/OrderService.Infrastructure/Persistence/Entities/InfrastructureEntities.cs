namespace OrderService.Infrastructure.Persistence.Entities;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset OccurredOn { get; set; }
    public DateTimeOffset? ProcessedOn { get; set; }
    public string? Error { get; set; }
    public int RetryCount { get; set; }
    public string? TraceParent { get; set; }
    public string? TraceState { get; set; }
}

public sealed class ProcessedMessage
{
    public Guid MessageId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; }
}
