using FinSight.Application.Abstractions.Persistence;
using FinSight.Domain.Entities;
using FinSight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Repositories;

public sealed class ReconciliationExceptionRepository
    : IReconciliationExceptionRepository
{
    private readonly AppDbContext _dbContext;

    public ReconciliationExceptionRepository(
        AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ReconciliationException?> GetByIdAsync(
        Guid exceptionId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ReconciliationExceptions
            .FirstOrDefaultAsync(
                x => x.Id == exceptionId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<ReconciliationException>> GetByRunIdAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ReconciliationExceptions
            .AsNoTracking()
            .Where(x => x.RunId == runId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<
        (IReadOnlyList<ReconciliationException> Items, int TotalCount)>
        GetPageByRunIdAsync(
            Guid runId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
    {
        var query =
            _dbContext.ReconciliationExceptions
                .AsNoTracking()
                .Where(x => x.RunId == runId)
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.Id);

        var totalCount =
            await query.CountAsync(cancellationToken);

        var items =
            await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(
        ReconciliationException exception,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.ReconciliationExceptions.AddAsync(
            exception,
            cancellationToken);
    }

    public async Task AddRangeAsync(
        IReadOnlyCollection<ReconciliationException> exceptions,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.ReconciliationExceptions.AddRangeAsync(
            exceptions,
            cancellationToken);
    }
}
