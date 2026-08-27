using FinSight.Domain.Entities;

namespace FinSight.Application.Abstractions.Persistence;

public interface IReconciliationRunRepository
{
    Task<ReconciliationRun?> GetByIdAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        ReconciliationRun run,
        CancellationToken cancellationToken = default);
}