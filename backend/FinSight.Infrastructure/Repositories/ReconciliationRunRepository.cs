using FinSight.Application.Abstractions.Persistence;
using FinSight.Domain.Entities;
using FinSight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Repositories;

public class ReconciliationRunRepository : IReconciliationRunRepository
{
    private readonly AppDbContext _dbContext;

    public ReconciliationRunRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ReconciliationRun?> GetByIdAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ReconciliationRuns
            .FirstOrDefaultAsync(
                x => x.Id == runId,
                cancellationToken);
    }

    public async Task AddAsync(
        ReconciliationRun run,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.ReconciliationRuns.AddAsync(
            run,
            cancellationToken);
    }
}