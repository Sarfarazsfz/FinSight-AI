using FinSight.DataGenerator.Generation;
using FinSight.DataGenerator.Models;

namespace FinSight.Tests.Evaluation;

/// <summary>
/// Unit-level tests for the synthetic-data generator's independent
/// ground-truth production. No database, no HTTP -- these exercise
/// TransactionGenerator/SourceRowGenerator/GroundTruthGenerator directly.
///
/// Prior to Phase 2, FinSight.Tests had no project reference to
/// FinSight.DataGenerator, so none of this code had ever been exercised
/// by an automated test.
/// </summary>
[TestFixture]
public sealed class GroundTruthGeneratorTests
{
    [Test]
    public void Generate_CalledTwiceWithSameSeed_ProducesIdenticalGroundTruth()
    {
        var firstRun = GenerateGroundTruth();
        var secondRun = GenerateGroundTruth();

        Assert.That(
            firstRun,
            Has.Count.EqualTo(secondRun.Count));

        for (var index = 0; index < firstRun.Count; index++)
        {
            var first = firstRun[index];
            var second = secondRun[index];

            Assert.That(
                second.TransactionReference,
                Is.EqualTo(first.TransactionReference),
                $"Row {index}: TransactionReference differs across runs.");

            Assert.That(
                second.ScenarioType,
                Is.EqualTo(first.ScenarioType),
                $"Row {index}: ScenarioType differs across runs.");

            Assert.That(
                second.ExpectedStatus,
                Is.EqualTo(first.ExpectedStatus),
                $"Row {index}: ExpectedStatus differs across runs.");

            Assert.That(
                second.ExpectedReasonCode,
                Is.EqualTo(first.ExpectedReasonCode),
                $"Row {index}: ExpectedReasonCode differs across runs.");

            Assert.That(
                second.ExpectedExceptionCategory,
                Is.EqualTo(first.ExpectedExceptionCategory),
                $"Row {index}: ExpectedExceptionCategory differs across runs.");

            Assert.That(
                second.ExpectedPaymentPresent,
                Is.EqualTo(first.ExpectedPaymentPresent),
                $"Row {index}: ExpectedPaymentPresent differs across runs.");

            Assert.That(
                second.ExpectedBankPresent,
                Is.EqualTo(first.ExpectedBankPresent),
                $"Row {index}: ExpectedBankPresent differs across runs.");

            Assert.That(
                second.ExpectedSettlementPresent,
                Is.EqualTo(first.ExpectedSettlementPresent),
                $"Row {index}: ExpectedSettlementPresent differs across runs.");
        }
    }

    [Test]
    public void Generate_ProducesEveryConfiguredScenarioInExactCounts()
    {
        var rows = GenerateGroundTruth();

        Assert.That(
            rows,
            Has.Count.EqualTo(GeneratorConfiguration.TotalLogicalTransactions));

        var countsByScenario =
            rows
                .GroupBy(x => x.ScenarioType)
                .ToDictionary(x => x.Key, x => x.Count());

        Assert.Multiple(() =>
        {
            AssertScenarioCount(
                countsByScenario,
                nameof(ReconciliationScenario.ExactMatch),
                GeneratorConfiguration.ExactMatchCount);

            AssertScenarioCount(
                countsByScenario,
                nameof(ReconciliationScenario.ToleranceMatch),
                GeneratorConfiguration.ToleranceMatchCount);

            AssertScenarioCount(
                countsByScenario,
                nameof(ReconciliationScenario.AmountMismatch),
                GeneratorConfiguration.AmountMismatchCount);

            AssertScenarioCount(
                countsByScenario,
                nameof(ReconciliationScenario.DateMismatch),
                GeneratorConfiguration.DateMismatchCount);

            AssertScenarioCount(
                countsByScenario,
                nameof(ReconciliationScenario.MissingBank),
                GeneratorConfiguration.MissingBankCount);

            AssertScenarioCount(
                countsByScenario,
                nameof(ReconciliationScenario.MissingSettlement),
                GeneratorConfiguration.MissingSettlementCount);

            AssertScenarioCount(
                countsByScenario,
                nameof(ReconciliationScenario.MissingPayment),
                GeneratorConfiguration.MissingPaymentCount);

            AssertScenarioCount(
                countsByScenario,
                nameof(ReconciliationScenario.DuplicatePayment),
                GeneratorConfiguration.DuplicatePaymentCount);

            AssertScenarioCount(
                countsByScenario,
                nameof(ReconciliationScenario.DuplicateBank),
                GeneratorConfiguration.DuplicateBankCount);

            AssertScenarioCount(
                countsByScenario,
                nameof(ReconciliationScenario.DuplicateSettlement),
                GeneratorConfiguration.DuplicateSettlementCount);

            AssertScenarioCount(
                countsByScenario,
                nameof(ReconciliationScenario.UnresolvedReversedFraud),
                GeneratorConfiguration.UnresolvedCount);
        });

        Assert.That(
            countsByScenario.Values.Sum(),
            Is.EqualTo(GeneratorConfiguration.TotalLogicalTransactions));
    }

    [Test]
    public void Generate_MissingPaymentRows_HaveCorrectExpectedShape()
    {
        var rows = GenerateGroundTruth();

        var missingPaymentRows =
            rows
                .Where(
                    x => x.ScenarioType ==
                        nameof(ReconciliationScenario.MissingPayment))
                .ToList();

        Assert.That(
            missingPaymentRows,
            Has.Count.EqualTo(GeneratorConfiguration.MissingPaymentCount));

        Assert.That(
            missingPaymentRows,
            Has.All.Matches<GroundTruthRow>(
                row =>
                    row.ExpectedStatus == "Missing" &&
                    row.ExpectedReasonCode == "SOURCE_ABSENT_PAYMENT" &&
                    row.ExpectedExceptionCategory == "MissingRecord" &&
                    row.ExpectedPaymentPresent == false &&
                    row.ExpectedBankPresent == true &&
                    row.ExpectedSettlementPresent == true));
    }

    private static void AssertScenarioCount(
        IReadOnlyDictionary<string, int> countsByScenario,
        string scenarioName,
        int expectedCount)
    {
        Assert.That(
            countsByScenario.TryGetValue(scenarioName, out var actualCount),
            Is.True,
            $"Ground truth contains no rows for scenario '{scenarioName}'.");

        Assert.That(
            actualCount,
            Is.EqualTo(expectedCount),
            $"Scenario '{scenarioName}' expected {expectedCount} rows " +
            $"but found {actualCount}.");
    }

    private static IReadOnlyList<GroundTruthRow> GenerateGroundTruth()
    {
        var plannedTransactions =
            new TransactionGenerator().Generate();

        return new GroundTruthGenerator().Generate(
            plannedTransactions);
    }
}
