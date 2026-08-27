using FinSight.Domain.Entities;

namespace FinSight.Application.Abstractions.Persistence;

public interface IBankRecordRepository
{
    Task AddRangeAsync(
        IReadOnlyCollection<BankRecord> records,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BankRecord>> GetByBatchIdAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);

    Task<BankRecord?> GetByIdAsync(
        Guid recordId,
        CancellationToken cancellationToken = default);
}