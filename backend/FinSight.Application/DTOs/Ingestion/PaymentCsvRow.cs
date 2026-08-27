namespace FinSight.Application.DTOs.Ingestion;

public sealed class PaymentCsvRow
{
    public string PaymentRecordId { get; set; } = string.Empty;

    public string TransactionReference { get; set; } = string.Empty;

    public string Amount { get; set; } = string.Empty;

    public string Currency { get; set; } = string.Empty;

    public string TransactionDate { get; set; } = string.Empty;

    public string PaymentStatus { get; set; } = string.Empty;
}