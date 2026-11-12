using OrderService.Application.DTOs;

namespace OrderService.Application.Ports;

public interface IOrderQueries
{
    Task<OrderDetailsDto?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<PagedResult<OrderSummaryDto>> GetOrdersPagedAsync(
        DateTimeOffset? cursorCreatedAt,
        Guid? cursorId,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}
