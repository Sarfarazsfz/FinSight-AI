using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Reconciliation;
using FinSight.Application.DTOs.Reconciliation;

namespace FinSight.Application.Reconciliation;

public sealed class ReconciliationSummaryBuilder
    : IReconciliationSummaryBuilder
{
    private readonly IReconciliationRunRepository _runRepository;
    private readonly IReconciliationResultRepository _resultRepository;
    private readonly IReconciliationExceptionRepository _exceptionRepository;

    public ReconciliationSummaryBuilder(
        IReconciliationRunRepository runRepository,
        IReconciliationResultRepository resultRepository,
        IReconciliationExceptionRepository exceptionRepository)
    {
        _runRepository = runRepository;
        _resultRepository = resultRepository;
        _exceptionRepository = exceptionRepository;
    }

    public async Task<ReconciliationRunSummaryResponse?> BuildAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var run =
            await _runRepository.GetByIdAsync(
                runId,
                cancellationToken);

        if (run is null)
        {
            return null;
        }

        var results =
            await _resultRepository.GetByRunIdAsync(
                runId,
                cancellationToken);

        var exceptions =
            await _exceptionRepository.GetByRunIdAsync(
                runId,
                cancellationToken);

        return new ReconciliationRunSummaryResponse
        {
            RunId = run.Id,
            BatchId = run.BatchId,
            Status = run.Status.ToString(),
            TotalUnits = results.Count,
            Matched =
                results.Count(
                    x => x.Status.ToString() == "Matched"),
            Mismatched =
                results.Count(
                    x => x.Status.ToString() == "Mismatched"),
            Missing =
                results.Count(
                    x => x.Status.ToString() == "Missing"),
            Duplicate =
                results.Count(
                    x => x.Status.ToString() == "Duplicate"),
            Unresolved =
                results.Count(
                    x => x.Status.ToString() == "Unresolved"),
            MatchRate =
                run.MatchRate ?? 0.00m,
            ExceptionCount =
                exceptions.Count
        };
    }
}
