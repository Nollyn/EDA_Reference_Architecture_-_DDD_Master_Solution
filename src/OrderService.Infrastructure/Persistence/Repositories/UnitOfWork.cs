using Microsoft.EntityFrameworkCore;
using OrderService.Application.Exceptions;
using OrderService.Application.Ports;

namespace OrderService.Infrastructure.Persistence.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly OrderDbContext _dbContext;

    public UnitOfWork(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("A concurrency conflict occurred while attempting to save changes. The entity has been modified or deleted by another process.", ex);
        }
    }
}
