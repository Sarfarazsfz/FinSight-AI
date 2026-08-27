using FinSight.Domain.Enums;

namespace FinSight.Application.DTOs.Reconciliation;

public sealed class ClassificationDecision
{
    public MatchStatus Status { get; init; }

    public ReconciliationReasonCode ReasonCode { get; init; }

    public string? StrategyUsed { get; init; }

    public ExceptionCategory? ExceptionCategory { get; init; }
}