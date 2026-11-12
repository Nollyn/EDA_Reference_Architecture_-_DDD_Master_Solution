namespace OrderService.Application.DTOs;

public sealed record AddressDto(
    string Street,
    string City,
    string State,
    string PostalCode,
    string Country);

public sealed record CreateOrderItemDto(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity);

public sealed record OrderItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal TotalPrice,
    string Currency);

public sealed record OrderDetailsDto(
    Guid Id,
    Guid CustomerId,
    string Status,
    decimal TotalAmount,
    string Currency,
    string? CancellationReason,
    AddressDto ShippingAddress,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    uint Version,
    IReadOnlyList<OrderItemDto> Items);

public sealed record OrderSummaryDto(
    Guid Id,
    Guid CustomerId,
    string Status,
    decimal TotalAmount,
    string Currency,
    int TotalItems,
    DateTimeOffset CreatedAt);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int PageSize,
    bool HasNextPage,
    DateTimeOffset? NextCursorCreatedAt,
    Guid? NextCursorId);
