using FinSight.Application.Abstractions.Persistence;
using FinSight.Domain.Entities;
using FinSight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Repositories;

public sealed class PaymentRecordRepository
    : IPaymentRecordRepository
{
    private readonly AppDbContext _dbContext;

    public PaymentRecordRepository(
        AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddRangeAsync(
        IReadOnlyCollection<PaymentRecord> records,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.PaymentRecords.AddRangeAsync(
            records,
            cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentRecord>> GetByBatchIdAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PaymentRecords
            .AsNoTracking()
            .Where(x => x.BatchId == batchId)
            .OrderBy(x => x.TransactionReference)
            .ToListAsync(cancellationToken);
    }

    public async Task<PaymentRecord?> GetByIdAsync(
        Guid recordId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PaymentRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == recordId,
                cancellationToken);
    }
}