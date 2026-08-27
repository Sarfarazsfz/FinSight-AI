using FinSight.Domain.Enums;

namespace FinSight.Domain.Entities;

public class ReconciliationRun
{
    public Guid Id { get; private set; }

    public Guid BatchId { get; private set; }

    public ReconciliationRunStatus Status { get; private set; }

    public int TotalReconciliationUnits { get; private set; }

    public decimal? MatchRate { get; private set; }

    public DateTime StartedAt { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private ReconciliationRun()
    {
    }

    public ReconciliationRun(Guid batchId)
    {
        if (batchId == Guid.Empty)
        {
            throw new ArgumentException(
                "Batch ID is required.",
                nameof(batchId));
        }

        Id = Guid.NewGuid();
        BatchId = batchId;
        Status = ReconciliationRunStatus.Pending;
        TotalReconciliationUnits = 0;
        MatchRate = null;
        StartedAt = DateTime.UtcNow;
        CompletedAt = null;
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkRunning()
    {
        EnsureStatus(
            ReconciliationRunStatus.Pending,
            nameof(MarkRunning));

        Status = ReconciliationRunStatus.Running;
    }

    public void Complete(
        int totalReconciliationUnits,
        decimal matchRate)
    {
        EnsureStatus(
            ReconciliationRunStatus.Running,
            nameof(Complete));

        if (totalReconciliationUnits < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalReconciliationUnits),
                "Total reconciliation units cannot be negative.");
        }

        if (matchRate < 0 || matchRate > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(matchRate),
                "Match rate must be between 0 and 100.");
        }

        TotalReconciliationUnits =
            totalReconciliationUnits;

        MatchRate =
            decimal.Round(matchRate, 2);

        Status =
            ReconciliationRunStatus.Completed;

        CompletedAt =
            DateTime.UtcNow;
    }

    public void Fail()
    {
        EnsureStatus(
            ReconciliationRunStatus.Running,
            nameof(Fail));

        Status =
            ReconciliationRunStatus.Failed;

        CompletedAt =
            DateTime.UtcNow;
    }

    private void EnsureStatus(
        ReconciliationRunStatus expectedStatus,
        string operation)
    {
        if (Status != expectedStatus)
        {
            throw new InvalidOperationException(
                $"Cannot perform '{operation}' when " +
                $"reconciliation run is in status " +
                $"'{Status}'. Expected status " +
                $"'{expectedStatus}'.");
        }
    }
}