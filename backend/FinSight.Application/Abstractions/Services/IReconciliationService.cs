using FinSight.Application.DTOs.Reconciliation;

namespace FinSight.Application.Abstractions.Services;

public interface IReconciliationService
{
    Task<ReconciliationRunResult> ExecuteAsync(
        ReconciliationRunRequest request,
        CancellationToken cancellationToken = default);
}