using FinSight.Domain.Enums;

namespace FinSight.Domain.Entities;

public class ReconciliationException
{
    public Guid Id { get; private set; }

    public Guid RunId { get; private set; }

    public Guid ReconciliationResultId { get; private set; }

    public ExceptionCategory Category { get; private set; }

    public string InvolvedSources { get; private set; } = string.Empty;

    public string DiscrepancyDetail { get; private set; } = string.Empty;

    public string? AiExplanation { get; private set; }

    public string? AiSuggestedCategory { get; private set; }

    public DateTime? AiExplanationGeneratedAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    private ReconciliationException()
    {
    }

    public ReconciliationException(
        Guid runId,
        Guid reconciliationResultId,
        ExceptionCategory category,
        string involvedSources,
        string discrepancyDetail)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException(
                "Run ID is required.",
                nameof(runId));
        }

        if (reconciliationResultId == Guid.Empty)
        {
            throw new ArgumentException(
                "Reconciliation result ID is required.",
                nameof(reconciliationResultId));
        }

        if (string.IsNullOrWhiteSpace(involvedSources))
        {
            throw new ArgumentException(
                "Involved sources are required.",
                nameof(involvedSources));
        }

        if (string.IsNullOrWhiteSpace(discrepancyDetail))
        {
            throw new ArgumentException(
                "Discrepancy detail is required.",
                nameof(discrepancyDetail));
        }

        Id = Guid.NewGuid();
        RunId = runId;
        ReconciliationResultId = reconciliationResultId;
        Category = category;
        InvolvedSources = involvedSources.Trim();
        DiscrepancyDetail = discrepancyDetail.Trim();
        CreatedAt = DateTime.UtcNow;
    }

    public void AddAiExplanation(
        string explanation,
        string? suggestedCategory)
    {
        if (string.IsNullOrWhiteSpace(explanation))
        {
            throw new ArgumentException(
                "AI explanation is required.",
                nameof(explanation));
        }

        AiExplanation = explanation.Trim();

        AiSuggestedCategory =
            string.IsNullOrWhiteSpace(suggestedCategory)
                ? null
                : suggestedCategory.Trim();

        AiExplanationGeneratedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}