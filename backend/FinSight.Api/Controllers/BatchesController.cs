using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Ingestion;
using FinSight.Application.DTOs.Reconciliation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Controllers;

[ApiController]
[Route("api/batches")]
[Authorize]
public class BatchesController : ControllerBase
{
    private readonly IBatchIngestionService _batchIngestionService;
    private readonly IBatchRepository _batchRepository;

    public BatchesController(
        IBatchIngestionService batchIngestionService,
        IBatchRepository batchRepository)
    {
        _batchIngestionService = batchIngestionService;
        _batchRepository = batchRepository;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(
        typeof(BatchIngestionResult),
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BatchIngestionResult>> CreateBatch(
        [FromForm] string batchLabel,
        [FromForm] string createdBy,
        [FromForm] IFormFile paymentsFile,
        [FromForm] IFormFile bankFile,
        [FromForm] IFormFile settlementsFile,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(batchLabel))
        {
            return Problem(
                detail: "batchLabel is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        if (string.IsNullOrWhiteSpace(createdBy))
        {
            return Problem(
                detail: "createdBy is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        if (paymentsFile is null || paymentsFile.Length == 0)
        {
            return Problem(
                detail: "paymentsFile is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        if (bankFile is null || bankFile.Length == 0)
        {
            return Problem(
                detail: "bankFile is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        if (settlementsFile is null || settlementsFile.Length == 0)
        {
            return Problem(
                detail: "settlementsFile is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        await using var paymentStream =
            paymentsFile.OpenReadStream();

        await using var bankStream =
            bankFile.OpenReadStream();

        await using var settlementStream =
            settlementsFile.OpenReadStream();

        var request = new BatchIngestionRequest
        {
            BatchLabel = batchLabel.Trim(),
            CreatedBy = createdBy.Trim(),
            PaymentFile = paymentStream,
            BankFile = bankStream,
            SettlementFile = settlementStream
        };

        try
        {
            var result =
                await _batchIngestionService.IngestAsync(
                    request,
                    cancellationToken);

            return StatusCode(
                StatusCodes.Status201Created,
                result);
        }
        catch (InvalidDataException ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(PagedResponse<BatchResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<BatchResponse>>> GetBatches(
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

        var page =
            await _batchRepository.GetPageAsync(
                pageNumber,
                pageSize,
                cancellationToken);

        var items =
            page.Items
                .Select(
                    batch => new BatchResponse
                    {
                        BatchId = batch.Id,
                        BatchLabel = batch.BatchLabel,
                        PaymentRecordCount = batch.PaymentRecordCount,
                        BankRecordCount = batch.BankRecordCount,
                        SettlementRecordCount = batch.SettlementRecordCount,
                        TotalRecordCount = batch.TotalRecordCount,
                        ValidationStatus = batch.ValidationStatus,
                        CreatedBy = batch.CreatedBy,
                        CreatedAt = batch.CreatedAt
                    })
                .ToList();

        var totalPages =
            page.TotalCount == 0
                ? 0
                : (int)Math.Ceiling(
                    page.TotalCount /
                    (double)pageSize);

        var response =
            new PagedResponse<BatchResponse>
            {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = page.TotalCount,
                TotalPages = totalPages
            };

        return Ok(response);
    }

    [HttpGet("{batchId:guid}")]
    [ProducesResponseType(
        typeof(BatchResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BatchResponse>> GetBatch(
        Guid batchId,
        CancellationToken cancellationToken)
    {
        var batch = await _batchRepository.GetByIdAsync(
            batchId,
            cancellationToken);

        if (batch is null)
        {
            return Problem(
                detail: $"Batch '{batchId}' was not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Resource Not Found");
        }

        var response = new BatchResponse
        {
            BatchId = batch.Id,
            BatchLabel = batch.BatchLabel,
            PaymentRecordCount = batch.PaymentRecordCount,
            BankRecordCount = batch.BankRecordCount,
            SettlementRecordCount = batch.SettlementRecordCount,
            TotalRecordCount = batch.TotalRecordCount,
            ValidationStatus = batch.ValidationStatus,
            CreatedBy = batch.CreatedBy,
            CreatedAt = batch.CreatedAt
        };

        return Ok(response);
    }
}
