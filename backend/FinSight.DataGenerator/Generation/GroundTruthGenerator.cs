using FinSight.DataGenerator.Models;

namespace FinSight.DataGenerator.Generation;

public sealed class GroundTruthGenerator
{
    public IReadOnlyList<GroundTruthRow> Generate(
        IReadOnlyList<(
            SourceTransaction Transaction,
            ReconciliationScenario Scenario)> plannedTransactions)
    {
        var rows = new List<GroundTruthRow>();

        foreach (var item in plannedTransactions)
        {
            rows.Add(
                CreateGroundTruthRow(
                    item.Transaction,
                    item.Scenario));
        }

        if (rows.Count !=
            GeneratorConfiguration.TotalLogicalTransactions)
        {
            throw new InvalidOperationException(
                $"Expected " +
                $"{GeneratorConfiguration.TotalLogicalTransactions} " +
                $"ground-truth rows but generated {rows.Count}.");
        }

        return rows;
    }

    private static GroundTruthRow CreateGroundTruthRow(
        SourceTransaction transaction,
        ReconciliationScenario scenario)
    {
        return scenario switch
        {
            ReconciliationScenario.ExactMatch =>
                Create(
                    transaction,
                    scenario,
                    "Matched",
                    "EXACT_MATCH",
                    string.Empty,
                    paymentPresent: true,
                    bankPresent: true,
                    settlementPresent: true,
                    amountRelationship: "Exact",
                    dateRelationship: "Exact"),

            ReconciliationScenario.ToleranceMatch =>
                Create(
                    transaction,
                    scenario,
                    "Matched",
                    "TOLERANCE_MATCH",
                    string.Empty,
                    paymentPresent: true,
                    bankPresent: true,
                    settlementPresent: true,
                    amountRelationship: "Exact",
                    dateRelationship: "+24h"),

            ReconciliationScenario.AmountMismatch =>
                Create(
                    transaction,
                    scenario,
                    "Mismatched",
                    "AMOUNT_MISMATCH",
                    "AmountMismatch",
                    paymentPresent: true,
                    bankPresent: true,
                    settlementPresent: true,
                    amountRelationship: "BankAndSettlementMinus10",
                    dateRelationship: "Exact"),

            ReconciliationScenario.DateMismatch =>
                Create(
                    transaction,
                    scenario,
                    "Mismatched",
                    "DATE_OUT_OF_TOLERANCE",
                    "DateMismatch",
                    paymentPresent: true,
                    bankPresent: true,
                    settlementPresent: true,
                    amountRelationship: "Exact",
                    dateRelationship: "+48h"),

            ReconciliationScenario.MissingBank =>
                Create(
                    transaction,
                    scenario,
                    "Missing",
                    "SOURCE_ABSENT_BANK",
                    "MissingRecord",
                    paymentPresent: true,
                    bankPresent: false,
                    settlementPresent: true,
                    amountRelationship: "NotComparable",
                    dateRelationship: "NotComparable"),

            ReconciliationScenario.MissingSettlement =>
                Create(
                    transaction,
                    scenario,
                    "Missing",
                    "SOURCE_ABSENT_SETTLEMENT",
                    "MissingRecord",
                    paymentPresent: true,
                    bankPresent: true,
                    settlementPresent: false,
                    amountRelationship: "NotComparable",
                    dateRelationship: "NotComparable"),

            ReconciliationScenario.MissingPayment =>
                Create(
                    transaction,
                    scenario,
                    "Missing",
                    "SOURCE_ABSENT_PAYMENT",
                    "MissingRecord",
                    paymentPresent: false,
                    bankPresent: true,
                    settlementPresent: true,
                    amountRelationship: "NotComparable",
                    dateRelationship: "NotComparable"),

            ReconciliationScenario.DuplicatePayment =>
                Create(
                    transaction,
                    scenario,
                    "Duplicate",
                    "DUPLICATE_PAYMENT",
                    "DuplicateRecord",
                    paymentPresent: true,
                    bankPresent: true,
                    settlementPresent: true,
                    amountRelationship: "Exact",
                    dateRelationship: "Exact"),

            ReconciliationScenario.DuplicateBank =>
                Create(
                    transaction,
                    scenario,
                    "Duplicate",
                    "DUPLICATE_BANK",
                    "DuplicateRecord",
                    paymentPresent: true,
                    bankPresent: true,
                    settlementPresent: true,
                    amountRelationship: "Exact",
                    dateRelationship: "Exact"),

            ReconciliationScenario.DuplicateSettlement =>
                Create(
                    transaction,
                    scenario,
                    "Duplicate",
                    "DUPLICATE_SETTLEMENT",
                    "DuplicateRecord",
                    paymentPresent: true,
                    bankPresent: true,
                    settlementPresent: true,
                    amountRelationship: "Exact",
                    dateRelationship: "Exact"),

            ReconciliationScenario.UnresolvedReversedFraud =>
                Create(
                    transaction,
                    scenario,
                    "Unresolved",
                    "UNRESOLVED",
                    "Unresolved",
                    paymentPresent: true,
                    bankPresent: true,
                    settlementPresent: true,
                    amountRelationship: "Exact",
                    dateRelationship: "Exact"),

            _ => throw new ArgumentOutOfRangeException(
                nameof(scenario),
                scenario,
                "Unsupported reconciliation scenario.")
        };
    }

    private static GroundTruthRow Create(
        SourceTransaction transaction,
        ReconciliationScenario scenario,
        string expectedStatus,
        string expectedReasonCode,
        string expectedExceptionCategory,
        bool paymentPresent,
        bool bankPresent,
        bool settlementPresent,
        string amountRelationship,
        string dateRelationship)
    {
        return new GroundTruthRow(
            transaction.TransactionReference,
            scenario.ToString(),
            expectedStatus,
            expectedReasonCode,
            expectedExceptionCategory,
            paymentPresent,
            bankPresent,
            settlementPresent,
            amountRelationship,
            dateRelationship);
    }
}