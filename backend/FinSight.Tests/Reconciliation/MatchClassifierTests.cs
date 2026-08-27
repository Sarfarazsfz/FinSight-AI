using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Infrastructure.Reconciliation;
using FinSight.Application.DTOs.Reconciliation;

namespace FinSight.Tests.Reconciliation;

public class MatchClassifierTests
{
    [Test]
    public void ExactMatch_ReturnsMatched()
    {
        var evidence = CreateEvidence();

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
            ExactAmountMatch = true,
            ExactDateMatch = true,
            AmountWithinTolerance = true,
            DateWithinTolerance = true
        };

        var classifier = new MatchClassifier();

        var result = classifier.Classify(
            evidence,
            exactEvidence,
            toleranceEvidence);

        Assert.That(result.Status, Is.EqualTo(MatchStatus.Matched));
        Assert.That(
            result.ReasonCode,
            Is.EqualTo(ReconciliationReasonCode.EXACT_MATCH));
    }

    [Test]
    public void MissingBank_ReturnsMissing()
    {
        var evidence = CreateEvidence(
            includeBank: false);

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

        Assert.That(result.Status, Is.EqualTo(MatchStatus.Missing));
        Assert.That(
            result.ReasonCode,
            Is.EqualTo(
                ReconciliationReasonCode.SOURCE_ABSENT_BANK));
    }

    [Test]
    public void DuplicatePayment_TakesHighestPrecedence()
    {
        var evidence = CreateEvidence(
            duplicatePayment: true);

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
            ExactAmountMatch = true,
            ExactDateMatch = true,
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
                ReconciliationReasonCode.DUPLICATE_PAYMENT));
    }

    [Test]
    public void AmountMismatch_TakesPrecedenceOverDateMismatch()
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

    [Test]
    public void ReversedFraud_ReturnsUnresolved()
    {
        var evidence = CreateEvidence();

        var exactEvidence = new StrategyEvidence
        {
            SourcesPresent = true,
            ExactReferenceMatch = true,
            ExactAmountMatch = true,
            ExactDateMatch = true,
            NonComparableBusinessState = true,
            NonComparableReason = "REVERSED_FRAUD"
        };

        var toleranceEvidence = new StrategyEvidence
        {
            SourcesPresent = true,
            ExactReferenceMatch = true,
            NonComparableBusinessState = true,
            NonComparableReason = "REVERSED_FRAUD"
        };

        var classifier = new MatchClassifier();

        var result = classifier.Classify(
            evidence,
            exactEvidence,
            toleranceEvidence);

        Assert.That(
            result.Status,
            Is.EqualTo(MatchStatus.Unresolved));

        Assert.That(
            result.ReasonCode,
            Is.EqualTo(
                ReconciliationReasonCode.UNRESOLVED));

        Assert.That(
            result.ExceptionCategory,
            Is.EqualTo(
                ExceptionCategory.Unresolved));
    }

    private static ReconciliationEvidence CreateEvidence(
        bool includeBank = true,
        bool duplicatePayment = false)
    {
        var payment = new PaymentRecord(
            Guid.NewGuid(),
            "PAY-000001",
            "TXN-0001",
            500.00m,
            "INR",
            new DateOnly(2026, 8, 25),
            "COMPLETED");

        var payments = duplicatePayment
            ? new[]
            {
                payment,
                new PaymentRecord(
                    Guid.NewGuid(),
                    "PAY-000002",
                    "TXN-0001",
                    500.00m,
                    "INR",
                    new DateOnly(2026, 8, 25),
                    "COMPLETED")
            }
            : new[]
            {
                payment
            };

        var banks = includeBank
            ? new[]
            {
                new BankRecord(
                    Guid.NewGuid(),
                    "BANK-000001",
                    "TXN-0001",
                    500.00m,
                    "INR",
                    new DateOnly(2026, 8, 25),
                    "CLEARED")
            }
            : Array.Empty<BankRecord>();

        var settlements = new[]
        {
            new SettlementRecord(
                Guid.NewGuid(),
                "SET-000001",
                "TXN-0001",
                500.00m,
                "INR",
                new DateOnly(2026, 8, 25),
                "SETTLED")
        };

        return new ReconciliationEvidence
        {
            TransactionReference = "TXN-0001",
            Payments = payments,
            Banks = banks,
            Settlements = settlements
        };
    }
}