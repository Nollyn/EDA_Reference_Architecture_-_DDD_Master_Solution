using Microsoft.AspNetCore.Mvc;
using OrderService.Application.Commands;
using OrderService.Application.Common;
using OrderService.Application.DTOs;
using OrderService.Application.Queries;

namespace OrderService.Api.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/orders")
            .WithTags("Orders")
            .WithOpenApi();

        // POST /api/v1/orders
        group.MapPost("/", async ([FromBody] CreateOrderCommand command, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var orderId = await mediator.SendAsync(command, cancellationToken);
            return Results.Created($"/api/v1/orders/{orderId}", new { Id = orderId });
        })
        .WithName("CreateOrder")
        .WithSummary("Create a new draft/submitted order")
        .Produces<object>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);

        // GET /api/v1/orders/{id}
        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var order = await mediator.SendAsync(new GetOrderByIdQuery(id), cancellationToken);
            return order is not null ? Results.Ok(order) : Results.NotFound();
        })
        .WithName("GetOrderById")
        .WithSummary("Get order details by ID")
        .Produces<OrderDetailsDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/orders (Keyset pagination)
        group.MapGet("/", async (
            [FromQuery] DateTimeOffset? cursorCreatedAt,
            [FromQuery] Guid? cursorId,
            [FromQuery] int? pageSize,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var query = new GetOrdersKeysetQuery(cursorCreatedAt, cursorId, pageSize ?? 20);
            var result = await mediator.SendAsync(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetOrdersPaged")
        .WithSummary("Get a paginated list of orders using high-performance keyset cursor pagination")
        .Produces<PagedResult<OrderSummaryDto>>(StatusCodes.Status200OK);

        // POST /api/v1/orders/{id}/confirm
        group.MapPost("/{id:guid}/confirm", async (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
        {
            await mediator.SendAsync(new ConfirmOrderCommand(id), cancellationToken);
            return Results.Ok(new { Message = "Order confirmed successfully." });
        })
        .WithName("ConfirmOrder")
        .WithSummary("Confirm an order")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);

        // POST /api/v1/orders/{id}/cancel
        group.MapPost("/{id:guid}/cancel", async (Guid id, [FromBody] CancelOrderRequest request, IMediator mediator, CancellationToken cancellationToken) =>
        {
            await mediator.SendAsync(new CancelOrderCommand(id, request.Reason), cancellationToken);
            return Results.Ok(new { Message = "Order cancelled successfully." });
        })
        .WithName("CancelOrder")
        .WithSummary("Cancel an order with a reason")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}

public sealed record CancelOrderRequest(string Reason);
