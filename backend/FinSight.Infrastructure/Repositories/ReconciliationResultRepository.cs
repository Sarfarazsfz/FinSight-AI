using FinSight.Application.Abstractions.Persistence;
using FinSight.Domain.Entities;
using FinSight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Repositories;

public sealed class ReconciliationResultRepository
    : IReconciliationResultRepository
{
    private readonly AppDbContext _dbContext;

    public ReconciliationResultRepository(
        AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ReconciliationResult?> GetByIdAsync(
        Guid resultId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ReconciliationResults
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == resultId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<ReconciliationResult>> GetByRunIdAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ReconciliationResults
            .AsNoTracking()
            .Where(x => x.RunId == runId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<
        (IReadOnlyList<ReconciliationResult> Items, int TotalCount)>
        GetPageByRunIdAsync(
            Guid runId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
    {
        var query =
            _dbContext.ReconciliationResults
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
        ReconciliationResult result,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.ReconciliationResults.AddAsync(
            result,
            cancellationToken);
    }

    public async Task AddRangeAsync(
        IReadOnlyCollection<ReconciliationResult> results,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.ReconciliationResults.AddRangeAsync(
            results,
            cancellationToken);
    }
}
