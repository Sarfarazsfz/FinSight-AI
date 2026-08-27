namespace FinSight.Application.DTOs.Reconciliation;

public sealed class ReconciliationResultResponse
{
    public Guid ResultId { get; init; }

    public Guid RunId { get; init; }

    public Guid NormalizedTransactionId { get; init; }

    public string TransactionReference { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? StrategyUsed { get; init; }

    public string ReasonCode { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }
}