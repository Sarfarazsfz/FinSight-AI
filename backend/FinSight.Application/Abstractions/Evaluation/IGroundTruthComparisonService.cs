using FinSight.Application.Evaluation;

namespace FinSight.Application.Abstractions.Evaluation;

/// <summary>
/// Builds the actual-side comparison data from a run's already-persisted
/// results/exceptions and compares it against caller-supplied ground
/// truth using the shared GroundTruthComparer. Does not check whether
/// the run exists -- callers (e.g. ReconciliationController) do that,
/// matching the existing convention of a direct null check before
/// delegating.
/// </summary>
public interface IGroundTruthComparisonService
{
    Task<GroundTruthComparisonResult> CompareAsync(
        Guid runId,
        IReadOnlyList<GroundTruthRow> expectedRows,
        CancellationToken cancellationToken = default);
}
