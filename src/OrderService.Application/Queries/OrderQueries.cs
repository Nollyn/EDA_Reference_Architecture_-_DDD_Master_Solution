using OrderService.Application.Common;
using OrderService.Application.DTOs;
using OrderService.Application.Ports;

namespace OrderService.Application.Queries;

public sealed record GetOrderByIdQuery(Guid OrderId) : IRequest<OrderDetailsDto?>;

public sealed class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderDetailsDto?>
{
    private readonly IOrderQueries _orderQueries;

    public GetOrderByIdQueryHandler(IOrderQueries orderQueries)
    {
        _orderQueries = orderQueries;
    }

    public Task<OrderDetailsDto?> HandleAsync(GetOrderByIdQuery request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _orderQueries.GetByIdAsync(request.OrderId, cancellationToken);
    }
}

public sealed record GetOrdersKeysetQuery(
    DateTimeOffset? CursorCreatedAt,
    Guid? CursorId,
    int PageSize = 20) : IRequest<PagedResult<OrderSummaryDto>>;

public sealed class GetOrdersKeysetQueryHandler : IRequestHandler<GetOrdersKeysetQuery, PagedResult<OrderSummaryDto>>
{
    private readonly IOrderQueries _orderQueries;

    public GetOrdersKeysetQueryHandler(IOrderQueries orderQueries)
    {
        _orderQueries = orderQueries;
    }

    public Task<PagedResult<OrderSummaryDto>> HandleAsync(GetOrdersKeysetQuery request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var size = request.PageSize is <= 0 or > 100 ? 20 : request.PageSize;
        return _orderQueries.GetOrdersPagedAsync(request.CursorCreatedAt, request.CursorId, size, cancellationToken);
    }
}
