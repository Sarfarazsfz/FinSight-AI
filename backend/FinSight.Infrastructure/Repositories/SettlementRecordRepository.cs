using FinSight.Application.Abstractions.Persistence;
using FinSight.Domain.Entities;
using FinSight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Repositories;

public sealed class SettlementRecordRepository
    : ISettlementRecordRepository
{
    private readonly AppDbContext _dbContext;

    public SettlementRecordRepository(
        AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddRangeAsync(
        IReadOnlyCollection<SettlementRecord> records,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.SettlementRecords.AddRangeAsync(
            records,
            cancellationToken);
    }

    public async Task<IReadOnlyList<SettlementRecord>> GetByBatchIdAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.SettlementRecords
            .AsNoTracking()
            .Where(x => x.BatchId == batchId)
            .OrderBy(x => x.TransactionReference)
            .ToListAsync(cancellationToken);
    }

    public async Task<SettlementRecord?> GetByIdAsync(
        Guid recordId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.SettlementRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == recordId,
                cancellationToken);
    }
}