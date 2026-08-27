using FinSight.Application.Abstractions.Persistence;
using FinSight.Domain.Entities;
using FinSight.Infrastructure.Persistence;

namespace FinSight.Infrastructure.Repositories;

public sealed class AuditLogRepository
    : IAuditLogWriter
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
}