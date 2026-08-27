namespace FinSight.Application.DTOs.Reconciliation;

public sealed class ReconciliationExceptionResponse
{
    public Guid ExceptionId { get; init; }

    public Guid RunId { get; init; }

    public Guid ReconciliationResultId { get; init; }

    public string TransactionReference { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string InvolvedSources { get; init; } = string.Empty;

    public string DiscrepancyDetail { get; init; } = string.Empty;

    public string? AiExplanation { get; init; }

    public string? AiSuggestedCategory { get; init; }

    public DateTime? AiExplanationGeneratedAt { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }
}