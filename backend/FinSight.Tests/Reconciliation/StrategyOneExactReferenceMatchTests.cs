using FinSight.Application.DTOs.Reconciliation;
using FinSight.Domain.Entities;
using FinSight.Infrastructure.Reconciliation.Strategies;

namespace FinSight.Tests.Reconciliation;

public class StrategyOneExactReferenceMatchTests
{
    [Test]
    public void ExactReferenceAmountAndDate_ReturnsExactEvidence()
    {
        var payment = new PaymentRecord(
            Guid.NewGuid(),
            "PAY-000001",
            "TXN-0001",
            500.00m,
            "INR",
            new DateOnly(2026, 8, 25),
            "COMPLETED");

        var bank = new BankRecord(
            Guid.NewGuid(),
            "BANK-000001",
            "TXN-0001",
            500.00m,
            "INR",
            new DateOnly(2026, 8, 25),
            "CLEARED");

        var settlement = new SettlementRecord(
            Guid.NewGuid(),
            "SET-000001",
            "TXN-0001",
            500.00m,
            "INR",
            new DateOnly(2026, 8, 25),
            "SETTLED");

        var evidence = new ReconciliationEvidence
        {
            TransactionReference = "TXN-0001",
            Payments = new[] { payment },
            Banks = new[] { bank },
            Settlements = new[] { settlement }
        };

        var strategy = new StrategyOneExactReferenceMatch();

        var result = strategy.Evaluate(evidence);

        Assert.That(result.SourcesPresent, Is.True);
        Assert.That(result.ExactReferenceMatch, Is.True);
        Assert.That(result.ExactAmountMatch, Is.True);
        Assert.That(result.ExactDateMatch, Is.True);
        Assert.That(result.NonComparableBusinessState, Is.False);
    }

    [Test]
    public void ReversedFraud_ReturnsNonComparableBusinessState()
    {
        var payment = new PaymentRecord(
            Guid.NewGuid(),
            "PAY-000001",
            "TXN-0001",
            500.00m,
            "INR",
            new DateOnly(2026, 8, 25),
            "COMPLETED");

        var bank = new BankRecord(
            Guid.NewGuid(),
            "BANK-000001",
            "TXN-0001",
            500.00m,
            "INR",
            new DateOnly(2026, 8, 25),
            "REVERSED_FRAUD");

        var settlement = new SettlementRecord(
            Guid.NewGuid(),
            "SET-000001",
            "TXN-0001",
            500.00m,
            "INR",
            new DateOnly(2026, 8, 25),
            "SETTLED");

        var evidence = new ReconciliationEvidence
        {
            TransactionReference = "TXN-0001",
            Payments = new[] { payment },
            Banks = new[] { bank },
            Settlements = new[] { settlement }
        };

        var strategy = new StrategyOneExactReferenceMatch();

        var result = strategy.Evaluate(evidence);

        Assert.That(result.SourcesPresent, Is.True);
        Assert.That(result.ExactReferenceMatch, Is.True);
        Assert.That(
            result.NonComparableBusinessState,
            Is.True);

        Assert.That(
            result.NonComparableReason,
            Is.EqualTo("REVERSED_FRAUD"));
    }

    [Test]
    public void MissingBank_ReturnsSourcesNotPresent()
    {
        var payment = new PaymentRecord(
            Guid.NewGuid(),
            "PAY-000001",
            "TXN-0001",
            500.00m,
            "INR",
            new DateOnly(2026, 8, 25),
            "COMPLETED");

        var settlement = new SettlementRecord(
            Guid.NewGuid(),
            "SET-000001",
            "TXN-0001",
            500.00m,
            "INR",
            new DateOnly(2026, 8, 25),
            "SETTLED");

        var evidence = new ReconciliationEvidence
        {
            TransactionReference = "TXN-0001",
            Payments = new[] { payment },
            Banks = Array.Empty<BankRecord>(),
            Settlements = new[] { settlement }
        };

        var strategy = new StrategyOneExactReferenceMatch();

        var result = strategy.Evaluate(evidence);

        Assert.That(result.SourcesPresent, Is.False);
        Assert.That(result.ExactReferenceMatch, Is.False);
    }
}