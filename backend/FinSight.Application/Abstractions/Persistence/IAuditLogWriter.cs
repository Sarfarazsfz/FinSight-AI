using FinSight.Domain.Entities;

namespace FinSight.Application.Abstractions.Persistence;

public interface IAuditLogWriter
{
    Task AddAsync(
        AuditLog auditLog,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(
        IReadOnlyCollection<AuditLog> auditLogs,
        CancellationToken cancellationToken = default);
}