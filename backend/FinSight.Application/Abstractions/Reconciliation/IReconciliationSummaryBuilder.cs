using FinSight.Application.DTOs.Reconciliation;

namespace FinSight.Application.Abstractions.Reconciliation;

/// <summary>
/// Single authoritative source for reconciliation run summary
/// calculation. Extracted in Phase 3 to eliminate the duplicated
/// per-status counting logic that previously lived independently in
/// both ReconciliationSummaryTool (the AI Finance Assistant tool) and
/// ReconciliationController.GetSummary.
/// </summary>
public interface IReconciliationSummaryBuilder
{
    /// <summary>
    /// Builds the summary for the given run, or null if no run with
    /// that ID exists -- callers decide how to represent "not found"
    /// in their own response envelope (HTTP 404 vs. a tool error code).
    /// </summary>
    Task<ReconciliationRunSummaryResponse?> BuildAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
}
