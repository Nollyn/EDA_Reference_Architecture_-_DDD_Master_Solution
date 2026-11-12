using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OrderService.Application.Commands;
using OrderService.Application.DTOs;
using OrderService.Application.Events;
using OrderService.Application.Exceptions;
using OrderService.Application.Ports;
using OrderService.Domain.Aggregates;
using OrderService.Domain.Repositories;
using OrderService.Domain.ValueObjects;
using Xunit;

namespace OrderService.Domain.Tests.Commands;

public sealed class CommandHandlersTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IIntegrationEventPublisher _eventPublisher = Substitute.For<IIntegrationEventPublisher>();
    private readonly IFraudCheckService _fraudCheckService = Substitute.For<IFraudCheckService>();

    [Fact]
    public async Task CreateOrderCommandHandler_WithValidRequestAndApprovedFraud_Should_PersistOrderAndPublishIntegrationEvent()
    {
        // Arrange
        _fraudCheckService.CheckFraudAsync(Arg.Any<OrderId>(), Arg.Any<CustomerId>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(FraudCheckResult.Approved());

        var handler = new CreateOrderCommandHandler(_orderRepository, _unitOfWork, _eventPublisher, _fraudCheckService);

        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            new AddressDto("100 Tech Way", "Austin", "TX", "78701", "USA"),
            "USD",
            [new CreateOrderItemDto(Guid.NewGuid(), "Mechanical Keyboard", 150.00m, 1)]);

        // Act
        var orderId = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        orderId.Should().NotBeEmpty();
        await _orderRepository.Received(1).AddAsync(Arg.Is<Order>(o => o.Id.Value == orderId), Arg.Any<CancellationToken>());
        await _eventPublisher.Received(1).PublishAsync(Arg.Is<OrderCreatedIntegrationEvent>(e => e.OrderId == orderId), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateOrderCommandHandler_WhenFraudCheckFails_Should_ThrowValidationException_And_NotPersist()
    {
        // Arrange
        _fraudCheckService.CheckFraudAsync(Arg.Any<OrderId>(), Arg.Any<CustomerId>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(FraudCheckResult.Rejected("High risk score"));

        var handler = new CreateOrderCommandHandler(_orderRepository, _unitOfWork, _eventPublisher, _fraudCheckService);

        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            new AddressDto("100 Tech Way", "Austin", "TX", "78701", "USA"),
            "USD",
            [new CreateOrderItemDto(Guid.NewGuid(), "Mechanical Keyboard", 150.00m, 1)]);

        // Act
        var act = () => handler.HandleAsync(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*validation failures*");

        await _orderRepository.DidNotReceive().AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessInventoryReservationFailedCommandHandler_Should_CancelOrderAndPublishCompensatingEvent()
    {
        // Arrange
        var customerId = CustomerId.New();
        var address = new Address("Street", "City", "State", "12345", "Country");
        var order = Order.Create(customerId, address, "USD");
        order.AddItem(ProductId.New(), "Item", Money.From(50m, "USD"), 1);
        order.Submit();

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var logger = Substitute.For<ILogger<ProcessInventoryReservationFailedCommandHandler>>();
        var handler = new ProcessInventoryReservationFailedCommandHandler(_orderRepository, _unitOfWork, _eventPublisher, logger);

        var command = new ProcessInventoryReservationFailedCommand(order.Id.Value, "Item out of stock");

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancellationReason.Should().Contain("Item out of stock");

        await _orderRepository.Received(1).UpdateAsync(order, Arg.Any<CancellationToken>());
        await _eventPublisher.Received(1).PublishAsync(Arg.Is<OrderCancelledIntegrationEvent>(e => e.OrderId == order.Id.Value), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
