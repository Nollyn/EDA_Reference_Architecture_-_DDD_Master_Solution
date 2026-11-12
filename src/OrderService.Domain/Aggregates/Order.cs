using OrderService.Domain.Common;
using OrderService.Domain.Entities;
using OrderService.Domain.Events;
using OrderService.Domain.Exceptions;
using OrderService.Domain.ValueObjects;

namespace OrderService.Domain.Aggregates;

public sealed class Order : AggregateRoot<OrderId>
{
    private readonly List<OrderItem> _items = [];

    public CustomerId CustomerId { get; private set; }
    public Address ShippingAddress { get; private set; }
    public OrderStatus Status { get; private set; }
    public Money TotalAmount { get; private set; }
    public string Currency { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    // Private constructor for EF Core
    private Order()
    {
        ShippingAddress = null!;
        TotalAmount = Money.Zero();
        Currency = "USD";
    }

    private Order(OrderId id, CustomerId customerId, Address shippingAddress, string currency)
    {
        ArgumentNullException.ThrowIfNull(shippingAddress);

        Id = id;
        CustomerId = customerId;
        ShippingAddress = shippingAddress;
        Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();
        Status = OrderStatus.Draft;
        TotalAmount = Money.Zero(Currency);
        CreatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new OrderCreatedDomainEvent(Id, CustomerId, ShippingAddress));
    }

    public static Order Create(CustomerId customerId, Address shippingAddress, string currency = "USD")
    {
        return new Order(OrderId.New(), customerId, shippingAddress, currency);
    }

    public static Order Create(OrderId id, CustomerId customerId, Address shippingAddress, string currency = "USD")
    {
        return new Order(id, customerId, shippingAddress, currency);
    }

    public OrderItem AddItem(ProductId productId, string productName, Money unitPrice, int quantity)
    {
        EnsureOrderCanBeModified();
        ArgumentNullException.ThrowIfNull(unitPrice);

        if (unitPrice.Currency != Currency)
            throw new OrderDomainException($"Item currency '{unitPrice.Currency}' does not match order currency '{Currency}'.");

        var existingItem = _items.FirstOrDefault(i => i.ProductId == productId);
        OrderItem item;

        if (existingItem is not null)
        {
            existingItem.AddQuantity(quantity);
            item = existingItem;
        }
        else
        {
            item = new OrderItem(OrderItemId.New(), productId, productName, unitPrice, quantity);
            _items.Add(item);
        }

        RecalculateTotal();
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new OrderItemAddedDomainEvent(Id, item.Id, productId, productName, unitPrice, quantity));
        return item;
    }

    public void RemoveItem(OrderItemId orderItemId)
    {
        EnsureOrderCanBeModified();

        var item = _items.FirstOrDefault(i => i.Id == orderItemId);
        if (item is null)
            throw new OrderDomainException($"Order item with ID '{orderItemId}' not found in order.");

        _items.Remove(item);
        RecalculateTotal();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateShippingAddress(Address newAddress)
    {
        EnsureOrderCanBeModified();
        ArgumentNullException.ThrowIfNull(newAddress);

        ShippingAddress = newAddress;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Submit()
    {
        if (Status != OrderStatus.Draft)
            throw new OrderDomainException($"Cannot submit order in status '{Status}'. Order must be in 'Draft' status.");

        if (_items.Count == 0)
            throw new OrderDomainException("Cannot submit an order with no items.");

        Status = OrderStatus.Submitted;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new OrderSubmittedDomainEvent(Id, CustomerId, TotalAmount));
    }

    public void Confirm()
    {
        if (Status is OrderStatus.Paid or OrderStatus.Shipped or OrderStatus.Cancelled)
            throw new OrderDomainException($"Cannot confirm order in status '{Status}'.");

        if (_items.Count == 0)
            throw new OrderDomainException("Cannot confirm an order without order items.");

        Status = OrderStatus.Confirmed;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new OrderConfirmedDomainEvent(Id, CustomerId, TotalAmount));
    }

    public void MarkAsPaid()
    {
        if (Status != OrderStatus.Confirmed && Status != OrderStatus.Submitted)
            throw new OrderDomainException($"Cannot mark order as paid when status is '{Status}'. Order must be 'Confirmed' or 'Submitted'.");

        Status = OrderStatus.Paid;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new OrderPaidDomainEvent(Id, CustomerId));
    }

    public void Ship()
    {
        if (Status != OrderStatus.Paid)
            throw new OrderDomainException($"Cannot ship order when status is '{Status}'. Order must be 'Paid'.");

        Status = OrderStatus.Shipped;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new OrderShippedDomainEvent(Id, CustomerId));
    }

    public void Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new OrderDomainException("Cancellation reason must be provided.");

        if (Status == OrderStatus.Shipped)
            throw new OrderDomainException("Cannot cancel an order that has already been shipped.");

        if (Status == OrderStatus.Cancelled)
            throw new OrderDomainException("Order is already cancelled.");

        Status = OrderStatus.Cancelled;
        CancellationReason = reason.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new OrderCancelledDomainEvent(Id, CustomerId, CancellationReason));
    }

    private void EnsureOrderCanBeModified()
    {
        if (Status is OrderStatus.Paid or OrderStatus.Shipped or OrderStatus.Cancelled)
            throw new OrderDomainException($"Order cannot be modified because its current status is '{Status}'.");
    }

    private void RecalculateTotal()
    {
        var total = Money.Zero(Currency);
        foreach (var item in _items)
        {
            total = total.Add(item.TotalPrice);
        }
        TotalAmount = total;
    }
}
