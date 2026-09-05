using FinSight.Domain.Entities;

namespace FinSight.Application.Abstractions.Persistence;

public interface IBatchRepository
{
    Task<Batch?> GetByIdAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Batch> Items, int TotalCount)> GetPageAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Same pagination/ordering contract as <see cref="GetPageAsync"/>,
    /// but scoped to one owner -- the only listing method the API
    /// actually exposes. Kept as a separate method rather than adding a
    /// parameter to GetPageAsync so existing callers/tests of the
    /// unfiltered listing are unaffected.
    /// </summary>
    Task<(IReadOnlyList<Batch> Items, int TotalCount)> GetPageByOwnerAsync(
        Guid ownerUserId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Batch batch,
        CancellationToken cancellationToken = default);
}