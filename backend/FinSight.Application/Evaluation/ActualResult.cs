namespace FinSight.Application.Evaluation;

public sealed class ActualResult
{
    public Guid ResultId { get; set; }

    public Guid RunId { get; set; }

    public Guid NormalizedTransactionId { get; set; }

    public string TransactionReference { get; set; } =
        string.Empty;

    public string Status { get; set; } =
        string.Empty;

    public string? StrategyUsed { get; set; }

    public string ReasonCode { get; set; } =
        string.Empty;

    public DateTime CreatedAt { get; set; }
}
