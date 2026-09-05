using FinSight.Domain.Entities;

namespace FinSight.Application.Abstractions.Services;

/// <summary>
/// The single place ownership is resolved for the whole reconciliation
/// surface -- batches, runs, and everything a run owns (results,
/// exceptions, evidence, AI explanation, ground-truth verification).
///
/// Batch is the ownership root (see Batch.CreatedByUserId); a
/// ReconciliationRun carries no owner of its own and is never granted one
/// -- it is owned exactly by whoever owns the batch it was created from,
/// which is why every result/exception/evidence row can be authorized
/// through one join instead of a UserId column duplicated onto every
/// table.
///
/// Both methods return null uniformly for "does not exist" and "exists
/// but is not yours" -- callers must map null to 404 with a message that
/// does not reveal which case occurred, so a caller cannot use response
/// differences to enumerate other users' resources.
/// </summary>
public interface IBatchAccessService
{
    Task<Batch?> GetOwnedBatchAsync(
        Guid batchId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ReconciliationRun?> GetOwnedRunAsync(
        Guid runId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
