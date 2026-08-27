using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Ingestion;
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
