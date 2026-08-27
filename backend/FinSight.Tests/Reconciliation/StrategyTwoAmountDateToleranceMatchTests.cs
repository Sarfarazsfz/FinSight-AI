using FinSight.Application.DTOs.Reconciliation;
using FinSight.Domain.Entities;
using FinSight.Infrastructure.Reconciliation.Strategies;

namespace FinSight.Tests.Reconciliation;

public class StrategyTwoAmountDateToleranceMatchTests
{
    [Test]
    public void ExactValuesWithinTolerance_ReturnsToleranceEvidence()
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
            new DateOnly(2026, 8, 26),
            "CLEARED");

        var settlement = new SettlementRecord(
            Guid.NewGuid(),
            "SET-000001",
            "TXN-0001",
            500.00m,
            "INR",
            new DateOnly(2026, 8, 26),
            "SETTLED");

        var evidence = new ReconciliationEvidence
        {
            TransactionReference = "TXN-0001",
            Payments = new[] { payment },
            Banks = new[] { bank },
            Settlements = new[] { settlement }
        };

        var exactEvidence = new StrategyEvidence
        {
            SourcesPresent = true,
            ExactReferenceMatch = true,
            ExactAmountMatch = true,
            ExactDateMatch = false
        };

        var strategy = new StrategyTwoAmountDateToleranceMatch();

        var result = strategy.Evaluate(
            evidence,
            exactEvidence);

        Assert.That(
            result.AmountWithinTolerance,
            Is.True);

        Assert.That(
            result.DateWithinTolerance,
            Is.True);

        Assert.That(
            result.AmountMismatch,
            Is.False);

        Assert.That(
            result.DateMismatch,
            Is.False);
    }

    [Test]
    public void DateBeyondTolerance_ReturnsDateMismatch()
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
            new DateOnly(2026, 8, 27),
            "CLEARED");

        var settlement = new SettlementRecord(
            Guid.NewGuid(),
            "SET-000001",
            "TXN-0001",
            500.00m,
            "INR",
            new DateOnly(2026, 8, 27),
            "SETTLED");

        var evidence = new ReconciliationEvidence
        {
            TransactionReference = "TXN-0001",
            Payments = new[] { payment },
            Banks = new[] { bank },
            Settlements = new[] { settlement }
        };

        var exactEvidence = new StrategyEvidence
        {
            SourcesPresent = true,
            ExactReferenceMatch = true,
            ExactAmountMatch = true,
            ExactDateMatch = false
        };

        var strategy = new StrategyTwoAmountDateToleranceMatch();

        var result = strategy.Evaluate(
            evidence,
            exactEvidence);

        Assert.That(
            result.DateWithinTolerance,
            Is.False);

        Assert.That(
            result.DateMismatch,
            Is.True);
    }
}