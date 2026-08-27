namespace FinSight.DataGenerator.Models;

public enum ReconciliationScenario
{
    ExactMatch,

    ToleranceMatch,

    AmountMismatch,

    DateMismatch,

    MissingBank,

    MissingSettlement,

    MissingPayment,

    DuplicatePayment,

    DuplicateBank,

    DuplicateSettlement,

    UnresolvedReversedFraud
}