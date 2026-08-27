using FinSight.Domain.Enums;

namespace FinSight.Domain.Entities;

public class ReconciliationResult
{
    public Guid Id { get; private set; }

    public Guid RunId { get; private set; }

    public Guid NormalizedTransactionId { get; private set; }

    public MatchStatus Status { get; private set; }

    public string? StrategyUsed { get; private set; }

    public ReconciliationReasonCode ReasonCode { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private ReconciliationResult()
    {
    }

    public ReconciliationResult(
        Guid runId,
        Guid normalizedTransactionId,
        MatchStatus status,
        ReconciliationReasonCode reasonCode,
        string? strategyUsed = null)
    {
        Id = Guid.NewGuid();
        RunId = runId;
        NormalizedTransactionId = normalizedTransactionId;
        Status = status;
        ReasonCode = reasonCode;
        StrategyUsed = strategyUsed;
        CreatedAt = DateTime.UtcNow;
    }
}