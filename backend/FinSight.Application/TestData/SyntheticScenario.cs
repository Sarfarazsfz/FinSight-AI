namespace FinSight.Application.TestData;

/// <summary>
/// Mirrors the reconciliation scenarios that the engine can handle.
/// Defined here (Application layer) so the parametrised generator and
/// the API controller can share it without referencing the CLI project.
/// </summary>
internal enum SyntheticScenario
{
    ExactMatch,
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
