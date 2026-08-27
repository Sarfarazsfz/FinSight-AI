using FinSight.Application.Abstractions.Persistence;
using FinSight.Domain.Entities;
using FinSight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Repositories;

public sealed class BankRecordRepository
    : IBankRecordRepository
{
    private readonly AppDbContext _dbContext;

    public BankRecordRepository(
        AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddRangeAsync(
        IReadOnlyCollection<BankRecord> records,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.BankRecords.AddRangeAsync(
            records,
            cancellationToken);
    }

    public async Task<IReadOnlyList<BankRecord>> GetByBatchIdAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.BankRecords
            .AsNoTracking()
            .Where(x => x.BatchId == batchId)
            .OrderBy(x => x.TransactionReference)
            .ToListAsync(cancellationToken);
    }

    public async Task<BankRecord?> GetByIdAsync(
        Guid recordId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.BankRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == recordId,
                cancellationToken);
    }
}