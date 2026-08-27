using FinSight.Domain.Entities;

namespace FinSight.Application.DTOs.Reconciliation;

public sealed class ReconciliationEvidence
{
    public string TransactionReference { get; init; } = string.Empty;

    public IReadOnlyList<PaymentRecord> Payments { get; init; }
        = Array.Empty<PaymentRecord>();

    public IReadOnlyList<BankRecord> Banks { get; init; }
        = Array.Empty<BankRecord>();

    public IReadOnlyList<SettlementRecord> Settlements { get; init; }
        = Array.Empty<SettlementRecord>();

    public bool HasPayment =>
        Payments.Count > 0;

    public bool HasBank =>
        Banks.Count > 0;

    public bool HasSettlement =>
        Settlements.Count > 0;

    public bool HasDuplicatePayment =>
        Payments.Count > 1;

    public bool HasDuplicateBank =>
        Banks.Count > 1;

    public bool HasDuplicateSettlement =>
        Settlements.Count > 1;

    public bool HasAnyDuplicate =>
        HasDuplicatePayment ||
        HasDuplicateBank ||
        HasDuplicateSettlement;
}