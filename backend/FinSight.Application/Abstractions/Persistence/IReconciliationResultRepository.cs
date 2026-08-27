using FinSight.Domain.Entities;

namespace FinSight.Application.Abstractions.Persistence;

public interface IReconciliationResultRepository
{
    Task<ReconciliationResult?> GetByIdAsync(
        Guid resultId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReconciliationResult>> GetByRunIdAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ReconciliationResult> Items, int TotalCount)>
        GetPageByRunIdAsync(
            Guid runId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);

    Task AddAsync(
        ReconciliationResult result,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(
        IReadOnlyCollection<ReconciliationResult> results,
        CancellationToken cancellationToken = default);
}
