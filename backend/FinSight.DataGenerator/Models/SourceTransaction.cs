namespace FinSight.DataGenerator.Models;

public sealed class SourceTransaction
{
    public int SequenceNumber { get; init; }

    public string TransactionReference { get; init; } = string.Empty;

    public decimal BaseAmount { get; init; }

    public DateOnly BaseDate { get; init; }

    public string Currency { get; init; } = "INR";

    public string PaymentStatus { get; init; } = "COMPLETED";

    public string BankStatus { get; init; } = "CLEARED";

    public string SettlementStatus { get; init; } = "SETTLED";
}