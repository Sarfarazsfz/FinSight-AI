using FinSight.Application.Abstractions.Evaluation;
using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Reconciliation;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Ai;
using FinSight.Application.DTOs.Reconciliation;
using FinSight.Application.Evaluation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Controllers;

[ApiController]
[Route("api/reconciliation")]
[Authorize]
public class ReconciliationController : ControllerBase
{
    private readonly IReconciliationService
        _reconciliationService;

    private readonly IReconciliationRunRepository
        _runRepository;

    private readonly IReconciliationResultRepository
        _resultRepository;

    private readonly IReconciliationExceptionRepository
        _exceptionRepository;

    private readonly INormalizedTransactionRepository
        _normalizedTransactionRepository;

    private readonly IPaymentRecordRepository
        _paymentRecordRepository;

    private readonly IBankRecordRepository
        _bankRecordRepository;

    private readonly ISettlementRecordRepository
        _settlementRecordRepository;

    private readonly IAiExplanationService
        _aiExplanationService;

    private readonly IReconciliationSummaryBuilder
        _summaryBuilder;

    private readonly IGroundTruthComparisonService
        _groundTruthComparisonService;

    public ReconciliationController(
        IReconciliationService reconciliationService,
        IReconciliationRunRepository runRepository,
        IReconciliationResultRepository resultRepository,
        IReconciliationExceptionRepository exceptionRepository,
        INormalizedTransactionRepository normalizedTransactionRepository,
        IPaymentRecordRepository paymentRecordRepository,
        IBankRecordRepository bankRecordRepository,
        ISettlementRecordRepository settlementRecordRepository,
        IAiExplanationService aiExplanationService,
        IReconciliationSummaryBuilder summaryBuilder,
        IGroundTruthComparisonService groundTruthComparisonService)
    {
        _reconciliationService =
            reconciliationService;

        _runRepository =
            runRepository;

        _resultRepository =
            resultRepository;

        _exceptionRepository =
            exceptionRepository;

        _normalizedTransactionRepository =
            normalizedTransactionRepository;

        _paymentRecordRepository =
            paymentRecordRepository;

        _bankRecordRepository =
            bankRecordRepository;

        _settlementRecordRepository =
            settlementRecordRepository;

        _aiExplanationService =
            aiExplanationService;

        _summaryBuilder =
            summaryBuilder;

        _groundTruthComparisonService =
            groundTruthComparisonService;
    }

    [HttpPost("runs")]
    [ProducesResponseType(
        typeof(ReconciliationRunResult),
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReconciliationRunResult>> CreateRun(
        [FromBody] ReconciliationRunRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null ||
            request.BatchId == Guid.Empty)
        {
            return Problem(
                detail: "A valid batchId is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        try
        {
            var result =
                await _reconciliationService.ExecuteAsync(
                    request,
                    cancellationToken);

            return StatusCode(
                StatusCodes.Status201Created,
                result);
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound,
                title: "Resource Not Found");
        }
    }

    [HttpGet("runs/{runId:guid}")]
    [ProducesResponseType(
        typeof(ReconciliationRunDetailsResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReconciliationRunDetailsResponse>> GetRun(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var run =
            await _runRepository.GetByIdAsync(
                runId,
                cancellationToken);

        if (run is null)
        {
            return Problem(
                detail: $"Reconciliation run '{runId}' was not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Resource Not Found");
        }

        var response =
            new ReconciliationRunDetailsResponse
            {
                RunId = run.Id,
                BatchId = run.BatchId,
                Status = run.Status.ToString(),
                TotalReconciliationUnits =
                    run.TotalReconciliationUnits,
                MatchRate = run.MatchRate,
                StartedAt = run.StartedAt,
                CompletedAt = run.CompletedAt,
                CreatedAt = run.CreatedAt
            };

        return Ok(response);
    }

    [HttpGet("runs/{runId:guid}/summary")]
    [ProducesResponseType(
        typeof(ReconciliationRunSummaryResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReconciliationRunSummaryResponse>>
        GetSummary(
            Guid runId,
            CancellationToken cancellationToken)
    {
        // Single authoritative calculation, shared with the Finance
        // Assistant's getReconciliationSummary tool -- see
        // IReconciliationSummaryBuilder.
        var response =
            await _summaryBuilder.BuildAsync(
                runId,
                cancellationToken);

        if (response is null)
        {
            return Problem(
                detail: $"Reconciliation run '{runId}' was not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Resource Not Found");
        }

        return Ok(response);
    }

    [HttpPost("runs/{runId:guid}/ground-truth-verification")]
    [ProducesResponseType(
        typeof(GroundTruthComparisonResult),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GroundTruthComparisonResult>>
        VerifyGroundTruth(
            Guid runId,
            [FromBody] GroundTruthRow[]? groundTruthRows,
            CancellationToken cancellationToken)
    {
        if (groundTruthRows is null ||
            groundTruthRows.Length == 0)
        {
            return Problem(
                detail: "A non-empty ground-truth row array is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        var run =
            await _runRepository.GetByIdAsync(
                runId,
                cancellationToken);

        if (run is null)
        {
            return Problem(
                detail: $"Reconciliation run '{runId}' was not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Resource Not Found");
        }

        var comparison =
            await _groundTruthComparisonService.CompareAsync(
                runId,
                groundTruthRows,
                cancellationToken);

        return Ok(comparison);
    }

    [HttpGet("runs/{runId:guid}/results")]
    [ProducesResponseType(
        typeof(PagedResponse<ReconciliationResultResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<
        ActionResult<PagedResponse<ReconciliationResultResponse>>>
        GetResults(
            Guid runId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken cancellationToken = default)
    {
        const int maxPageSize = 100;

        if (pageNumber < 1)
        {
            return Problem(
                detail: "pageNumber must be greater than or equal to 1.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        if (pageSize < 1 || pageSize > maxPageSize)
        {
            return Problem(
                detail: $"pageSize must be between 1 and {maxPageSize}.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        var run =
            await _runRepository.GetByIdAsync(
                runId,
                cancellationToken);

        if (run is null)
        {
            return Problem(
                detail: $"Reconciliation run '{runId}' was not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Resource Not Found");
        }

        var pagedResults =
            await _resultRepository.GetPageByRunIdAsync(
                runId,
                pageNumber,
                pageSize,
                cancellationToken);

        var normalizedTransactions =
            await _normalizedTransactionRepository.GetByRunIdAsync(
                runId,
                cancellationToken);

        var transactionReferenceById =
            normalizedTransactions.ToDictionary(
                x => x.Id,
                x => x.TransactionReference);

        var items =
            pagedResults.Items
                .Select(
                    result =>
                    {
                        transactionReferenceById.TryGetValue(
                            result.NormalizedTransactionId,
                            out var transactionReference);

                        return new ReconciliationResultResponse
                        {
                            ResultId =
                                result.Id,

                            RunId =
                                result.RunId,

                            NormalizedTransactionId =
                                result.NormalizedTransactionId,

                            TransactionReference =
                                transactionReference ??
                                string.Empty,

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

        var totalPages =
            pagedResults.TotalCount == 0
                ? 0
                : (int)Math.Ceiling(
                    pagedResults.TotalCount /
                    (double)pageSize);

        var response =
            new PagedResponse<ReconciliationResultResponse>
            {
                Items =
                    items,

                PageNumber =
                    pageNumber,

                PageSize =
                    pageSize,

                TotalCount =
                    pagedResults.TotalCount,

                TotalPages =
                    totalPages
            };

        return Ok(response);
    }

    [HttpGet(
        "runs/{runId:guid}/results/{resultId:guid}")]
    [ProducesResponseType(
        typeof(ReconciliationTransactionDetailResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReconciliationTransactionDetailResponse>>
        GetTransactionDetail(
            Guid runId,
            Guid resultId,
            CancellationToken cancellationToken)
    {
        var run =
            await _runRepository.GetByIdAsync(
                runId,
                cancellationToken);

        if (run is null)
        {
            return Problem(
                detail: $"Reconciliation run '{runId}' was not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Resource Not Found");
        }

        var result =
            await _resultRepository.GetByIdAsync(
                resultId,
                cancellationToken);

        if (result is null ||
            result.RunId != runId)
        {
            return Problem(
                detail:
                    $"Reconciliation result '{resultId}' was not found " +
                    $"for run '{runId}'.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Resource Not Found");
        }

        var normalizedTransaction =
            await _normalizedTransactionRepository.GetByIdAsync(
                result.NormalizedTransactionId,
                cancellationToken);

        if (normalizedTransaction is null ||
            normalizedTransaction.RunId != runId)
        {
            return Problem(
                detail:
                    $"Normalized transaction " +
                    $"'{result.NormalizedTransactionId}' " +
                    "was not found for this run.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Resource Not Found");
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

        return Ok(response);
    }

    [HttpGet("runs/{runId:guid}/exceptions")]
    [ProducesResponseType(
        typeof(PagedResponse<ReconciliationExceptionResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<
        ActionResult<PagedResponse<ReconciliationExceptionResponse>>>
        GetExceptions(
            Guid runId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken cancellationToken = default)
    {
        const int maxPageSize = 100;

        if (pageNumber < 1)
        {
            return Problem(
                detail: "pageNumber must be greater than or equal to 1.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        if (pageSize < 1 || pageSize > maxPageSize)
        {
            return Problem(
                detail: $"pageSize must be between 1 and {maxPageSize}.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        var run =
            await _runRepository.GetByIdAsync(
                runId,
                cancellationToken);

        if (run is null)
        {
            return Problem(
                detail: $"Reconciliation run '{runId}' was not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Resource Not Found");
        }

        var pagedExceptions =
            await _exceptionRepository.GetPageByRunIdAsync(
                runId,
                pageNumber,
                pageSize,
                cancellationToken);

        var results =
            await _resultRepository.GetByRunIdAsync(
                runId,
                cancellationToken);

        var normalizedTransactions =
            await _normalizedTransactionRepository.GetByRunIdAsync(
                runId,
                cancellationToken);

        var normalizedReferenceById =
            normalizedTransactions.ToDictionary(
                x => x.Id,
                x => x.TransactionReference);

        var normalizedTransactionIdByResultId =
            results.ToDictionary(
                x => x.Id,
                x => x.NormalizedTransactionId);

        var items =
            pagedExceptions.Items
                .Select(
                    exception =>
                    {
                        var transactionReference =
                            string.Empty;

                        if (
                            normalizedTransactionIdByResultId.TryGetValue(
                                exception.ReconciliationResultId,
                                out var normalizedTransactionId) &&
                            normalizedReferenceById.TryGetValue(
                                normalizedTransactionId,
                                out var reference))
                        {
                            transactionReference =
                                reference;
                        }

                        return new ReconciliationExceptionResponse
                        {
                            ExceptionId =
                                exception.Id,

                            RunId =
                                exception.RunId,

                            ReconciliationResultId =
                                exception.ReconciliationResultId,

                            TransactionReference =
                                transactionReference,

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
                    })
                .ToList();

        var totalPages =
            pagedExceptions.TotalCount == 0
                ? 0
                : (int)Math.Ceiling(
                    pagedExceptions.TotalCount /
                    (double)pageSize);

        var response =
            new PagedResponse<ReconciliationExceptionResponse>
            {
                Items =
                    items,

                PageNumber =
                    pageNumber,

                PageSize =
                    pageSize,

                TotalCount =
                    pagedExceptions.TotalCount,

                TotalPages =
                    totalPages
            };

        return Ok(response);
    }

    [HttpGet("exceptions/{exceptionId:guid}")]
    [ProducesResponseType(
        typeof(ReconciliationExceptionResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReconciliationExceptionResponse>>
        GetException(
            Guid exceptionId,
            CancellationToken cancellationToken)
    {
        var exception =
            await _exceptionRepository.GetByIdAsync(
                exceptionId,
                cancellationToken);

        if (exception is null)
        {
            return Problem(
                detail:
                    $"Reconciliation exception '{exceptionId}' was not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Resource Not Found");
        }

        var result =
            await _resultRepository.GetByIdAsync(
                exception.ReconciliationResultId,
                cancellationToken);

        if (result is null)
        {
            return Problem(
                detail:
                    $"Reconciliation result " +
                    $"'{exception.ReconciliationResultId}' " +
                    "was not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Resource Not Found");
        }

        var normalizedTransaction =
            await _normalizedTransactionRepository.GetByIdAsync(
                result.NormalizedTransactionId,
                cancellationToken);

        if (normalizedTransaction is null)
        {
            return Problem(
                detail:
                    $"Normalized transaction " +
                    $"'{result.NormalizedTransactionId}' " +
                    "was not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Resource Not Found");
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

        return Ok(response);
    }

    [HttpPost(
        "exceptions/{exceptionId:guid}/ai-explanation")]
    [ProducesResponseType(
        typeof(AiExplanationResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AiExplanationResponse>>
        GenerateAiExplanation(
            Guid exceptionId,
            CancellationToken cancellationToken)
    {
        if (exceptionId == Guid.Empty)
        {
            return Problem(
                detail: "A valid exceptionId is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        try
        {
            var response =
                await _aiExplanationService.ExplainAsync(
                    exceptionId,
                    cancellationToken);

            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound,
                title: "Resource Not Found");
        }
        catch (ArgumentException ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }
    }
}
