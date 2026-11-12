using FluentAssertions;
using OrderService.Domain.Aggregates;
using OrderService.Domain.Events;
using OrderService.Domain.Exceptions;
using OrderService.Domain.ValueObjects;
using Xunit;

namespace OrderService.Domain.Tests.Aggregates;

public sealed class OrderTests
{
    private readonly CustomerId _customerId = CustomerId.New();
    private readonly Address _shippingAddress = new("123 Main St", "Springfield", "IL", "62701", "USA");

    [Fact]
    public void Create_Should_InitializeOrderInDraftState_And_EmitOrderCreatedDomainEvent()
    {
        // Act
        var order = Order.Create(_customerId, _shippingAddress, "USD");

        // Assert
        order.Should().NotBeNull();
        order.CustomerId.Should().Be(_customerId);
        order.ShippingAddress.Should().Be(_shippingAddress);
        order.Status.Should().Be(OrderStatus.Draft);
        order.TotalAmount.Should().Be(Money.Zero("USD"));
        order.Currency.Should().Be("USD");
        order.Items.Should().BeEmpty();
        order.DomainEvents.Should().ContainSingle(e => e is OrderCreatedDomainEvent);
    }

    [Fact]
    public void AddItem_Should_AddOrderItem_And_RecalculateTotalAmount()
    {
        // Arrange
        var order = Order.Create(_customerId, _shippingAddress, "USD");
        var productId = ProductId.New();
        var price = Money.From(25.50m, "USD");

        // Act
        var item = order.AddItem(productId, "Clean Architecture Book", price, 2);

        // Assert
        order.Items.Should().HaveCount(1);
        item.TotalPrice.Should().Be(Money.From(51.00m, "USD"));
        order.TotalAmount.Should().Be(Money.From(51.00m, "USD"));
        order.DomainEvents.Should().Contain(e => e is OrderItemAddedDomainEvent);
    }

    [Fact]
    public void AddItem_WithExistingProduct_Should_IncrementQuantity_And_UpdateTotal()
    {
        // Arrange
        var order = Order.Create(_customerId, _shippingAddress, "USD");
        var productId = ProductId.New();
        var price = Money.From(10.00m, "USD");

        order.AddItem(productId, "Product A", price, 2);

        // Act
        order.AddItem(productId, "Product A", price, 3);

        // Assert
        order.Items.Should().HaveCount(1);
        order.Items.First().Quantity.Should().Be(5);
        order.TotalAmount.Should().Be(Money.From(50.00m, "USD"));
    }

    [Fact]
    public void AddItem_WithMismatchedCurrency_Should_ThrowOrderDomainException()
    {
        // Arrange
        var order = Order.Create(_customerId, _shippingAddress, "USD");
        var productId = ProductId.New();
        var eurPrice = Money.From(10.00m, "EUR");

        // Act
        var act = () => order.AddItem(productId, "Product A", eurPrice, 1);

        // Assert
        act.Should().Throw<OrderDomainException>()
            .WithMessage("*does not match order currency*");
    }

    [Fact]
    public void Submit_WithoutItems_Should_ThrowOrderDomainException()
    {
        // Arrange
        var order = Order.Create(_customerId, _shippingAddress, "USD");

        // Act
        var act = () => order.Submit();

        // Assert
        act.Should().Throw<OrderDomainException>()
            .WithMessage("*no items*");
    }

    [Fact]
    public void Submit_WithItems_Should_TransitionToSubmitted_And_EmitEvent()
    {
        // Arrange
        var order = Order.Create(_customerId, _shippingAddress, "USD");
        order.AddItem(ProductId.New(), "Item 1", Money.From(15.00m, "USD"), 2);

        // Act
        order.Submit();

        // Assert
        order.Status.Should().Be(OrderStatus.Submitted);
        order.DomainEvents.Should().Contain(e => e is OrderSubmittedDomainEvent);
    }

    [Fact]
    public void Confirm_WithoutItems_Should_ThrowOrderDomainException()
    {
        // Arrange
        var order = Order.Create(_customerId, _shippingAddress, "USD");

        // Act
        var act = () => order.Confirm();

        // Assert
        act.Should().Throw<OrderDomainException>()
            .WithMessage("*without order items*");
    }

    [Fact]
    public void Confirm_WithItems_Should_TransitionToConfirmed_And_EmitEvent()
    {
        // Arrange
        var order = Order.Create(_customerId, _shippingAddress, "USD");
        order.AddItem(ProductId.New(), "Item 1", Money.From(100.00m, "USD"), 1);

        // Act
        order.Confirm();

        // Assert
        order.Status.Should().Be(OrderStatus.Confirmed);
        order.DomainEvents.Should().Contain(e => e is OrderConfirmedDomainEvent);
    }

    [Fact]
    public void MarkAsPaid_WhenConfirmed_Should_TransitionToPaid()
    {
        // Arrange
        var order = Order.Create(_customerId, _shippingAddress, "USD");
        order.AddItem(ProductId.New(), "Item 1", Money.From(50.00m, "USD"), 1);
        order.Confirm();

        // Act
        order.MarkAsPaid();

        // Assert
        order.Status.Should().Be(OrderStatus.Paid);
        order.DomainEvents.Should().Contain(e => e is OrderPaidDomainEvent);
    }

    [Fact]
    public void Ship_WhenPaid_Should_TransitionToShipped()
    {
        // Arrange
        var order = Order.Create(_customerId, _shippingAddress, "USD");
        order.AddItem(ProductId.New(), "Item 1", Money.From(50.00m, "USD"), 1);
        order.Confirm();
        order.MarkAsPaid();

        // Act
        order.Ship();

        // Assert
        order.Status.Should().Be(OrderStatus.Shipped);
        order.DomainEvents.Should().Contain(e => e is OrderShippedDomainEvent);
    }

    [Fact]
    public void AddItem_WhenOrderIsPaidOrShipped_Should_ThrowOrderDomainException()
    {
        // Arrange
        var order = Order.Create(_customerId, _shippingAddress, "USD");
        order.AddItem(ProductId.New(), "Item 1", Money.From(50.00m, "USD"), 1);
        order.Confirm();
        order.MarkAsPaid();

        // Act
        var act = () => order.AddItem(ProductId.New(), "Item 2", Money.From(20.00m, "USD"), 1);

        // Assert
        act.Should().Throw<OrderDomainException>()
            .WithMessage("*cannot be modified*");
    }

    [Fact]
    public void Cancel_WhenShipped_Should_ThrowOrderDomainException()
    {
        // Arrange
        var order = Order.Create(_customerId, _shippingAddress, "USD");
        order.AddItem(ProductId.New(), "Item 1", Money.From(50.00m, "USD"), 1);
        order.Confirm();
        order.MarkAsPaid();
        order.Ship();

        // Act
        var act = () => order.Cancel("Customer changed mind");

        // Assert
        act.Should().Throw<OrderDomainException>()
            .WithMessage("*already been shipped*");
    }

    [Fact]
    public void Cancel_WhenDraftOrSubmittedOrConfirmed_Should_TransitionToCancelled_And_RecordReason()
    {
        // Arrange
        var order = Order.Create(_customerId, _shippingAddress, "USD");
        order.AddItem(ProductId.New(), "Item 1", Money.From(50.00m, "USD"), 1);
        order.Submit();

        // Act
        order.Cancel("Inventory reservation out of stock");

        // Assert
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancellationReason.Should().Be("Inventory reservation out of stock");
        order.DomainEvents.Should().Contain(e => e is OrderCancelledDomainEvent);
    }
}
