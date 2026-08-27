using FinSight.DataGenerator.Models;

namespace FinSight.DataGenerator.Generation;

public sealed class TransactionGenerator
{
    public IReadOnlyList<
        (SourceTransaction Transaction, ReconciliationScenario Scenario)>
        Generate()
    {
        var plan = new GeneratorPlan();

        if (plan.TotalScenarioUnits !=
            GeneratorConfiguration.TotalLogicalTransactions)
        {
            throw new InvalidOperationException(
                "Generator scenario counts do not add up to " +
                $"{GeneratorConfiguration.TotalLogicalTransactions}.");
        }

        var random =
            new Random(GeneratorConfiguration.Seed);

        var generated = new List<
            (SourceTransaction Transaction,
             ReconciliationScenario Scenario)>();

        var sequenceNumber = 1;

        foreach (var scenarioDefinition in plan.Scenarios)
        {
            for (var i = 0;
                 i < scenarioDefinition.Count;
                 i++)
            {
                var transaction =
                    CreateTransaction(
                        sequenceNumber,
                        random);

                generated.Add(
                    (
                        transaction,
                        scenarioDefinition.Scenario
                    ));

                sequenceNumber++;
            }
        }

        if (generated.Count !=
            GeneratorConfiguration.TotalLogicalTransactions)
        {
            throw new InvalidOperationException(
                $"Expected " +
                $"{GeneratorConfiguration.TotalLogicalTransactions} " +
                $"logical transactions but generated " +
                $"{generated.Count}.");
        }

        return generated;
    }

    private static SourceTransaction CreateTransaction(
        int sequenceNumber,
        Random random)
    {
        var amount =
            GeneratorConfiguration.DefaultAmount +
            (random.Next(1, 1000) * 10);

        var baseDate =
            new DateOnly(2026, 8, 1)
                .AddDays(random.Next(0, 30));

        return new SourceTransaction
        {
            SequenceNumber = sequenceNumber,

            TransactionReference =
                $"TXN-{sequenceNumber:0000}",

            BaseAmount =
                decimal.Round(amount, 2),

            BaseDate =
                baseDate,

            Currency = "INR",

            PaymentStatus = "COMPLETED",

            BankStatus = "CLEARED",

            SettlementStatus = "SETTLED"
        };
    }
}