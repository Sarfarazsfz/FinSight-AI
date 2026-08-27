using System.Text.Json;
using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.DTOs.Reconciliation;

namespace FinSight.Application.AI;

public sealed class TransactionDetailsTool
    : ITransactionDetailsTool
{
    public string Name =>
        "getTransactionDetails";

    private readonly IReconciliationRunRepository _runRepository;
    private readonly IReconciliationResultRepository _resultRepository;
    private readonly INormalizedTransactionRepository _normalizedTransactionRepository;
    private readonly IPaymentRecordRepository _paymentRecordRepository;
    private readonly IBankRecordRepository _bankRecordRepository;
    private readonly ISettlementRecordRepository _settlementRecordRepository;

    public TransactionDetailsTool(
        IReconciliationRunRepository runRepository,
        IReconciliationResultRepository resultRepository,
        INormalizedTransactionRepository normalizedTransactionRepository,
        IPaymentRecordRepository paymentRecordRepository,
        IBankRecordRepository bankRecordRepository,
        ISettlementRecordRepository settlementRecordRepository)
    {
        _runRepository = runRepository;
        _resultRepository = resultRepository;
        _normalizedTransactionRepository = normalizedTransactionRepository;
        _paymentRecordRepository = paymentRecordRepository;
        _bankRecordRepository = bankRecordRepository;
        _settlementRecordRepository = settlementRecordRepository;
    }

    public async Task<FinanceToolResult> ExecuteAsync(
        FinanceToolRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.RunId is null ||
            request.RunId == Guid.Empty ||
            request.ResultId is null ||
            request.ResultId == Guid.Empty)
        {
            return new FinanceToolResult
            {
                ToolName = Name,
                Success = false,
                ErrorCode = "INVALID_ARGUMENT",
                ErrorMessage =
                    "Valid runId and resultId are required."
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

        var result =
            await _resultRepository.GetByIdAsync(
                request.ResultId.Value,
                cancellationToken);

        if (result is null ||
            result.RunId != request.RunId.Value)
        {
            return new FinanceToolResult
            {
                ToolName = Name,
                Success = false,
                ErrorCode = "RESULT_NOT_FOUND",
                ErrorMessage =
                    $"Reconciliation result '{request.ResultId.Value}' " +
                    $"was not found for run '{request.RunId.Value}'."
            };
        }

        var normalizedTransaction =
            await _normalizedTransactionRepository.GetByIdAsync(
                result.NormalizedTransactionId,
                cancellationToken);

        if (normalizedTransaction is null ||
            normalizedTransaction.RunId != request.RunId.Value)
        {
            return new FinanceToolResult
            {
                ToolName = Name,
                Success = false,
                ErrorCode = "TRANSACTION_NOT_FOUND",
                ErrorMessage =
                    $"Normalized transaction " +
                    $"'{result.NormalizedTransactionId}' " +
                    "was not found for this run."
            };
        }

        var transactionReference =
            normalizedTransaction.TransactionReference;

        var payments =
            await _paymentRecordRepository.GetByBatchIdAsync(
                run.BatchId,
                cancellationToken);

        var banks =
            await _bankRecordRepository.GetByBatchIdAsync(
                run.BatchId,
                cancellationToken);

        var settlements =
            await _settlementRecordRepository.GetByBatchIdAsync(
                run.BatchId,
                cancellationToken);

        var paymentDetails =
            payments
                .Where(
                    x => string.Equals(
                        x.TransactionReference,
                        transactionReference,
                        StringComparison.Ordinal))
                .Select(
                    x => new SourceTransactionRecordResponse
                    {
                        Id = x.Id,
                        SourceRecordIdentifier =
                            x.SourceRecordIdentifier,
                        TransactionReference =
                            x.TransactionReference,
                        Amount = x.Amount,
                        Currency = x.Currency,
                        TransactionDate =
                            x.TransactionDate,
                        Status = x.Status,
                        CreatedAt = x.CreatedAt
                    })
                .ToList();

        var bankDetails =
            banks
                .Where(
                    x => string.Equals(
                        x.TransactionReference,
                        transactionReference,
                        StringComparison.Ordinal))
                .Select(
                    x => new SourceTransactionRecordResponse
                    {
                        Id = x.Id,
                        SourceRecordIdentifier =
                            x.SourceRecordIdentifier,
                        TransactionReference =
                            x.TransactionReference,
                        Amount = x.Amount,
                        Currency = x.Currency,
                        TransactionDate =
                            x.TransactionDate,
                        Status = x.Status,
                        CreatedAt = x.CreatedAt
                    })
                .ToList();

        var settlementDetails =
            settlements
                .Where(
                    x => string.Equals(
                        x.TransactionReference,
                        transactionReference,
                        StringComparison.Ordinal))
                .Select(
                    x => new SourceTransactionRecordResponse
                    {
                        Id = x.Id,
                        SourceRecordIdentifier =
                            x.SourceRecordIdentifier,
                        TransactionReference =
                            x.TransactionReference,
                        Amount = x.Amount,
                        Currency = x.Currency,
                        TransactionDate =
                            x.TransactionDate,
                        Status = x.Status,
                        CreatedAt = x.CreatedAt
                    })
                .ToList();

        var response =
            new ReconciliationTransactionDetailResponse
            {
                ResultId =
                    result.Id,

                RunId =
                    result.RunId,

                NormalizedTransactionId =
                    result.NormalizedTransactionId,

                TransactionReference =
                    transactionReference,

                Status =
                    result.Status.ToString(),

                StrategyUsed =
                    result.StrategyUsed,

                ReasonCode =
                    result.ReasonCode.ToString(),

                Payments =
                    paymentDetails,

                Banks =
                    bankDetails,

                Settlements =
                    settlementDetails
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
