namespace FinSight.Domain.Entities;

public class Batch
{
    public Guid Id { get; private set; }

    public string BatchLabel { get; private set; } = string.Empty;

    public int PaymentRecordCount { get; private set; }

    public int BankRecordCount { get; private set; }

    public int SettlementRecordCount { get; private set; }

    public int TotalRecordCount { get; private set; }

    public string ValidationStatus { get; private set; } = string.Empty;

    public string CreatedBy { get; private set; } = string.Empty;

    /// <summary>
    /// The ownership root for every downstream reconciliation resource
    /// (runs, results, exceptions all resolve ownership through the batch
    /// they belong to -- see IBatchAccessService). Nullable because
    /// batches created before ownership existed have no way to be
    /// correlated to a real user without inventing one; those rows stay
    /// unowned rather than being silently assigned. A null value means
    /// the batch is inaccessible through the ownership boundary, by
    /// design -- deny, not a random grant.
    /// </summary>
    public Guid? CreatedByUserId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private Batch()
    {
    }

    public Batch(
        string batchLabel,
        int paymentRecordCount,
        int bankRecordCount,
        int settlementRecordCount,
        string validationStatus,
        string createdBy,
        Guid? createdByUserId = null)
    {
        if (string.IsNullOrWhiteSpace(batchLabel))
        {
            throw new ArgumentException(
                "Batch label is required.",
                nameof(batchLabel));
        }

        if (paymentRecordCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(paymentRecordCount),
                "Payment record count cannot be negative.");
        }

        if (bankRecordCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bankRecordCount),
                "Bank record count cannot be negative.");
        }

        if (settlementRecordCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settlementRecordCount),
                "Settlement record count cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(validationStatus))
        {
            throw new ArgumentException(
                "Validation status is required.",
                nameof(validationStatus));
        }

        if (string.IsNullOrWhiteSpace(createdBy))
        {
            throw new ArgumentException(
                "CreatedBy is required.",
                nameof(createdBy));
        }

        Id = Guid.NewGuid();

        BatchLabel = batchLabel.Trim();

        PaymentRecordCount = paymentRecordCount;
        BankRecordCount = bankRecordCount;
        SettlementRecordCount = settlementRecordCount;

        TotalRecordCount =
            paymentRecordCount +
            bankRecordCount +
            settlementRecordCount;

        ValidationStatus = validationStatus.Trim();

        CreatedBy = createdBy.Trim();

        CreatedByUserId = createdByUserId;

        CreatedAt = DateTime.UtcNow;
    }
}