using System.Globalization;
using System.Text;
using FinSight.Api.Authentication;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.TestData;
using FinSight.Infrastructure.TestData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Controllers;

/// <summary>
/// Parametrised synthetic-data generator for reconciliation evaluation and demos.
///
/// Security guarantees:
///   • All endpoints are [Authorize] — anonymous callers receive 401.
///   • Download endpoints verify that the requesting user is the same one
///     who generated the dataset — no cross-user data access.
///   • No production data is queried.  Generation is pure computation.
///   • Generated files contain no secrets, credentials, or PII.
///   • Dataset size is bounded (50/100/250/500) to prevent DoS.
/// </summary>
[ApiController]
[Route("api/test-data")]
[Authorize]
public sealed class TestDataController : ControllerBase
{
    // -----------------------------------------------------------------
    // Allowed sizes (must match SyntheticDataGenerator's own list)
    // -----------------------------------------------------------------
    private static readonly HashSet<int> AllowedSizes = [50, 100, 250, 500];

    private readonly ISyntheticDataGenerator _generator;
    private readonly TestDataSessionStore    _sessionStore;
    private readonly ICurrentUserService     _currentUser;

    public TestDataController(
        ISyntheticDataGenerator generator,
        TestDataSessionStore    sessionStore,
        ICurrentUserService     currentUser)
    {
        _generator    = generator;
        _sessionStore = sessionStore;
        _currentUser  = currentUser;
    }

    // -----------------------------------------------------------------
    // POST /api/test-data/generate
    // -----------------------------------------------------------------

    /// <summary>
    /// Generates a synthetic dataset and returns its metadata plus download
    /// links.  No CSV content is embedded in the response — use the per-file
    /// download endpoints below.
    /// </summary>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(GenerateDatasetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Generate([FromBody] GenerateDatasetRequest body)
    {
        if (body is null)
        {
            return Problem(
                detail: "Request body is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        if (!AllowedSizes.Contains(body.Size))
        {
            return Problem(
                detail: $"Size must be one of [{string.Join(", ", AllowedSizes.Order())}].",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        if (!Enum.IsDefined(typeof(GenerationMode), body.Mode))
        {
            return Problem(
                detail: $"Unknown generation mode: {body.Mode}.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        if (!Enum.IsDefined(typeof(CorruptionIntensity), body.Intensity))
        {
            return Problem(
                detail: $"Unknown corruption intensity: {body.Intensity}.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return Unauthorized();
        }

        var request = new DataGenerationRequest
        {
            Size      = body.Size,
            Mode      = body.Mode,
            Intensity = body.Intensity,
            Seed      = body.Seed,
        };

        DataGenerationResult result;
        try
        {
            result = _generator.Generate(request);
        }
        catch (ArgumentException ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        // Persist only the minimal session info (seed + request params) —
        // the full CSV is never stored in memory.
        _sessionStore.Store(
            result.Metadata.GenerationId,
            userId.Value,
            request,
            result.Metadata.Seed);

        return Ok(new GenerateDatasetResponse(result.Metadata));
    }

    // -----------------------------------------------------------------
    // GET /api/test-data/download/{generationId}/payments
    // -----------------------------------------------------------------

    [HttpGet("download/{generationId}/payments")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DownloadPayments(string generationId)
    {
        var result = RegenerateOrNotFound(generationId, out var notFound);
        if (notFound is not null) return notFound;

        var csv = BuildPaymentsCsv(result!.Payments);
        return CsvFile(csv, "payments.csv");
    }

    // -----------------------------------------------------------------
    // GET /api/test-data/download/{generationId}/bank
    // -----------------------------------------------------------------

    [HttpGet("download/{generationId}/bank")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DownloadBank(string generationId)
    {
        var result = RegenerateOrNotFound(generationId, out var notFound);
        if (notFound is not null) return notFound;

        var csv = BuildBankCsv(result!.Banks);
        return CsvFile(csv, "bank.csv");
    }

    // -----------------------------------------------------------------
    // GET /api/test-data/download/{generationId}/settlements
    // -----------------------------------------------------------------

    [HttpGet("download/{generationId}/settlements")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DownloadSettlements(string generationId)
    {
        var result = RegenerateOrNotFound(generationId, out var notFound);
        if (notFound is not null) return notFound;

        var csv = BuildSettlementsCsv(result!.Settlements);
        return CsvFile(csv, "settlements.csv");
    }

    // -----------------------------------------------------------------
    // GET /api/test-data/download/{generationId}/ground-truth
    // -----------------------------------------------------------------

    [HttpGet("download/{generationId}/ground-truth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DownloadGroundTruth(string generationId)
    {
        var result = RegenerateOrNotFound(generationId, out var notFound);
        if (notFound is not null) return notFound;

        var csv = BuildGroundTruthCsv(result!.GroundTruth);
        return CsvFile(csv, "ground-truth.csv");
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    /// <summary>
    /// Looks up the session and regenerates the dataset using the stored
    /// seed (same seed → identical output).  Returns null with an
    /// ActionResult in <paramref name="notFound"/> on failure.
    /// </summary>
    private DataGenerationResult? RegenerateOrNotFound(
        string generationId,
        out IActionResult? notFound)
    {
        notFound = null;

        var userId = _currentUser.UserId;
        if (userId is null)
        {
            notFound = Unauthorized();
            return null;
        }

        var session = _sessionStore.TryGet(generationId, userId.Value);
        if (session is null)
        {
            notFound = NotFound(new { detail = "Dataset not found or has expired (datasets expire after 1 hour)." });
            return null;
        }

        // Regenerate with the stored seed → deterministic, identical to
        // the original generation.
        var request = new DataGenerationRequest
        {
            Size      = session.Value.Request.Size,
            Mode      = session.Value.Request.Mode,
            Intensity = session.Value.Request.Intensity,
            Seed      = session.Value.Seed,
        };

        return _generator.Generate(request);
    }

    private static IActionResult CsvFile(byte[] content, string fileName)
        => new FileContentResult(content, "text/csv; charset=utf-8")
        {
            FileDownloadName = fileName
        };

    // -----------------------------------------------------------------
    // CSV serialisation (mirrors the format the ingestion pipeline expects)
    // -----------------------------------------------------------------

    private static byte[] BuildPaymentsCsv(
        IReadOnlyList<GeneratedPaymentRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "payment_record_id,transaction_reference,amount,currency,transaction_date,payment_status");

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(",",
                Escape(r.PaymentRecordId),
                Escape(r.TransactionReference),
                r.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                Escape(r.Currency),
                r.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Escape(r.Status)));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static byte[] BuildBankCsv(
        IReadOnlyList<GeneratedBankRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "bank_record_id,transaction_reference,amount,currency,transaction_date,bank_status");

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(",",
                Escape(r.BankRecordId),
                Escape(r.TransactionReference),
                r.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                Escape(r.Currency),
                r.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Escape(r.Status)));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static byte[] BuildSettlementsCsv(
        IReadOnlyList<GeneratedSettlementRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "settlement_record_id,transaction_reference,amount,currency,transaction_date,settlement_status");

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(",",
                Escape(r.SettlementRecordId),
                Escape(r.TransactionReference),
                r.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                Escape(r.Currency),
                r.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Escape(r.Status)));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static byte[] BuildGroundTruthCsv(
        IReadOnlyList<Application.Evaluation.GroundTruthRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "transaction_reference,scenario_type,expected_status,expected_reason_code," +
            "expected_exception_category,expected_payment_present,expected_bank_present," +
            "expected_settlement_present,expected_amount_relationship,expected_date_relationship");

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(",",
                Escape(r.TransactionReference),
                Escape(r.ScenarioType),
                Escape(r.ExpectedStatus),
                Escape(r.ExpectedReasonCode),
                Escape(r.ExpectedExceptionCategory),
                r.ExpectedPaymentPresent    ? "true" : "false",
                r.ExpectedBankPresent       ? "true" : "false",
                r.ExpectedSettlementPresent ? "true" : "false",
                Escape(r.ExpectedAmountRelationship),
                Escape(r.ExpectedDateRelationship)));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (!value.Contains(',')  &&
            !value.Contains('"') &&
            !value.Contains('\r') &&
            !value.Contains('\n'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}

// ---------------------------------------------------------------------------
// Request / Response DTOs (local to the API layer)
// ---------------------------------------------------------------------------

/// <summary>Request body for POST /api/test-data/generate.</summary>
public sealed record GenerateDatasetRequest
{
    /// <summary>Number of logical transactions (50, 100, 250, or 500).</summary>
    public int Size { get; init; } = 100;

    /// <summary>Generation mode (int value of <see cref="GenerationMode"/>).</summary>
    public GenerationMode Mode { get; init; } = GenerationMode.Mixed;

    /// <summary>Corruption intensity (int value of <see cref="CorruptionIntensity"/>).</summary>
    public CorruptionIntensity Intensity { get; init; } = CorruptionIntensity.Medium;

    /// <summary>
    /// Optional explicit seed.  Omit (or set to null) for a new random seed.
    /// Provide the same seed to reproduce an identical dataset.
    /// </summary>
    public long? Seed { get; init; }
}

/// <summary>Response body from POST /api/test-data/generate.</summary>
public sealed record GenerateDatasetResponse(GeneratedDatasetMetadata Metadata);
