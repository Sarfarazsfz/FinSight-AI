namespace FinSight.DataGenerator.Models;

public sealed class GeneratorPlan
{
    public IReadOnlyList<
        (ReconciliationScenario Scenario, int Count)> Scenarios
        =>
        new[]
        {
            (
                ReconciliationScenario.ExactMatch,
                GeneratorConfiguration.ExactMatchCount
            ),

            (
                ReconciliationScenario.ToleranceMatch,
                GeneratorConfiguration.ToleranceMatchCount
            ),

            (
                ReconciliationScenario.AmountMismatch,
                GeneratorConfiguration.AmountMismatchCount
            ),

            (
                ReconciliationScenario.DateMismatch,
                GeneratorConfiguration.DateMismatchCount
            ),

            (
                ReconciliationScenario.MissingBank,
                GeneratorConfiguration.MissingBankCount
            ),

            (
                ReconciliationScenario.MissingSettlement,
                GeneratorConfiguration.MissingSettlementCount
            ),

            (
                ReconciliationScenario.MissingPayment,
                GeneratorConfiguration.MissingPaymentCount
            ),

            (
                ReconciliationScenario.DuplicatePayment,
                GeneratorConfiguration.DuplicatePaymentCount
            ),

            (
                ReconciliationScenario.DuplicateBank,
                GeneratorConfiguration.DuplicateBankCount
            ),

            (
                ReconciliationScenario.DuplicateSettlement,
                GeneratorConfiguration.DuplicateSettlementCount
            ),

            (
                ReconciliationScenario.UnresolvedReversedFraud,
                GeneratorConfiguration.UnresolvedCount
            )
        };

    public int TotalScenarioUnits =>
        Scenarios.Sum(x => x.Count);
}