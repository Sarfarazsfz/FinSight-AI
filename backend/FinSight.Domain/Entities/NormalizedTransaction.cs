namespace FinSight.Domain.Entities;

public class NormalizedTransaction
{
    public Guid Id { get; private set; }

    public Guid RunId { get; private set; }

    public string TransactionReference { get; private set; } = string.Empty;

    public Guid? PaymentRecordId { get; private set; }

    public Guid? BankRecordId { get; private set; }

    public Guid? SettlementRecordId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private NormalizedTransaction()
    {
    }

    public NormalizedTransaction(
        Guid runId,
        string transactionReference,
        Guid? paymentRecordId,
        Guid? bankRecordId,
        Guid? settlementRecordId)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException(
                "Run ID is required.",
                nameof(runId));
        }

        if (string.IsNullOrWhiteSpace(transactionReference))
        {
            throw new ArgumentException(
                "Transaction reference is required.",
                nameof(transactionReference));
        }

        Id = Guid.NewGuid();
        RunId = runId;

        TransactionReference = transactionReference.Trim();

        PaymentRecordId = paymentRecordId;
        BankRecordId = bankRecordId;
        SettlementRecordId = settlementRecordId;

        CreatedAt = DateTime.UtcNow;
    }
}