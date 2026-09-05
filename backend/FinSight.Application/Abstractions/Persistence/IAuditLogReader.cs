using FinSight.Domain.Entities;

namespace FinSight.Application.Abstractions.Persistence;

/// <summary>
/// Read-only access to the existing audit_logs table. Deliberately a
/// separate interface from <see cref="IAuditLogWriter"/> -- a component
/// that only needs to display audit evidence (the API controller) should
/// never even have the shape of a write method available to it, let alone
/// a "create/edit/delete audit event" endpoint. Both interfaces are
/// implemented by the same repository, over the same table: this is read
/// access to the existing canonical audit store, not a second audit
/// system.
/// </summary>
public interface IAuditLogReader
{
    /// <summary>
    /// Newest-first, with Id as a stable tie-break for entries recorded
    /// in the same instant (the orchestrator writes several audit rows
    /// per run in a single request).
    /// </summary>
    Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetPageByRunIdAsync(
        Guid runId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
