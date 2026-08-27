namespace FinSight.Domain.Entities;

public class SettlementRecord
{
    public Guid Id { get; private set; }

    public Guid BatchId { get; private set; }

    public string SourceRecordIdentifier { get; private set; } = string.Empty;

    public string TransactionReference { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public DateOnly TransactionDate { get; private set; }

    public string Status { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }

    private SettlementRecord()
    {
    }

    public SettlementRecord(
        Guid batchId,
        string sourceRecordIdentifier,
        string transactionReference,
        decimal amount,
        string currency,
        DateOnly transactionDate,
        string status)
    {
        if (batchId == Guid.Empty)
        {
            throw new ArgumentException(
                "Batch ID is required.",
                nameof(batchId));
        }

        if (string.IsNullOrWhiteSpace(sourceRecordIdentifier))
        {
            throw new ArgumentException(
                "Source record identifier is required.",
                nameof(sourceRecordIdentifier));
        }

        if (string.IsNullOrWhiteSpace(transactionReference))
        {
            throw new ArgumentException(
                "Transaction reference is required.",
                nameof(transactionReference));
        }

        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Amount cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException(
                "Currency is required.",
                nameof(currency));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException(
                "Settlement status is required.",
                nameof(status));
        }

        Id = Guid.NewGuid();
        BatchId = batchId;
        SourceRecordIdentifier = sourceRecordIdentifier.Trim();
        TransactionReference = transactionReference.Trim();
        Amount = decimal.Round(amount, 2);
        Currency = currency.Trim().ToUpperInvariant();
        TransactionDate = transactionDate;
        Status = status.Trim().ToUpperInvariant();
        CreatedAt = DateTime.UtcNow;
    }
}