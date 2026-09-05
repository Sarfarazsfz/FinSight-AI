using FinSight.Application.Abstractions.Persistence;
using FinSight.Domain.Entities;
using FinSight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Repositories;

public class BatchRepository : IBatchRepository
{
    private readonly AppDbContext _dbContext;

    public BatchRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Batch?> GetByIdAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Batches
            .FirstOrDefaultAsync(
                x => x.Id == batchId,
                cancellationToken);
    }

    public async Task<(IReadOnlyList<Batch> Items, int TotalCount)>
        GetPageAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
    {
        var query =
            _dbContext.Batches
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id);

        var totalCount =
            await query.CountAsync(cancellationToken);

        var items =
            await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<Batch> Items, int TotalCount)>
        GetPageByOwnerAsync(
            Guid ownerUserId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
    {
        var query =
            _dbContext.Batches
                .AsNoTracking()
                .Where(x => x.CreatedByUserId == ownerUserId)
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id);

        var totalCount =
            await query.CountAsync(cancellationToken);

        var items =
            await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(
        Batch batch,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Batches.AddAsync(
            batch,
            cancellationToken);
    }
}