using OrderService.Application.Ports;

namespace OrderService.Application.Events;

public abstract record IntegrationEventBase(Guid Id, DateTimeOffset OccurredOn, string EventType) : IIntegrationEvent
{
    protected IntegrationEventBase(string eventType) : this(Guid.NewGuid(), DateTimeOffset.UtcNow, eventType)
    {
    }
}

public sealed record OrderCreatedIntegrationEvent(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount,
    string Currency,
    IReadOnlyList<OrderCreatedItemPayload> Items,
    Guid Id,
    DateTimeOffset OccurredOn,
    string EventType) : IntegrationEventBase(Id, OccurredOn, EventType)
{
    public const string Type = "OrderService.OrderCreated";

    public OrderCreatedIntegrationEvent(Guid orderId, Guid customerId, decimal totalAmount, string currency, IReadOnlyList<OrderCreatedItemPayload> items)
        : this(orderId, customerId, totalAmount, currency, items, Guid.NewGuid(), DateTimeOffset.UtcNow, Type)
    {
    }
}

public sealed record OrderCreatedItemPayload(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice);

public sealed record OrderConfirmedIntegrationEvent(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount,
    string Currency,
    Guid Id,
    DateTimeOffset OccurredOn,
    string EventType) : IntegrationEventBase(Id, OccurredOn, EventType)
{
    public const string Type = "OrderService.OrderConfirmed";

    public OrderConfirmedIntegrationEvent(Guid orderId, Guid customerId, decimal totalAmount, string currency)
        : this(orderId, customerId, totalAmount, currency, Guid.NewGuid(), DateTimeOffset.UtcNow, Type)
    {
    }
}

public sealed record OrderCancelledIntegrationEvent(
    Guid OrderId,
    Guid CustomerId,
    string Reason,
    Guid Id,
    DateTimeOffset OccurredOn,
    string EventType) : IntegrationEventBase(Id, OccurredOn, EventType)
{
    public const string Type = "OrderService.OrderCancelled";

    public OrderCancelledIntegrationEvent(Guid orderId, Guid customerId, string reason)
        : this(orderId, customerId, reason, Guid.NewGuid(), DateTimeOffset.UtcNow, Type)
    {
    }
}

public sealed record InventoryReservedIntegrationEvent(
    Guid OrderId,
    Guid ReservationId,
    Guid Id,
    DateTimeOffset OccurredOn,
    string EventType) : IntegrationEventBase(Id, OccurredOn, EventType)
{
    public const string Type = "InventoryService.InventoryReserved";

    public InventoryReservedIntegrationEvent(Guid orderId, Guid reservationId)
        : this(orderId, reservationId, Guid.NewGuid(), DateTimeOffset.UtcNow, Type)
    {
    }
}

public sealed record InventoryReservationFailedIntegrationEvent(
    Guid OrderId,
    string Reason,
    Guid Id,
    DateTimeOffset OccurredOn,
    string EventType) : IntegrationEventBase(Id, OccurredOn, EventType)
{
    public const string Type = "InventoryService.InventoryReservationFailed";

    public InventoryReservationFailedIntegrationEvent(Guid orderId, string reason)
        : this(orderId, reason, Guid.NewGuid(), DateTimeOffset.UtcNow, Type)
    {
    }
}
