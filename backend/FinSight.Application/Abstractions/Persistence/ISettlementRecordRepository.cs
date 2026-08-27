using FinSight.Domain.Entities;

namespace FinSight.Application.Abstractions.Persistence;

public interface ISettlementRecordRepository
{
    Task AddRangeAsync(
        IReadOnlyCollection<SettlementRecord> records,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SettlementRecord>> GetByBatchIdAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);

    Task<SettlementRecord?> GetByIdAsync(
        Guid recordId,
        CancellationToken cancellationToken = default);
}