using OrderService.Domain.Common;
using OrderService.Domain.Exceptions;
using OrderService.Domain.ValueObjects;

namespace OrderService.Domain.Entities;

public sealed class OrderItem : Entity<OrderItemId>
{
    public ProductId ProductId { get; private set; }
    public string ProductName { get; private set; }
    public Money UnitPrice { get; private set; }
    public int Quantity { get; private set; }

    public Money TotalPrice => UnitPrice.Multiply(Quantity);

    // Private constructor for EF Core
    private OrderItem()
    {
        ProductName = string.Empty;
        UnitPrice = Money.Zero();
    }

    internal OrderItem(OrderItemId id, ProductId productId, string productName, Money unitPrice, int quantity)
    {
        if (string.IsNullOrWhiteSpace(productName))
            throw new OrderDomainException("Product name cannot be empty.");

        if (quantity <= 0)
            throw new OrderDomainException("Quantity must be greater than zero.");

        ArgumentNullException.ThrowIfNull(unitPrice);

        Id = id;
        ProductId = productId;
        ProductName = productName.Trim();
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    internal void AddQuantity(int additionalQuantity)
    {
        if (additionalQuantity <= 0)
            throw new OrderDomainException("Additional quantity must be greater than zero.");

        Quantity += additionalQuantity;
    }

    internal void UpdateQuantity(int newQuantity)
    {
        if (newQuantity <= 0)
            throw new OrderDomainException("Quantity must be greater than zero.");

        Quantity = newQuantity;
    }
}
