using System.Data;
using Microsoft.EntityFrameworkCore;

namespace SchoolAdmission.Api.Data;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _dbContext;

    public UnitOfWork(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        if (!_dbContext.Database.IsRelational())
        {
            var result = await action();
            await _dbContext.SaveChangesAsync(ct);
            return result;
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var result = await action();
            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default)
    {
        if (!_dbContext.Database.IsRelational())
        {
            await action();
            await _dbContext.SaveChangesAsync(ct);
            return;
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            await action();
            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return _dbContext.SaveChangesAsync(ct);
    }
}

