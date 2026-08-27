namespace FinSight.Application.DTOs.Reconciliation;

public sealed class ReconciliationRunSummaryResponse
{
    public Guid RunId { get; init; }

    public Guid BatchId { get; init; }

    public string Status { get; init; } = string.Empty;

    public int TotalUnits { get; init; }

    public int Matched { get; init; }

    public int Mismatched { get; init; }

    public int Missing { get; init; }

    public int Duplicate { get; init; }

    public int Unresolved { get; init; }

    public decimal MatchRate { get; init; }

    public int ExceptionCount { get; init; }
}