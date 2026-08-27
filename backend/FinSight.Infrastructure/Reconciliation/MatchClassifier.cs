using FinSight.Application.DTOs.Reconciliation;
using FinSight.Domain.Enums;

namespace FinSight.Infrastructure.Reconciliation;

public sealed class MatchClassifier
{
    public ClassificationDecision Classify(
        ReconciliationEvidence evidence,
        StrategyEvidence exactEvidence,
        StrategyEvidence toleranceEvidence)
    {
        // Priority 1: Duplicate
        if (evidence.HasAnyDuplicate)
        {
            return ClassifyDuplicate(evidence);
        }

        // Priority 2: Missing
        if (!evidence.HasPayment ||
            !evidence.HasBank ||
            !evidence.HasSettlement)
        {
            return ClassifyMissing(evidence);
        }

        // Priority 3: Exact deterministic match
        if (exactEvidence.SourcesPresent &&
            exactEvidence.ExactReferenceMatch &&
            exactEvidence.ExactAmountMatch &&
            exactEvidence.ExactDateMatch &&
            !exactEvidence.NonComparableBusinessState)
        {
            return new ClassificationDecision
            {
                Status = MatchStatus.Matched,
                ReasonCode = ReconciliationReasonCode.EXACT_MATCH,
                StrategyUsed = "StrategyOne_ExactReferenceMatch",
                ExceptionCategory = null
            };
        }

        // Priority 4: Tolerance match
        // AmountTolerance = 0.00
        // DateToleranceHours = 24
        //
        // Exact match has already been evaluated above, therefore
        // this branch represents a valid tolerance-based match.
        if (toleranceEvidence.SourcesPresent &&
            toleranceEvidence.ExactReferenceMatch &&
            toleranceEvidence.AmountWithinTolerance &&
            toleranceEvidence.DateWithinTolerance &&
            !toleranceEvidence.ExactAmountMatch ||
            toleranceEvidence.SourcesPresent &&
            toleranceEvidence.ExactReferenceMatch &&
            toleranceEvidence.AmountWithinTolerance &&
            toleranceEvidence.DateWithinTolerance &&
            !toleranceEvidence.ExactDateMatch)
        {
            return new ClassificationDecision
            {
                Status = MatchStatus.Matched,
                ReasonCode = ReconciliationReasonCode.TOLERANCE_MATCH,
                StrategyUsed = "StrategyTwo_AmountDateToleranceMatch",
                ExceptionCategory = null
            };
        }

        // Priority 5: Amount mismatch
        if (toleranceEvidence.AmountMismatch ||
            exactEvidence.AmountMismatch)
        {
            return new ClassificationDecision
            {
                Status = MatchStatus.Mismatched,
                ReasonCode = ReconciliationReasonCode.AMOUNT_MISMATCH,
                StrategyUsed = "StrategyTwo_AmountDateToleranceMatch",
                ExceptionCategory = ExceptionCategory.AmountMismatch
            };
        }

        // Priority 6: Date mismatch
        if (toleranceEvidence.DateMismatch ||
            exactEvidence.DateMismatch)
        {
            return new ClassificationDecision
            {
                Status = MatchStatus.Mismatched,
                ReasonCode = ReconciliationReasonCode.DATE_OUT_OF_TOLERANCE,
                StrategyUsed = "StrategyTwo_AmountDateToleranceMatch",
                ExceptionCategory = ExceptionCategory.DateMismatch
            };
        }

        // Priority 7: Unresolved
        if (exactEvidence.NonComparableBusinessState ||
            toleranceEvidence.NonComparableBusinessState)
        {
            return new ClassificationDecision
            {
                Status = MatchStatus.Unresolved,
                ReasonCode = ReconciliationReasonCode.UNRESOLVED,
                StrategyUsed = null,
                ExceptionCategory = ExceptionCategory.Unresolved
            };
        }

        return new ClassificationDecision
        {
            Status = MatchStatus.Unresolved,
            ReasonCode = ReconciliationReasonCode.UNRESOLVED,
            StrategyUsed = null,
            ExceptionCategory = ExceptionCategory.Unresolved
        };
    }

    private static ClassificationDecision ClassifyDuplicate(
        ReconciliationEvidence evidence)
    {
        if (evidence.HasDuplicatePayment)
        {
            return new ClassificationDecision
            {
                Status = MatchStatus.Duplicate,
                ReasonCode =
                    ReconciliationReasonCode.DUPLICATE_PAYMENT,
                StrategyUsed = null,
                ExceptionCategory =
                    ExceptionCategory.DuplicateRecord
            };
        }

        if (evidence.HasDuplicateBank)
        {
            return new ClassificationDecision
            {
                Status = MatchStatus.Duplicate,
                ReasonCode =
                    ReconciliationReasonCode.DUPLICATE_BANK,
                StrategyUsed = null,
                ExceptionCategory =
                    ExceptionCategory.DuplicateRecord
            };
        }

        return new ClassificationDecision
        {
            Status = MatchStatus.Duplicate,
            ReasonCode =
                ReconciliationReasonCode.DUPLICATE_SETTLEMENT,
            StrategyUsed = null,
            ExceptionCategory =
                ExceptionCategory.DuplicateRecord
        };
    }

    private static ClassificationDecision ClassifyMissing(
        ReconciliationEvidence evidence)
    {
        if (!evidence.HasPayment)
        {
            return new ClassificationDecision
            {
                Status = MatchStatus.Missing,
                ReasonCode =
                    ReconciliationReasonCode.SOURCE_ABSENT_PAYMENT,
                StrategyUsed = null,
                ExceptionCategory =
                    ExceptionCategory.MissingRecord
            };
        }

        if (!evidence.HasBank)
        {
            return new ClassificationDecision
            {
                Status = MatchStatus.Missing,
                ReasonCode =
                    ReconciliationReasonCode.SOURCE_ABSENT_BANK,
                StrategyUsed = null,
                ExceptionCategory =
                    ExceptionCategory.MissingRecord
            };
        }

        return new ClassificationDecision
        {
            Status = MatchStatus.Missing,
            ReasonCode =
                ReconciliationReasonCode.SOURCE_ABSENT_SETTLEMENT,
            StrategyUsed = null,
            ExceptionCategory =
                ExceptionCategory.MissingRecord
        };
    }
}