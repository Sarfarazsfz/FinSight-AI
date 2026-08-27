namespace FinSight.Application.DTOs.Ai;

public sealed class AiExplanationRequest
{
    public Guid ExceptionId { get; init; }

    public Guid RunId { get; init; }

    public Guid ReconciliationResultId { get; init; }

    public string TransactionReference { get; init; }
        = string.Empty;

    public string DeterministicCategory { get; init; }
        = string.Empty;

    public string InvolvedSources { get; init; }
        = string.Empty;

    public string DiscrepancyDetail { get; init; }
        = string.Empty;
}