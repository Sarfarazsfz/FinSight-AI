using System.Text.Json;
using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.DTOs.Reconciliation;

namespace FinSight.Application.AI;

public sealed class ExceptionDetailsTool
    : IExceptionDetailsTool
{
    public string Name =>
        "getExceptionDetails";

    private readonly IReconciliationExceptionRepository _exceptionRepository;
    private readonly IReconciliationResultRepository _resultRepository;
    private readonly INormalizedTransactionRepository _normalizedTransactionRepository;

    public ExceptionDetailsTool(
        IReconciliationExceptionRepository exceptionRepository,
        IReconciliationResultRepository resultRepository,
        INormalizedTransactionRepository normalizedTransactionRepository)
    {
        _exceptionRepository = exceptionRepository;
        _resultRepository = resultRepository;
        _normalizedTransactionRepository = normalizedTransactionRepository;
    }

    public async Task<FinanceToolResult> ExecuteAsync(
        FinanceToolRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ExceptionId is null ||
            request.ExceptionId == Guid.Empty)
        {
            return new FinanceToolResult
            {
                ToolName = Name,
                Success = false,
                ErrorCode = "INVALID_ARGUMENT",
                ErrorMessage = "A valid exceptionId is required."
            };
        }

        var exception =
            await _exceptionRepository.GetByIdAsync(
                request.ExceptionId.Value,
                cancellationToken);

        if (exception is null)
        {
            return new FinanceToolResult
            {
                ToolName = Name,
                Success = false,
                ErrorCode = "EXCEPTION_NOT_FOUND",
                ErrorMessage =
                    $"Reconciliation exception '{request.ExceptionId.Value}' was not found."
            };
        }

        var result =
            await _resultRepository.GetByIdAsync(
                exception.ReconciliationResultId,
                cancellationToken);

        if (result is null)
        {
            return new FinanceToolResult
            {
                ToolName = Name,
                Success = false,
                ErrorCode = "RESULT_NOT_FOUND",
                ErrorMessage =
                    $"Reconciliation result " +
                    $"'{exception.ReconciliationResultId}' was not found."
            };
        }

        var normalizedTransaction =
            await _normalizedTransactionRepository.GetByIdAsync(
                result.NormalizedTransactionId,
                cancellationToken);

        if (normalizedTransaction is null)
        {
            return new FinanceToolResult
            {
                ToolName = Name,
                Success = false,
                ErrorCode = "TRANSACTION_NOT_FOUND",
                ErrorMessage =
                    $"Normalized transaction " +
                    $"'{result.NormalizedTransactionId}' was not found."
            };
        }

        var response =
            new ReconciliationExceptionResponse
            {
                ExceptionId =
                    exception.Id,

                RunId =
                    exception.RunId,

                ReconciliationResultId =
                    exception.ReconciliationResultId,

                TransactionReference =
                    normalizedTransaction.TransactionReference,

                Category =
                    exception.Category.ToString(),

                InvolvedSources =
                    exception.InvolvedSources,

                DiscrepancyDetail =
                    exception.DiscrepancyDetail,

                AiExplanation =
                    exception.AiExplanation,

                AiSuggestedCategory =
                    exception.AiSuggestedCategory,

                AiExplanationGeneratedAt =
                    exception.AiExplanationGeneratedAt,

                CreatedAt =
                    exception.CreatedAt,

                UpdatedAt =
                    exception.UpdatedAt
            };

        var jsonOptions =
            new JsonSerializerOptions(
                JsonSerializerDefaults.Web);

        return new FinanceToolResult
        {
            ToolName = Name,
            Success = true,
            DataJson =
                JsonSerializer.Serialize(
                    response,
                    jsonOptions)
        };
    }
}
