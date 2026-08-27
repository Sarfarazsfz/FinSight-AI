namespace FinSight.Application.DTOs.Ingestion;

public sealed class BatchResponse
{
    public Guid BatchId { get; init; }

    public string BatchLabel { get; init; } = string.Empty;

    public int PaymentRecordCount { get; init; }

    public int BankRecordCount { get; init; }

    public int SettlementRecordCount { get; init; }

    public int TotalRecordCount { get; init; }

    public string ValidationStatus { get; init; } = string.Empty;

    public string CreatedBy { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }
}