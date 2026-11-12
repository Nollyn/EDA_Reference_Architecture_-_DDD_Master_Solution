using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace OrderService.Infrastructure.Persistence;

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

public sealed class DatabaseInitializer : IDatabaseInitializer
{
    private readonly OrderDbContext _dbContext;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(OrderDbContext dbContext, ILogger<DatabaseInitializer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Ensuring PostgreSQL database schema is initialized...");

        await _dbContext.Database.EnsureCreatedAsync(cancellationToken);

        const string createIndexesSql = """
            CREATE INDEX IF NOT EXISTS ix_orders_created_at_id ON orders (created_at DESC, id DESC);
            CREATE INDEX IF NOT EXISTS ix_orders_customer_id ON orders (customer_id);
            CREATE INDEX IF NOT EXISTS ix_order_items_order_id ON order_items (order_id);
            CREATE INDEX IF NOT EXISTS ix_outbox_messages_pending ON outbox_messages (occurred_on ASC) WHERE processed_on IS NULL;
            """;

        try
        {
            await _dbContext.Database.ExecuteSqlRawAsync(createIndexesSql, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Index creation warning (may already exist or using non-Postgres provider).");
        }

        _logger.LogInformation("Database schema initialized successfully.");
    }
}
