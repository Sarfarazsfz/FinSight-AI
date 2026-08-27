using FinSight.Application.Abstractions.Persistence;
using FinSight.Domain.Entities;
using FinSight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Repositories;

public sealed class NormalizedTransactionRepository
    : INormalizedTransactionRepository
{
    private readonly AppDbContext _dbContext;

    public NormalizedTransactionRepository(
        AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddRangeAsync(
        IReadOnlyCollection<NormalizedTransaction> transactions,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.NormalizedTransactions.AddRangeAsync(
            transactions,
            cancellationToken);
    }

    public async Task<IReadOnlyList<NormalizedTransaction>> GetByRunIdAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.NormalizedTransactions
            .AsNoTracking()
            .Where(x => x.RunId == runId)
            .OrderBy(x => x.TransactionReference)
            .ToListAsync(cancellationToken);
    }

    public async Task<NormalizedTransaction?> GetByIdAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.NormalizedTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == transactionId,
                cancellationToken);
    }
}