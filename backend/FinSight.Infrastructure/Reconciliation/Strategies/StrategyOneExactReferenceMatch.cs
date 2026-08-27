using FinSight.Application.Abstractions.Reconciliation;
using FinSight.Application.DTOs.Reconciliation;

namespace FinSight.Infrastructure.Reconciliation.Strategies;

public sealed class StrategyOneExactReferenceMatch
    : IExactReferenceMatchStrategy
{
    public StrategyEvidence Evaluate(
        ReconciliationEvidence evidence,
        StrategyEvidence? previousEvidence = null)
    {
        var sourcesPresent =
            evidence.HasPayment &&
            evidence.HasBank &&
            evidence.HasSettlement;

        // Missing source evidence is handled later by MatchClassifier.
        if (!sourcesPresent)
        {
            return new StrategyEvidence
            {
                SourcesPresent = false,
                ExactReferenceMatch = false
            };
        }

        // Business duplicates are handled with highest precedence.
        // Do not allow duplicate rows to enter mathematical matching.
        if (evidence.HasAnyDuplicate)
        {
            return new StrategyEvidence
            {
                SourcesPresent = true,
                ExactReferenceMatch = true
            };
        }

        var payment = evidence.Payments[0];
        var bank = evidence.Banks[0];
        var settlement = evidence.Settlements[0];

        var referenceMatch =
            string.Equals(
                payment.TransactionReference,
                bank.TransactionReference,
                StringComparison.Ordinal) &&
            string.Equals(
                bank.TransactionReference,
                settlement.TransactionReference,
                StringComparison.Ordinal);

        // REVERSED_FRAUD is a deterministic business/data state.
        // It is not a parser or system failure.
        var nonComparableBusinessState =
            string.Equals(
                bank.Status,
                "REVERSED_FRAUD",
                StringComparison.OrdinalIgnoreCase);

        if (nonComparableBusinessState)
        {
            return new StrategyEvidence
            {
                SourcesPresent = true,
                ExactReferenceMatch = referenceMatch,
                NonComparableBusinessState = true,
                NonComparableReason = "REVERSED_FRAUD"
            };
        }

        var exactAmountMatch =
            payment.Amount == bank.Amount &&
            bank.Amount == settlement.Amount;

        var exactDateMatch =
            payment.TransactionDate == bank.TransactionDate &&
            bank.TransactionDate == settlement.TransactionDate;

        return new StrategyEvidence
        {
            SourcesPresent = true,
            ExactReferenceMatch = referenceMatch,
            ExactAmountMatch = exactAmountMatch,
            ExactDateMatch = exactDateMatch,
            AmountMismatch = !exactAmountMatch,
            DateMismatch = !exactDateMatch
        };
    }
}