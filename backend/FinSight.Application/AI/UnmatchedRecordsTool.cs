using System.Text.Json;
using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.DTOs.Reconciliation;
using FinSight.Domain.Enums;

namespace FinSight.Application.AI;

public sealed class UnmatchedRecordsTool
    : IUnmatchedRecordsTool
{
    public string Name =>
        "getUnmatchedRecords";

    private readonly IReconciliationRunRepository _runRepository;
    private readonly IReconciliationResultRepository _resultRepository;
    private readonly INormalizedTransactionRepository _normalizedTransactionRepository;

    public UnmatchedRecordsTool(
        IReconciliationRunRepository runRepository,
        IReconciliationResultRepository resultRepository,
        INormalizedTransactionRepository normalizedTransactionRepository)
    {
        _runRepository = runRepository;
        _resultRepository = resultRepository;
        _normalizedTransactionRepository = normalizedTransactionRepository;
    }

    public async Task<FinanceToolResult> ExecuteAsync(
        FinanceToolRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.RunId is null ||
            request.RunId == Guid.Empty)
        {
            return new FinanceToolResult
            {
                ToolName = Name,
                Success = false,
                ErrorCode = "INVALID_ARGUMENT",
                ErrorMessage = "A valid runId is required."
            };
        }

        var run =
            await _runRepository.GetByIdAsync(
                request.RunId.Value,
                cancellationToken);

        if (run is null)
        {
            return new FinanceToolResult
            {
                ToolName = Name,
                Success = false,
                ErrorCode = "RUN_NOT_FOUND",
                ErrorMessage =
                    $"Reconciliation run '{request.RunId.Value}' was not found."
            };
        }

        var results =
            await _resultRepository.GetByRunIdAsync(
                request.RunId.Value,
                cancellationToken);

        var normalizedTransactions =
            await _normalizedTransactionRepository.GetByRunIdAsync(
                request.RunId.Value,
                cancellationToken);

        var transactionReferenceById =
            normalizedTransactions.ToDictionary(
                x => x.Id,
                x => x.TransactionReference);

        var unmatched =
            results
                .Where(x => x.Status != MatchStatus.Matched)
                .Select(
                    result =>
                    {
                        transactionReferenceById.TryGetValue(
                            result.NormalizedTransactionId,
                            out var transactionReference);

                        return new ReconciliationResultResponse
                        {
                            ResultId = result.Id,
                            RunId = result.RunId,
                            NormalizedTransactionId =
                                result.NormalizedTransactionId,
                            TransactionReference =
                                transactionReference ?? string.Empty,
                            Status =
                                result.Status.ToString(),
                            StrategyUsed =
                                result.StrategyUsed,
                            ReasonCode =
                                result.ReasonCode.ToString(),
                            CreatedAt =
                                result.CreatedAt
                        };
                    })
                .ToList();

        var jsonOptions =
            new JsonSerializerOptions(
                JsonSerializerDefaults.Web);

        return new FinanceToolResult
        {
            ToolName = Name,
            Success = true,
            DataJson =
                JsonSerializer.Serialize(
                    new
                    {
                        runId = run.Id,
                        totalUnmatched = unmatched.Count,
                        items = unmatched
                    },
                    jsonOptions)
        };
    }
}
