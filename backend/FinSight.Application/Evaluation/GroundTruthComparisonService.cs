using FinSight.Application.Abstractions.Evaluation;
using FinSight.Application.Abstractions.Persistence;

namespace FinSight.Application.Evaluation;

public sealed class GroundTruthComparisonService
    : IGroundTruthComparisonService
{
    private readonly IReconciliationResultRepository _resultRepository;

    private readonly IReconciliationExceptionRepository
        _exceptionRepository;

    private readonly INormalizedTransactionRepository
        _normalizedTransactionRepository;

    public GroundTruthComparisonService(
        IReconciliationResultRepository resultRepository,
        IReconciliationExceptionRepository exceptionRepository,
        INormalizedTransactionRepository normalizedTransactionRepository)
    {
        _resultRepository = resultRepository;
        _exceptionRepository = exceptionRepository;
        _normalizedTransactionRepository = normalizedTransactionRepository;
    }

    public async Task<GroundTruthComparisonResult> CompareAsync(
        Guid runId,
        IReadOnlyList<GroundTruthRow> expectedRows,
        CancellationToken cancellationToken = default)
    {
        // Same mapping technique already proven in
        // GroundTruthEndToEndIntegrationTests: build the comparator's
        // independent wire-shaped types from the real persisted rows.
        var normalizedTransactions =
            await _normalizedTransactionRepository.GetByRunIdAsync(
                runId,
                cancellationToken);

        var referenceByTransactionId =
            normalizedTransactions.ToDictionary(
                x => x.Id,
                x => x.TransactionReference);

        var persistedResults =
            await _resultRepository.GetByRunIdAsync(
                runId,
                cancellationToken);

        var actualResults =
            persistedResults
                .Select(
                    x => new ActualResult
                    {
                        ResultId = x.Id,
                        RunId = x.RunId,
                        NormalizedTransactionId =
                            x.NormalizedTransactionId,
                        TransactionReference =
                            referenceByTransactionId[
                                x.NormalizedTransactionId],
                        Status = x.Status.ToString(),
                        StrategyUsed = x.StrategyUsed,
                        ReasonCode = x.ReasonCode.ToString(),
                        CreatedAt = x.CreatedAt
                    })
                .ToList();

        var resultById =
            persistedResults.ToDictionary(x => x.Id);

        var persistedExceptions =
            await _exceptionRepository.GetByRunIdAsync(
                runId,
                cancellationToken);

        var actualExceptions =
            persistedExceptions
                .Select(
                    x => new ActualException
                    {
                        ExceptionId = x.Id,
                        RunId = x.RunId,
                        ReconciliationResultId =
                            x.ReconciliationResultId,
                        Category = x.Category.ToString(),
                        TransactionReference =
                            referenceByTransactionId[
                                resultById[x.ReconciliationResultId]
                                    .NormalizedTransactionId]
                    })
                .ToList();

        return GroundTruthComparer.Compare(
            expectedRows,
            actualResults,
            actualExceptions);
    }
}
