namespace FinSight.DataGenerator.Models;

public sealed class GeneratedSourceRows
{
    public List<PaymentSourceRow> Payments { get; } = new();

    public List<BankSourceRow> Banks { get; } = new();

    public List<SettlementSourceRow> Settlements { get; } = new();
}

public sealed record PaymentSourceRow(
    string PaymentRecordId,
    string TransactionReference,
    decimal Amount,
    string Currency,
    DateOnly TransactionDate,
    string PaymentStatus);

public sealed record BankSourceRow(
    string BankRecordId,
    string TransactionReference,
    decimal Amount,
    string Currency,
    DateOnly TransactionDate,
    string BankStatus);

public sealed record SettlementSourceRow(
    string SettlementRecordId,
    string TransactionReference,
    decimal Amount,
    string Currency,
    DateOnly TransactionDate,
    string SettlementStatus);