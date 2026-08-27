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

    public async Task AddAsync(
        Batch batch,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Batches.AddAsync(
            batch,
            cancellationToken);
    }
}