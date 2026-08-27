namespace FinSight.Application.DTOs.Ingestion;

public sealed class BatchIngestionResult
{
    public Guid BatchId { get; init; }

    public string ValidationStatus { get; init; } = string.Empty;

    public int PaymentRecordCount { get; init; }

    public int BankRecordCount { get; init; }

    public int SettlementRecordCount { get; init; }

    public int TotalRecordCount { get; init; }
}