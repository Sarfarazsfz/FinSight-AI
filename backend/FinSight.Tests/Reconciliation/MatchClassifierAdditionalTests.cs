using FinSight.Application.DTOs.Reconciliation;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Infrastructure.Reconciliation;

namespace FinSight.Tests.Reconciliation;

public class MatchClassifierAdditionalTests
{
    [Test]
    public void DuplicateBank_ReturnsDuplicateBank()
    {
        var evidence = CreateEvidence(
            duplicateBank: true);

        var exactEvidence = new StrategyEvidence
        {
            SourcesPresent = true,
            ExactReferenceMatch = true,
            ExactAmountMatch = true,
            ExactDateMatch = true
        };

        var toleranceEvidence = new StrategyEvidence
        {
            SourcesPresent = true,
            ExactReferenceMatch = true,
            AmountWithinTolerance = true,
            DateWithinTolerance = true
        };

        var classifier = new MatchClassifier();

        var result = classifier.Classify(
            evidence,
            exactEvidence,
            toleranceEvidence);

        Assert.That(
            result.Status,
            Is.EqualTo(MatchStatus.Duplicate));

        Assert.That(
            result.ReasonCode,
            Is.EqualTo(
                ReconciliationReasonCode.DUPLICATE_BANK));
    }

    [Test]
    public void DuplicateSettlement_ReturnsDuplicateSettlement()
    {
        var evidence = CreateEvidence(
            duplicateSettlement: true);

        var exactEvidence = new StrategyEvidence
        {
            SourcesPresent = true,
            ExactReferenceMatch = true,
            ExactAmountMatch = true,
            ExactDateMatch = true
        };

        var toleranceEvidence = new StrategyEvidence
        {
            SourcesPresent = true,
            ExactReferenceMatch = true,
            AmountWithinTolerance = true,
            DateWithinTolerance = true
        };

        var classifier = new MatchClassifier();

        var result = classifier.Classify(
            evidence,
            exactEvidence,
            toleranceEvidence);

        Assert.That(
            result.Status,
            Is.EqualTo(MatchStatus.Duplicate));

        Assert.That(
            result.ReasonCode,
            Is.EqualTo(
                ReconciliationReasonCode.DUPLICATE_SETTLEMENT));
    }

    [Test]
    public void MissingSettlement_ReturnsMissingSettlement()
    {
        var evidence = CreateEvidence(
            includeSettlement: false);

        var exactEvidence = new StrategyEvidence
        {
            SourcesPresent = false,
            ExactReferenceMatch = false
        };

        var toleranceEvidence = new StrategyEvidence
        {
            SourcesPresent = false,
            ExactReferenceMatch = false
        };

        var classifier = new MatchClassifier();

        var result = classifier.Classify(
            evidence,
            exactEvidence,
            toleranceEvidence);

        Assert.That(
            result.Status,
            Is.EqualTo(MatchStatus.Missing));

        Assert.That(
            result.ReasonCode,
            Is.EqualTo(
                ReconciliationReasonCode.SOURCE_ABSENT_SETTLEMENT));
    }

    [Test]
    public void MissingPayment_ReturnsMissingPayment()
    {
        var evidence = CreateEvidence(
            includePayment: false);

        var exactEvidence = new StrategyEvidence
        {
            SourcesPresent = false,
            ExactReferenceMatch = false
        };

        var toleranceEvidence = new StrategyEvidence
        {
            SourcesPresent = false,
            ExactReferenceMatch = false
        };

        var classifier = new MatchClassifier();

        var result = classifier.Classify(
            evidence,
            exactEvidence,
            toleranceEvidence);

        Assert.That(
            result.Status,
            Is.EqualTo(MatchStatus.Missing));

        Assert.That(
            result.ReasonCode,
            Is.EqualTo(
                ReconciliationReasonCode.SOURCE_ABSENT_PAYMENT));
    }

    [Test]
    public void DateMismatch_ReturnsDateOutOfTolerance()
    {
        var evidence = CreateEvidence();

        var exactEvidence = new StrategyEvidence
        {
            SourcesPresent = true,
            ExactReferenceMatch = true,
            ExactAmountMatch = true,
            ExactDateMatch = false,
            DateMismatch = true
        };

        var toleranceEvidence = new StrategyEvidence
        {
            SourcesPresent = true,
            ExactReferenceMatch = true,
            ExactAmountMatch = true,
            ExactDateMatch = false,
            AmountWithinTolerance = true,
            DateWithinTolerance = false,
            AmountMismatch = false,
            DateMismatch = true
        };

        var classifier = new MatchClassifier();

        var result = classifier.Classify(
            evidence,
            exactEvidence,
            toleranceEvidence);

        Assert.That(
            result.Status,
            Is.EqualTo(MatchStatus.Mismatched));

        Assert.That(
            result.ReasonCode,
            Is.EqualTo(
                ReconciliationReasonCode.DATE_OUT_OF_TOLERANCE));
    }

    [Test]
    public void ToleranceMatch_ReturnsMatched()
    {
        var evidence = CreateEvidence();

        var exactEvidence = new StrategyEvidence
        {
            SourcesPresent = true,
            ExactReferenceMatch = true,
            ExactAmountMatch = false,
            ExactDateMatch = false
        };

        var toleranceEvidence = new StrategyEvidence
        {
            SourcesPresent = true,
            ExactReferenceMatch = true,
            ExactAmountMatch = false,
            ExactDateMatch = false,
            AmountWithinTolerance = true,
            DateWithinTolerance = true,
            AmountMismatch = false,
            DateMismatch = false
        };

        var classifier = new MatchClassifier();

        var result = classifier.Classify(
            evidence,
            exactEvidence,
            toleranceEvidence);

        Assert.That(
            result.Status,
            Is.EqualTo(MatchStatus.Matched));

        Assert.That(
            result.ReasonCode,
            Is.EqualTo(
                ReconciliationReasonCode.TOLERANCE_MATCH));
    }

    [Test]
    public void AmountAndDateMismatch_AmountTakesPrecedence()
    {
        var evidence = CreateEvidence();

        var exactEvidence = new StrategyEvidence
        {
            SourcesPresent = true,
            ExactReferenceMatch = true,
            ExactAmountMatch = false,
            ExactDateMatch = false,
            AmountMismatch = true,
            DateMismatch = true
        };

        var toleranceEvidence = new StrategyEvidence
        {
            SourcesPresent = true,
            ExactReferenceMatch = true,
            ExactAmountMatch = false,
            ExactDateMatch = false,
            AmountWithinTolerance = false,
            DateWithinTolerance = false,
            AmountMismatch = true,
            DateMismatch = true
        };

        var classifier = new MatchClassifier();

        var result = classifier.Classify(
            evidence,
            exactEvidence,
            toleranceEvidence);

        Assert.That(
            result.Status,
            Is.EqualTo(MatchStatus.Mismatched));

        Assert.That(
            result.ReasonCode,
            Is.EqualTo(
                ReconciliationReasonCode.AMOUNT_MISMATCH));
    }

    private static ReconciliationEvidence CreateEvidence(
        bool includePayment = true,
        bool includeBank = true,
        bool includeSettlement = true,
        bool duplicateBank = false,
        bool duplicateSettlement = false)
    {
        var payments = includePayment
            ? new[]
            {
                new PaymentRecord(
                    Guid.NewGuid(),
                    "PAY-000001",
                    "TXN-0001",
                    500.00m,
                    "INR",
                    new DateOnly(2026, 8, 25),
                    "COMPLETED")
            }
            : Array.Empty<PaymentRecord>();

        var bank = new BankRecord(
            Guid.NewGuid(),
            "BANK-000001",
            "TXN-0001",
            500.00m,
            "INR",
            new DateOnly(2026, 8, 25),
            "CLEARED");

        var banks = includeBank
            ? duplicateBank
                ? new[]
                {
                    bank,
                    new BankRecord(
                        Guid.NewGuid(),
                        "BANK-000002",
                        "TXN-0001",
                        500.00m,
                        "INR",
                        new DateOnly(2026, 8, 25),
                        "CLEARED")
                }
                : new[]
                {
                    bank
                }
            : Array.Empty<BankRecord>();

        var settlement = new SettlementRecord(
            Guid.NewGuid(),
            "SET-000001",
            "TXN-0001",
            500.00m,
            "INR",
            new DateOnly(2026, 8, 25),
            "SETTLED");

        var settlements = includeSettlement
            ? duplicateSettlement
                ? new[]
                {
                    settlement,
                    new SettlementRecord(
                        Guid.NewGuid(),
                        "SET-000002",
                        "TXN-0001",
                        500.00m,
                        "INR",
                        new DateOnly(2026, 8, 25),
                        "SETTLED")
                }
                : new[]
                {
                    settlement
                }
            : Array.Empty<SettlementRecord>();

        return new ReconciliationEvidence
        {
            TransactionReference = "TXN-0001",
            Payments = payments,
            Banks = banks,
            Settlements = settlements
        };
    }
}