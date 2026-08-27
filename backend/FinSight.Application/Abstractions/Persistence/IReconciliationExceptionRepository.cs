using FinSight.Domain.Entities;

namespace FinSight.Application.Abstractions.Persistence;

public interface IReconciliationExceptionRepository
{
    Task<ReconciliationException?> GetByIdAsync(
        Guid exceptionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReconciliationException>> GetByRunIdAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ReconciliationException> Items, int TotalCount)>
        GetPageByRunIdAsync(
            Guid runId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);

    Task AddAsync(
        ReconciliationException exception,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(
        IReadOnlyCollection<ReconciliationException> exceptions,
        CancellationToken cancellationToken = default);
}
