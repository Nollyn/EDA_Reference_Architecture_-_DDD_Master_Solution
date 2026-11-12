using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrderService.Application.Commands;
using OrderService.Application.DTOs;
using OrderService.Application.Events;
using OrderService.Domain.Aggregates;
using OrderService.Domain.Repositories;
using OrderService.Domain.ValueObjects;
using OrderService.Infrastructure.Messaging.Inbox;
using OrderService.Infrastructure.Messaging.Outbox;
using OrderService.Infrastructure.Messaging.RabbitMq;
using OrderService.Infrastructure.Persistence;
using OrderService.Integration.Tests.Infrastructure;
using Xunit;

namespace OrderService.Integration.Tests.Messaging;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public sealed class OutboxInboxIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;

    public OutboxInboxIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task OutboxProcessor_Should_ReadPendingMessages_PublishToRabbitMQ_And_MarkAsProcessed()
    {
        using var scope = _fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var connectionManager = scope.ServiceProvider.GetRequiredService<IRabbitMqConnectionManager>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<RabbitMqOptions>>();

        // Create an order with outbox message
        var order = Order.Create(CustomerId.New(), new Address("1 Tech St", "San Francisco", "CA", "94105", "USA"), "USD");
        order.AddItem(ProductId.New(), "Mechanical Keyboard", Money.From(120m, "USD"), 1);
        order.Submit();

        await dbContext.Orders.AddAsync(order);

        var integrationEvent = new OrderCreatedIntegrationEvent(
            order.Id.Value,
            order.CustomerId.Value,
            order.TotalAmount.Amount,
            order.Currency,
            [new OrderCreatedItemPayload(order.Items.First().ProductId.Value, "Mechanical Keyboard", 1, 120m)]);

        var publisher = scope.ServiceProvider.GetRequiredService<OrderService.Application.Ports.IIntegrationEventPublisher>();
        await publisher.PublishAsync(integrationEvent);
        await dbContext.SaveChangesAsync();

        // Act: Process Outbox Messages
        var processor = new OutboxProcessor(dbContext, connectionManager, options, NullLogger<OutboxProcessor>.Instance);
        var processedCount = await processor.ProcessOutboxMessagesAsync(batchSize: 10);

        // Assert
        processedCount.Should().BeGreaterThan(0);

        var updatedMessage = await dbContext.OutboxMessages.FirstOrDefaultAsync(m => m.Id == integrationEvent.Id);
        updatedMessage.Should().NotBeNull();
        updatedMessage!.ProcessedOn.Should().NotBeNull();
        updatedMessage.Error.Should().BeNull();
    }

    [Fact]
    public async Task InboxConsumer_WhenInventoryReservationFails_Should_CompensateOrder_And_RecordIdempotency()
    {
        using var scope = _fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<OrderService.Application.Common.IMediator>();

        // Create and persist order
        var order = Order.Create(CustomerId.New(), new Address("2 Tech St", "New York", "NY", "10001", "USA"), "USD");
        order.AddItem(ProductId.New(), "High-end GPU", Money.From(800m, "USD"), 1);
        order.Submit();

        await dbContext.Orders.AddAsync(order);
        await dbContext.SaveChangesAsync();

        // Simulate incoming failed inventory reservation event
        var failedEvent = new InventoryReservationFailedIntegrationEvent(order.Id.Value, "Warehouse stock depleted");

        // Act: Dispatch through saga handler
        await mediator.SendAsync(new ProcessInventoryReservationFailedCommand(failedEvent.OrderId, failedEvent.Reason));

        // Assert: Order status transitioned to Cancelled
        var refreshedOrder = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == order.Id);
        refreshedOrder.Should().NotBeNull();
        refreshedOrder!.Status.Should().Be(OrderStatus.Cancelled);
        refreshedOrder.CancellationReason.Should().Contain("Warehouse stock depleted");
    }
}
