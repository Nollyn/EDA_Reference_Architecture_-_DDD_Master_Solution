using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Api.Endpoints;
using OrderService.Application.Commands;
using OrderService.Application.DTOs;
using OrderService.Infrastructure.Persistence;
using OrderService.Integration.Tests.Infrastructure;
using Xunit;

namespace OrderService.Integration.Tests.Endpoints;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public sealed class OrderApiIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    private readonly HttpClient _client;

    public OrderApiIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task CreateOrder_Should_PersistInDatabase_And_Return201Created_WithOutboxRecord()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var command = new CreateOrderCommand(
            customerId,
            new AddressDto("123 Tech Blvd", "Seattle", "WA", "98101", "USA"),
            "USD",
            [
                new CreateOrderItemDto(Guid.NewGuid(), "Domain-Driven Design Book", 45.00m, 1),
                new CreateOrderItemDto(Guid.NewGuid(), "USB-C Hub", 30.00m, 2)
            ]);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/orders", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Contains("X-Correlation-Id").Should().BeTrue();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = body.GetProperty("id").GetGuid();
        orderId.Should().NotBeEmpty();

        // Verify in Database & Outbox
        using var scope = _fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        var order = await dbContext.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == OrderService.Domain.ValueObjects.OrderId.From(orderId));
        order.Should().NotBeNull();
        order!.TotalAmount.Amount.Should().Be(105.00m);
        order.Items.Should().HaveCount(2);

        var outboxMessage = await dbContext.OutboxMessages.FirstOrDefaultAsync(m => m.Id == orderId || m.Content.Contains(orderId.ToString()));
        outboxMessage.Should().NotBeNull();
        outboxMessage!.Type.Should().Be("OrderService.OrderCreated");
    }

    [Fact]
    public async Task GetOrderById_WhenExists_Should_ReturnOrderDetails()
    {
        // Arrange
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            new AddressDto("456 Hexagonal Ave", "Chicago", "IL", "60601", "USA"),
            "USD",
            [new CreateOrderItemDto(Guid.NewGuid(), "Monitor 4K", 400.00m, 1)]);

        var createRes = await _client.PostAsJsonAsync("/api/v1/orders", command);
        var createBody = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = createBody.GetProperty("id").GetGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/orders/{orderId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var order = await response.Content.ReadFromJsonAsync<OrderDetailsDto>();
        order.Should().NotBeNull();
        order!.Id.Should().Be(orderId);
        order.TotalAmount.Should().Be(400.00m);
        order.ShippingAddress.City.Should().Be("Chicago");
    }

    [Fact]
    public async Task GetOrdersPaged_WithKeysetCursor_Should_ReturnCorrectPages()
    {
        // Arrange: Create multiple orders
        for (var i = 0; i < 3; i++)
        {
            var command = new CreateOrderCommand(
                Guid.NewGuid(),
                new AddressDto($"Street {i}", "Boston", "MA", "02101", "USA"),
                "USD",
                [new CreateOrderItemDto(Guid.NewGuid(), $"Product {i}", 10.00m * (i + 1), 1)]);
            await _client.PostAsJsonAsync("/api/v1/orders", command);
        }

        // Act - First page
        var page1Res = await _client.GetAsync("/api/v1/orders?pageSize=2");
        page1Res.StatusCode.Should().Be(HttpStatusCode.OK);

        var page1 = await page1Res.Content.ReadFromJsonAsync<PagedResult<OrderSummaryDto>>();
        page1.Should().NotBeNull();
        page1!.Items.Should().HaveCount(2);
        page1.HasNextPage.Should().BeTrue();
        page1.NextCursorCreatedAt.Should().NotBeNull();
        page1.NextCursorId.Should().NotBeNull();

        // Act - Second page using cursor
        var cursorDate = Uri.EscapeDataString(page1.NextCursorCreatedAt!.Value.ToString("O"));
        var cursorId = page1.NextCursorId!.Value;
        var page2Res = await _client.GetAsync($"/api/v1/orders?cursorCreatedAt={cursorDate}&cursorId={cursorId}&pageSize=2");

        // Assert
        page2Res.StatusCode.Should().Be(HttpStatusCode.OK);
        var page2 = await page2Res.Content.ReadFromJsonAsync<PagedResult<OrderSummaryDto>>();
        page2.Should().NotBeNull();
        page2!.Items.Should().NotBeEmpty();
        page2.Items.First().Id.Should().NotBe(page1.Items.First().Id);
    }

    [Fact]
    public async Task ConfirmAndCancelOrder_Should_TransitionStatesProperly()
    {
        // Arrange
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            new AddressDto("789 Reactive Rd", "Denver", "CO", "80201", "USA"),
            "USD",
            [new CreateOrderItemDto(Guid.NewGuid(), "Wireless Mouse", 50.00m, 1)]);

        var createRes = await _client.PostAsJsonAsync("/api/v1/orders", command);
        var createBody = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = createBody.GetProperty("id").GetGuid();

        // Confirm
        var confirmRes = await _client.PostAsync($"/api/v1/orders/{orderId}/confirm", null);
        confirmRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var getConfirmed = await _client.GetFromJsonAsync<OrderDetailsDto>($"/api/v1/orders/{orderId}");
        getConfirmed!.Status.Should().Be("Confirmed");

        // Cancel
        var cancelRes = await _client.PostAsJsonAsync($"/api/v1/orders/{orderId}/cancel", new CancelOrderRequest("Customer request"));
        cancelRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var getCancelled = await _client.GetFromJsonAsync<OrderDetailsDto>($"/api/v1/orders/{orderId}");
        getCancelled!.Status.Should().Be("Cancelled");
        getCancelled.CancellationReason.Should().Be("Customer request");
    }
}
