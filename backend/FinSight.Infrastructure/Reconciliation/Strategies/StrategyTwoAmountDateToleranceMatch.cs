using FinSight.Application.Abstractions.Reconciliation;
using FinSight.Application.DTOs.Reconciliation;

namespace FinSight.Infrastructure.Reconciliation.Strategies;

public sealed class StrategyTwoAmountDateToleranceMatch
    : IAmountDateToleranceMatchStrategy
{
    private const decimal AmountTolerance = 0.00m;
    private const int DateToleranceHours = 24;

    public StrategyEvidence Evaluate(
        ReconciliationEvidence evidence,
        StrategyEvidence? previousEvidence = null)
    {
        if (previousEvidence is null)
        {
            throw new ArgumentNullException(
                nameof(previousEvidence),
                "Strategy Two requires evidence produced by Strategy One.");
        }

        var exactEvidence = previousEvidence;

        if (!exactEvidence.SourcesPresent)
        {
            return new StrategyEvidence
            {
                SourcesPresent = false,
                ExactReferenceMatch =
                    exactEvidence.ExactReferenceMatch
            };
        }

        if (evidence.HasAnyDuplicate)
        {
            return new StrategyEvidence
            {
                SourcesPresent = true,
                ExactReferenceMatch =
                    exactEvidence.ExactReferenceMatch
            };
        }

        if (exactEvidence.NonComparableBusinessState)
        {
            return new StrategyEvidence
            {
                SourcesPresent =
                    exactEvidence.SourcesPresent,

                ExactReferenceMatch =
                    exactEvidence.ExactReferenceMatch,

                NonComparableBusinessState = true,

                NonComparableReason =
                    exactEvidence.NonComparableReason
            };
        }

        if (!exactEvidence.ExactReferenceMatch)
        {
            return new StrategyEvidence
            {
                SourcesPresent = true,
                ExactReferenceMatch = false
            };
        }

        var payment = evidence.Payments[0];
        var bank = evidence.Banks[0];
        var settlement = evidence.Settlements[0];

        var paymentBankAmountDifference =
            Math.Abs(payment.Amount - bank.Amount);

        var bankSettlementAmountDifference =
            Math.Abs(bank.Amount - settlement.Amount);

        var amountWithinTolerance =
            paymentBankAmountDifference <=
                AmountTolerance &&
            bankSettlementAmountDifference <=
                AmountTolerance;

        var paymentBankDateDifferenceHours =
            Math.Abs(
                (
                    payment.TransactionDate.ToDateTime(
                        TimeOnly.MinValue) -
                    bank.TransactionDate.ToDateTime(
                        TimeOnly.MinValue)
                ).TotalHours);

        var bankSettlementDateDifferenceHours =
            Math.Abs(
                (
                    bank.TransactionDate.ToDateTime(
                        TimeOnly.MinValue) -
                    settlement.TransactionDate.ToDateTime(
                        TimeOnly.MinValue)
                ).TotalHours);

        var dateWithinTolerance =
            paymentBankDateDifferenceHours <=
                DateToleranceHours &&
            bankSettlementDateDifferenceHours <=
                DateToleranceHours;

        return new StrategyEvidence
        {
            SourcesPresent = true,
            ExactReferenceMatch = true,
            ExactAmountMatch =
                exactEvidence.ExactAmountMatch,
            ExactDateMatch =
                exactEvidence.ExactDateMatch,
            AmountWithinTolerance =
                amountWithinTolerance,
            DateWithinTolerance =
                dateWithinTolerance,
            AmountMismatch =
                !amountWithinTolerance,
            DateMismatch =
                !dateWithinTolerance
        };
    }
}