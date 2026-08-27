using FinSight.Domain.Entities;

namespace FinSight.Application.Abstractions.Persistence;

public interface IBatchRepository
{
    Task<Batch?> GetByIdAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Batch batch,
        CancellationToken cancellationToken = default);
}