namespace FinSight.Application.TestData;

/// <summary>
/// Controls what kind of reconciliation scenarios the generator injects.
/// </summary>
public enum GenerationMode
{
    /// <summary>All records reconcile cleanly — expected match rate 100 %.</summary>
    Clean = 0,

    /// <summary>Bank/Settlement amounts differ from Payment; triggers Mismatched/AMOUNT_MISMATCH.</summary>
    AmountMismatch = 1,

    /// <summary>Bank/Settlement dates are beyond tolerance; triggers Mismatched/DATE_OUT_OF_TOLERANCE.</summary>
    DateMismatch = 2,

    /// <summary>Bank record absent; triggers Missing/SOURCE_ABSENT_BANK.</summary>
    MissingBank = 3,

    /// <summary>Settlement record absent; triggers Missing/SOURCE_ABSENT_SETTLEMENT.</summary>
    MissingSettlement = 4,

    /// <summary>Payment record absent; orphan bank+settlement; triggers Missing/SOURCE_ABSENT_PAYMENT.</summary>
    MissingPayment = 5,

    /// <summary>Duplicate Payment/Bank/Settlement records present for the same transaction reference.</summary>
    Duplicate = 6,

    /// <summary>Bank shows REVERSED_FRAUD status; triggers Unresolved classification.</summary>
    Unresolved = 7,

    /// <summary>
    /// Proportional mix of all corruption types scaled by intensity —
    /// mirrors the shape of the canonical evaluator scenario.
    /// </summary>
    Mixed = 8,

    /// <summary>
    /// Randomly selects 3–5 corruption operators from the full set.
    /// Reproducible given the same seed; different seeds produce
    /// different corruption mixes.
    /// </summary>
    RandomChaos = 9
}
