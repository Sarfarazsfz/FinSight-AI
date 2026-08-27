using FinSight.Domain.Enums;

namespace FinSight.Application.DTOs.Reconciliation;

public sealed class ReconciliationRunDetailsResponse
{
    public Guid RunId { get; init; }

    public Guid BatchId { get; init; }

    public string Status { get; init; } = string.Empty;

    public int TotalReconciliationUnits { get; init; }

    public decimal? MatchRate { get; init; }

    public DateTime StartedAt { get; init; }

    public DateTime? CompletedAt { get; init; }

    public DateTime CreatedAt { get; init; }
}