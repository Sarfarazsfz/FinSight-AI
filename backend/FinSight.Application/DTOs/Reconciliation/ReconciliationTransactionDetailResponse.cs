namespace FinSight.Application.DTOs.Reconciliation;

public sealed class ReconciliationTransactionDetailResponse
{
    public Guid ResultId { get; init; }

    public Guid RunId { get; init; }

    public Guid NormalizedTransactionId { get; init; }

    public string TransactionReference { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? StrategyUsed { get; init; }

    public string ReasonCode { get; init; } = string.Empty;

    public IReadOnlyList<SourceTransactionRecordResponse> Payments { get; init; }
        = Array.Empty<SourceTransactionRecordResponse>();

    public IReadOnlyList<SourceTransactionRecordResponse> Banks { get; init; }
        = Array.Empty<SourceTransactionRecordResponse>();

    public IReadOnlyList<SourceTransactionRecordResponse> Settlements { get; init; }
        = Array.Empty<SourceTransactionRecordResponse>();
}

public sealed class SourceTransactionRecordResponse
{
    public Guid Id { get; init; }

    public string SourceRecordIdentifier { get; init; } = string.Empty;

    public string TransactionReference { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string Currency { get; init; } = string.Empty;

    public DateOnly TransactionDate { get; init; }

    public string Status { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }
}