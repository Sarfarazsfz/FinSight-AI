namespace FinSight.Application.DTOs.Ingestion;

public sealed class BankCsvRow
{
    public string BankRecordId { get; set; } = string.Empty;

    public string TransactionReference { get; set; } = string.Empty;

    public string Amount { get; set; } = string.Empty;

    public string Currency { get; set; } = string.Empty;

    public string TransactionDate { get; set; } = string.Empty;

    public string BankStatus { get; set; } = string.Empty;
}