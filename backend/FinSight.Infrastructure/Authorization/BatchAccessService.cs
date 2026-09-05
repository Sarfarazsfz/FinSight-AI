using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Services;
using FinSight.Domain.Entities;

namespace FinSight.Infrastructure.Authorization;

/// <summary>
/// Real, DB-backed implementation of the ownership boundary.
///
/// Exactly two indexed primary-key lookups per call (batch by id, run by
/// id then its batch by id) -- no collection is loaded to "discover"
/// ownership, so this stays cheap even called once per request across
/// several endpoints.
///
/// Has no HTTP dependency (unlike ICurrentUserService), so it is safe to
/// register from AddInfrastructure and resolve from a bare
/// ServiceCollection -- the offline create-user command and the DI tests
/// both build one without a web host.
/// </summary>
public sealed class BatchAccessService : IBatchAccessService
{
    private readonly IBatchRepository _batchRepository;
    private readonly IReconciliationRunRepository _runRepository;

    public BatchAccessService(
        IBatchRepository batchRepository,
        IReconciliationRunRepository runRepository)
    {
        _batchRepository = batchRepository;
        _runRepository = runRepository;
    }

    public async Task<Batch?> GetOwnedBatchAsync(
        Guid batchId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var batch =
            await _batchRepository.GetByIdAsync(batchId, cancellationToken);

        // A null CreatedByUserId (legacy/unmatched-backfill data) can
        // never equal a real userId, so an unowned batch is correctly
        // inaccessible to everyone rather than accidentally matching.
        return batch is not null && batch.CreatedByUserId == userId
            ? batch
            : null;
    }

    public async Task<ReconciliationRun?> GetOwnedRunAsync(
        Guid runId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var run =
            await _runRepository.GetByIdAsync(runId, cancellationToken);

        if (run is null)
        {
            return null;
        }

        var ownedBatch =
            await GetOwnedBatchAsync(run.BatchId, userId, cancellationToken);

        return ownedBatch is not null ? run : null;
    }
}
