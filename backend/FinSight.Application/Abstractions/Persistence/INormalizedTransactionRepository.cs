using FinSight.Domain.Entities;

namespace FinSight.Application.Abstractions.Persistence;

public interface INormalizedTransactionRepository
{
    Task AddRangeAsync(
        IReadOnlyCollection<NormalizedTransaction> transactions,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NormalizedTransaction>> GetByRunIdAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<NormalizedTransaction?> GetByIdAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default);
}