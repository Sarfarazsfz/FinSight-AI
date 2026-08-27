namespace FinSight.Application.DTOs.Ingestion;

public sealed class SettlementCsvRow
{
    public string SettlementRecordId { get; set; } = string.Empty;

    public string TransactionReference { get; set; } = string.Empty;

    public string Amount { get; set; } = string.Empty;

    public string Currency { get; set; } = string.Empty;

    public string TransactionDate { get; set; } = string.Empty;

    public string SettlementStatus { get; set; } = string.Empty;
}