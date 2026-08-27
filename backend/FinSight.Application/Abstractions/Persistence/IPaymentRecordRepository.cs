using FinSight.Domain.Entities;

namespace FinSight.Application.Abstractions.Persistence;

public interface IPaymentRecordRepository
{
    Task AddRangeAsync(
        IReadOnlyCollection<PaymentRecord> records,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentRecord>> GetByBatchIdAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);

    Task<PaymentRecord?> GetByIdAsync(
        Guid recordId,
        CancellationToken cancellationToken = default);
}