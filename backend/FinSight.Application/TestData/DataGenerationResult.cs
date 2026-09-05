using FinSight.Application.Evaluation;

namespace FinSight.Application.TestData;

/// <summary>
/// Full output of a single generation run.
/// Row lists are in-memory; they are not persisted.  The API layer uses
/// <see cref="Metadata"/> (specifically the seed and request shape) to
/// regenerate the same rows deterministically for download.
/// </summary>
public sealed record DataGenerationResult(
    GeneratedDatasetMetadata Metadata,
    IReadOnlyList<GeneratedPaymentRow> Payments,
    IReadOnlyList<GeneratedBankRow> Banks,
    IReadOnlyList<GeneratedSettlementRow> Settlements,
    IReadOnlyList<GroundTruthRow> GroundTruth);

// ---------------------------------------------------------------------------
// Row types (mirror the CSV column contracts expected by the ingestion pipeline)
// ---------------------------------------------------------------------------

public sealed record GeneratedPaymentRow(
    string PaymentRecordId,
    string TransactionReference,
    decimal Amount,
    string Currency,
    DateOnly Date,
    string Status);

public sealed record GeneratedBankRow(
    string BankRecordId,
    string TransactionReference,
    decimal Amount,
    string Currency,
    DateOnly Date,
    string Status);

public sealed record GeneratedSettlementRow(
    string SettlementRecordId,
    string TransactionReference,
    decimal Amount,
    string Currency,
    DateOnly Date,
    string Status);
