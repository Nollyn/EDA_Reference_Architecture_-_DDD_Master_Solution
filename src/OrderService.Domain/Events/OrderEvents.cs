using OrderService.Domain.Common;
using OrderService.Domain.ValueObjects;

namespace OrderService.Domain.Events;

public sealed record OrderCreatedDomainEvent(
    OrderId OrderId,
    CustomerId CustomerId,
    Address ShippingAddress,
    DateTimeOffset OccurredOn,
    Guid EventId) : IDomainEvent
{
    public OrderCreatedDomainEvent(OrderId orderId, CustomerId customerId, Address shippingAddress)
        : this(orderId, customerId, shippingAddress, DateTimeOffset.UtcNow, Guid.NewGuid())
    {
    }
}

public sealed record OrderItemAddedDomainEvent(
    OrderId OrderId,
    OrderItemId OrderItemId,
    ProductId ProductId,
    string ProductName,
    Money UnitPrice,
    int Quantity,
    DateTimeOffset OccurredOn,
    Guid EventId) : IDomainEvent
{
    public OrderItemAddedDomainEvent(OrderId orderId, OrderItemId orderItemId, ProductId productId, string productName, Money unitPrice, int quantity)
        : this(orderId, orderItemId, productId, productName, unitPrice, quantity, DateTimeOffset.UtcNow, Guid.NewGuid())
    {
    }
}

public sealed record OrderSubmittedDomainEvent(
    OrderId OrderId,
    CustomerId CustomerId,
    Money TotalAmount,
    DateTimeOffset OccurredOn,
    Guid EventId) : IDomainEvent
{
    public OrderSubmittedDomainEvent(OrderId orderId, CustomerId customerId, Money totalAmount)
        : this(orderId, customerId, totalAmount, DateTimeOffset.UtcNow, Guid.NewGuid())
    {
    }
}

public sealed record OrderConfirmedDomainEvent(
    OrderId OrderId,
    CustomerId CustomerId,
    Money TotalAmount,
    DateTimeOffset OccurredOn,
    Guid EventId) : IDomainEvent
{
    public OrderConfirmedDomainEvent(OrderId orderId, CustomerId customerId, Money totalAmount)
        : this(orderId, customerId, totalAmount, DateTimeOffset.UtcNow, Guid.NewGuid())
    {
    }
}

public sealed record OrderPaidDomainEvent(
    OrderId OrderId,
    CustomerId CustomerId,
    DateTimeOffset OccurredOn,
    Guid EventId) : IDomainEvent
{
    public OrderPaidDomainEvent(OrderId orderId, CustomerId customerId)
        : this(orderId, customerId, DateTimeOffset.UtcNow, Guid.NewGuid())
    {
    }
}

public sealed record OrderShippedDomainEvent(
    OrderId OrderId,
    CustomerId CustomerId,
    DateTimeOffset OccurredOn,
    Guid EventId) : IDomainEvent
{
    public OrderShippedDomainEvent(OrderId orderId, CustomerId customerId)
        : this(orderId, customerId, DateTimeOffset.UtcNow, Guid.NewGuid())
    {
    }
}

public sealed record OrderCancelledDomainEvent(
    OrderId OrderId,
    CustomerId CustomerId,
    string Reason,
    DateTimeOffset OccurredOn,
    Guid EventId) : IDomainEvent
{
    public OrderCancelledDomainEvent(OrderId orderId, CustomerId customerId, string reason)
        : this(orderId, customerId, reason, DateTimeOffset.UtcNow, Guid.NewGuid())
    {
    }
}
