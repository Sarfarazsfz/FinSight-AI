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

    /// <summary>
    /// Wall-clock milliseconds between the run's persisted StartedAt and
    /// CompletedAt -- the window that brackets the matching and
    /// classification loop.
    ///
    /// Null while a run has no CompletedAt (Pending/Running, or a crash
    /// artifact): an unfinished run has no duration, and reporting 0 would
    /// be an invented value rather than an absent one.
    ///
    /// This is a single wall-clock measurement of one run on whatever
    /// machine executed it. It is not a benchmark, and nothing here
    /// distinguishes a cold run from a warm one.
    /// </summary>
    public double? DurationMs { get; init; }

    /// <summary>
    /// TotalUnits divided by the duration in seconds, or null when
    /// DurationMs is null or zero -- a sub-tick run yields no meaningful
    /// rate, and dividing by it would produce Infinity, which
    /// System.Text.Json cannot represent.
    /// </summary>
    public double? RecordsPerSecond { get; init; }
}