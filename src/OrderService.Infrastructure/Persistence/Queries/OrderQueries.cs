using System.Data;
using Dapper;
using Npgsql;
using OrderService.Application.DTOs;
using OrderService.Application.Ports;

namespace OrderService.Infrastructure.Persistence.Queries;

public sealed class OrderQueries : IOrderQueries
{
    private readonly string _connectionString;

    public OrderQueries(string connectionString)
    {
        _connectionString = connectionString;
    }

    private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

    public async Task<OrderDetailsDto?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        using var connection = CreateConnection();

        const string orderSql = """
            SELECT
                id AS Id,
                customer_id AS CustomerId,
                status AS Status,
                total_amount AS TotalAmount,
                total_currency AS Currency,
                cancellation_reason AS CancellationReason,
                shipping_street AS Street,
                shipping_city AS City,
                shipping_state AS State,
                shipping_postal_code AS PostalCode,
                shipping_country AS Country,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt,
                version AS Version
            FROM orders
            WHERE id = @OrderId;
            """;

        const string itemsSql = """
            SELECT
                id AS Id,
                product_id AS ProductId,
                product_name AS ProductName,
                unit_price_amount AS UnitPrice,
                quantity AS Quantity,
                (unit_price_amount * quantity) AS TotalPrice,
                unit_price_currency AS Currency
            FROM order_items
            WHERE order_id = @OrderId;
            """;

        var orderRecord = await connection.QuerySingleOrDefaultAsync<dynamic>(new CommandDefinition(orderSql, new { OrderId = orderId }, cancellationToken: cancellationToken));
        if (orderRecord is null)
            return null;

        var items = (await connection.QueryAsync<OrderItemDto>(new CommandDefinition(itemsSql, new { OrderId = orderId }, cancellationToken: cancellationToken))).ToList();

        var address = new AddressDto(
            (string)orderRecord.street,
            (string)orderRecord.city,
            (string)orderRecord.state,
            (string)orderRecord.postalcode,
            (string)orderRecord.country);

        return new OrderDetailsDto(
            (Guid)orderRecord.id,
            (Guid)orderRecord.customerid,
            (string)orderRecord.status,
            (decimal)orderRecord.totalamount,
            (string)orderRecord.currency,
            (string?)orderRecord.cancellationreason,
            address,
            (DateTimeOffset)orderRecord.createdat,
            (DateTimeOffset?)orderRecord.updatedat,
            (uint)(long)orderRecord.version,
            items);
    }

    public async Task<PagedResult<OrderSummaryDto>> GetOrdersPagedAsync(
        DateTimeOffset? cursorCreatedAt,
        Guid? cursorId,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        using var connection = CreateConnection();

        // Fetch pageSize + 1 to detect if a next page exists without separate COUNT query
        var limit = pageSize + 1;

        const string sql = """
            SELECT
                o.id AS Id,
                o.customer_id AS CustomerId,
                o.status AS Status,
                o.total_amount AS TotalAmount,
                o.total_currency AS Currency,
                COUNT(i.id) AS TotalItems,
                o.created_at AS CreatedAt
            FROM orders o
            LEFT JOIN order_items i ON o.id = i.order_id
            WHERE (@CursorCreatedAt IS NULL OR (o.created_at < @CursorCreatedAt) OR (o.created_at = @CursorCreatedAt AND o.id < @CursorId))
            GROUP BY o.id, o.customer_id, o.status, o.total_amount, o.total_currency, o.created_at
            ORDER BY o.created_at DESC, o.id DESC
            LIMIT @Limit;
            """;

        var parameters = new
        {
            CursorCreatedAt = cursorCreatedAt,
            CursorId = cursorId,
            Limit = limit
        };

        var rows = (await connection.QueryAsync<OrderSummaryDto>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))).ToList();

        var hasNextPage = rows.Count > pageSize;
        var items = hasNextPage ? rows.Take(pageSize).ToList() : rows;

        DateTimeOffset? nextCursorCreatedAt = null;
        Guid? nextCursorId = null;

        if (hasNextPage && items.Count > 0)
        {
            var lastItem = items[^1];
            nextCursorCreatedAt = lastItem.CreatedAt;
            nextCursorId = lastItem.Id;
        }

        return new PagedResult<OrderSummaryDto>(
            items,
            pageSize,
            hasNextPage,
            nextCursorCreatedAt,
            nextCursorId);
    }
}
