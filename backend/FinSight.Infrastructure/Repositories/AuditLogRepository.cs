using FinSight.Application.Abstractions.Persistence;
using FinSight.Domain.Entities;
using FinSight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Repositories;

/// <summary>
/// The single implementation over the single audit_logs table --
/// implementing both the write side (IAuditLogWriter, used throughout
/// reconciliation/ingestion/AI execution) and the read side
/// (IAuditLogReader, used only by the read-only audit evidence endpoint).
/// One storage mechanism, two narrow interfaces, so a component that only
/// needs to read never has write methods in its dependency's shape.
/// </summary>
public sealed class AuditLogRepository
    : IAuditLogWriter, IAuditLogReader
{
    private readonly AppDbContext _dbContext;

    public AuditLogRepository(
        AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        AuditLog auditLog,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.AuditLogs.AddAsync(
            auditLog,
            cancellationToken);
    }

    public async Task AddRangeAsync(
        IReadOnlyCollection<AuditLog> auditLogs,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.AuditLogs.AddRangeAsync(
            auditLogs,
            cancellationToken);
    }

    public async Task<(IReadOnlyList<AuditLog> Items, int TotalCount)>
        GetPageByRunIdAsync(
            Guid runId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
    {
        // AsNoTracking: this is a read-only viewer, never an editor --
        // there is no update path for these entities to track. The
        // run_id foreign key already carries an index (IX_audit_logs_run_id,
        // from the initial migration), so this filters on an indexed
        // column rather than scanning the table.
        var query =
            _dbContext.AuditLogs
                .AsNoTracking()
                .Where(x => x.RunId == runId)
                .OrderByDescending(x => x.OccurredAt)
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
}