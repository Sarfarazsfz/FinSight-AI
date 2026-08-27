using FinSight.Domain.Enums;

namespace FinSight.Application.DTOs.Reconciliation;

public sealed class ReconciliationRunResult
{
    public Guid RunId { get; init; }

    public Guid BatchId { get; init; }

    public ReconciliationRunStatus Status { get; init; }

    public int TotalReconciliationUnits { get; init; }

    public int MatchedCount { get; init; }

    public int MismatchedCount { get; init; }

    public int MissingCount { get; init; }

    public int DuplicateCount { get; init; }

    public int UnresolvedCount { get; init; }

    public decimal MatchRate { get; init; }
}