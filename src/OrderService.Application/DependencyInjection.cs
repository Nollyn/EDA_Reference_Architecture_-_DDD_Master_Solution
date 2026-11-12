using Microsoft.Extensions.DependencyInjection;
using OrderService.Application.Commands;
using OrderService.Application.Common;
using OrderService.Application.DTOs;
using OrderService.Application.Queries;

namespace OrderService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IMediator, Mediator>();

        // Register Command Handlers
        services.AddScoped<IRequestHandler<CreateOrderCommand, Guid>, CreateOrderCommandHandler>();
        services.AddScoped<IRequestHandler<ConfirmOrderCommand, bool>, ConfirmOrderCommandHandler>();
        services.AddScoped<IRequestHandler<CancelOrderCommand, bool>, CancelOrderCommandHandler>();
        services.AddScoped<IRequestHandler<ProcessInventoryReservationFailedCommand, bool>, ProcessInventoryReservationFailedCommandHandler>();
        services.AddScoped<IRequestHandler<ProcessInventoryReservedCommand, bool>, ProcessInventoryReservedCommandHandler>();

        // Register Query Handlers
        services.AddScoped<IRequestHandler<GetOrderByIdQuery, OrderDetailsDto?>, GetOrderByIdQueryHandler>();
        services.AddScoped<IRequestHandler<GetOrdersKeysetQuery, PagedResult<OrderSummaryDto>>, GetOrdersKeysetQueryHandler>();

        return services;
    }
}
