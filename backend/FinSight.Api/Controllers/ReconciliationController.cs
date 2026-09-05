using FinSight.Api.Authentication;
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

    private readonly ICurrentUserService _currentUserService;
    private readonly IBatchAccessService _batchAccessService;
    private readonly IAuditLogReader _auditLogReader;

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
        IGroundTruthComparisonService groundTruthComparisonService,
        ICurrentUserService currentUserService,
        IBatchAccessService batchAccessService,
        IAuditLogReader auditLogReader)
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

        _currentUserService =
            currentUserService;

        _batchAccessService =
            batchAccessService;

        _auditLogReader =
            auditLogReader;
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

        if (!_currentUserService.TryGetCurrentUserId(out var currentUserId))
        {
            return Problem(
                detail: "Authentication is required.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized");
        }

        // Ownership is checked here, before the reconciliation engine
        // ever runs -- not inside it. A batch that is not the caller's
        // (or does not exist) is reported with the exact same message
        // ReconciliationOrchestrator itself uses for "does not exist",
        // so the two cases stay indistinguishable.
        var ownedBatch =
            await _batchAccessService.GetOwnedBatchAsync(
                request.BatchId,
                currentUserId,
                cancellationToken);

        if (ownedBatch is null)
        {
            return Problem(
                detail: $"Batch '{request.BatchId}' was not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Resource Not Found");
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
        if (!_currentUserService.TryGetCurrentUserId(out var currentUserId))
        {
            return Problem(
                detail: "Authentication is required.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized");
        }

        var run =
            await _batchAccessService.GetOwnedRunAsync(
                runId,
                currentUserId,
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
        if (!_currentUserService.TryGetCurrentUserId(out var currentUserId))
        {
            return Problem(
                detail: "Authentication is required.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized");
        }

        // IReconciliationSummaryBuilder is shared with the Finance
        // Assistant's internal getReconciliationSummary tool, which has
        // no per-caller identity of its own to check against -- ownership
        // is therefore verified here, at the HTTP boundary, rather than
        // inside the shared builder.
        var ownedRun =
            await _batchAccessService.GetOwnedRunAsync(
                runId,
                currentUserId,
                cancellationToken);

        if (ownedRun is null)
        {
            return Problem(
                detail: $"Reconciliation run '{runId}' was not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Resource Not Found");
        }

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

        if (!_currentUserService.TryGetCurrentUserId(out var currentUserId))
        {
            return Problem(
                detail: "Authentication is required.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized");
        }

        // Ownership is established before the comparison runs at all --
        // an authenticated user must not be able to verify a run they do
        // not own, regardless of which ground-truth labels they supply.
        // Nothing about the comparison itself (expected/actual counts,
        // match-rate check, failure generation) changes here.
        var run =
            await _batchAccessService.GetOwnedRunAsync(
                runId,
                currentUserId,
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

        if (!_currentUserService.TryGetCurrentUserId(out var currentUserId))
        {
            return Problem(
                detail: "Authentication is required.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized");
        }

        var run =
            await _batchAccessService.GetOwnedRunAsync(
                runId,
                currentUserId,
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
        if (!_currentUserService.TryGetCurrentUserId(out var currentUserId))
        {
            return Problem(
                detail: "Authentication is required.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized");
        }

        var run =
            await _batchAccessService.GetOwnedRunAsync(
                runId,
                currentUserId,
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

        if (!_currentUserService.TryGetCurrentUserId(out var currentUserId))
        {
            return Problem(
                detail: "Authentication is required.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized");
        }

        var run =
            await _batchAccessService.GetOwnedRunAsync(
                runId,
                currentUserId,
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

    /// <summary>
    /// Read-only audit evidence for a run, from the existing audit_logs
    /// table -- the same store BatchIngestionService,
    /// ReconciliationOrchestrator, AiExplanationService and
    /// FinanceAssistantService already write to. There is no
    /// corresponding create/update/delete action anywhere in this API:
    /// this endpoint cannot produce, alter, or remove a single audit row.
    ///
    /// This is evidence ABOUT the run's execution -- timing, throughput,
    /// which events fired, in what order -- never a second source of
    /// financial truth. Match status, match rate, exception counts and
    /// classification remain whatever the deterministic reconciliation
    /// engine and Ground Truth Verification say they are.
    /// </summary>
    [HttpGet("runs/{runId:guid}/audit")]
    [ProducesResponseType(
        typeof(PagedResponse<AuditLogEntryResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<AuditLogEntryResponse>>>
        GetAuditLog(
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

        if (!_currentUserService.TryGetCurrentUserId(out var currentUserId))
        {
            return Problem(
                detail: "Authentication is required.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized");
        }

        // Ownership resolved and enforced before a single audit row is
        // read -- identical mechanism and identical 404 message to every
        // other run-scoped endpoint, so a caller cannot tell "this run
        // does not exist" apart from "this run exists, but isn't yours."
        var run =
            await _batchAccessService.GetOwnedRunAsync(
                runId,
                currentUserId,
                cancellationToken);

        if (run is null)
        {
            return Problem(
                detail: $"Reconciliation run '{runId}' was not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Resource Not Found");
        }

        var pagedAuditLogs =
            await _auditLogReader.GetPageByRunIdAsync(
                runId,
                pageNumber,
                pageSize,
                cancellationToken);

        var items =
            pagedAuditLogs.Items
                .Select(
                    auditLog =>
                        new AuditLogEntryResponse
                        {
                            Id =
                                auditLog.Id,

                            OccurredAt =
                                auditLog.OccurredAt,

                            EventType =
                                auditLog.EventType.ToString(),

                            RunId =
                                auditLog.RunId,

                            RelatedEntityType =
                                auditLog.RelatedEntityType,

                            RelatedEntityId =
                                auditLog.RelatedEntityId,

                            Detail =
                                auditLog.DetailPayload
                        })
                .ToList();

        var totalPages =
            pagedAuditLogs.TotalCount == 0
                ? 0
                : (int)Math.Ceiling(
                    pagedAuditLogs.TotalCount /
                    (double)pageSize);

        var response =
            new PagedResponse<AuditLogEntryResponse>
            {
                Items =
                    items,

                PageNumber =
                    pageNumber,

                PageSize =
                    pageSize,

                TotalCount =
                    pagedAuditLogs.TotalCount,

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
        if (!_currentUserService.TryGetCurrentUserId(out var currentUserId))
        {
            return Problem(
                detail: "Authentication is required.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized");
        }

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

        // This route deliberately takes no runId, so ownership cannot be
        // checked before the lookup the way every other endpoint does it
        // -- the exception itself must be fetched first to learn which
        // run it belongs to. The 404 message is identical to the
        // not-found case above: a caller must not be able to tell "this
        // exception does not exist" apart from "this exception exists,
        // but not for you."
        var ownedRun =
            await _batchAccessService.GetOwnedRunAsync(
                exception.RunId,
                currentUserId,
                cancellationToken);

        if (ownedRun is null)
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

        if (!_currentUserService.TryGetCurrentUserId(out var currentUserId))
        {
            return Problem(
                detail: "Authentication is required.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized");
        }

        // AI must stay completely outside the truth path, which includes
        // never being invoked on another user's data. The exception is
        // resolved to its run and ownership is checked BEFORE
        // IAiExplanationService is called at all -- if the exception
        // genuinely does not exist, that is left to the service's own
        // KeyNotFoundException below, unchanged.
        var exception =
            await _exceptionRepository.GetByIdAsync(
                exceptionId,
                cancellationToken);

        if (exception is not null)
        {
            var ownedRun =
                await _batchAccessService.GetOwnedRunAsync(
                    exception.RunId,
                    currentUserId,
                    cancellationToken);

            if (ownedRun is null)
            {
                return Problem(
                    detail:
                        $"Reconciliation exception '{exceptionId}' was not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Resource Not Found");
            }
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
