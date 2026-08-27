namespace FinSight.Application.DTOs.Reconciliation;

public sealed class StrategyEvidence
{
    public bool SourcesPresent { get; init; }

    public bool ExactReferenceMatch { get; init; }

    public bool ExactAmountMatch { get; init; }

    public bool ExactDateMatch { get; init; }

    public bool AmountWithinTolerance { get; init; }

    public bool DateWithinTolerance { get; init; }

    public bool AmountMismatch { get; init; }

    public bool DateMismatch { get; init; }

    public bool NonComparableBusinessState { get; init; }

    public string? NonComparableReason { get; init; }
}