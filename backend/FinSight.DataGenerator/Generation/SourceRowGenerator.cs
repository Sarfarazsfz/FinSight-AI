using FinSight.DataGenerator.Models;

namespace FinSight.DataGenerator.Generation;

public sealed class SourceRowGenerator
{
    public GeneratedSourceRows Generate(
        IReadOnlyList<(
            SourceTransaction Transaction,
            ReconciliationScenario Scenario)> plannedTransactions)
    {
        var result = new GeneratedSourceRows();

        foreach (var item in plannedTransactions)
        {
            AddRows(
                result,
                item.Transaction,
                item.Scenario);
        }

        ValidateRawRowCounts(result);

        return result;
    }

    private static void AddRows(
        GeneratedSourceRows result,
        SourceTransaction transaction,
        ReconciliationScenario scenario)
    {
        var baseAmount = transaction.BaseAmount;
        var baseDate = transaction.BaseDate;

        switch (scenario)
        {
            case ReconciliationScenario.ExactMatch:
                AddPayment(
                    result,
                    transaction,
                    "COMPLETED",
                    baseAmount,
                    baseDate);

                AddBank(
                    result,
                    transaction,
                    "CLEARED",
                    baseAmount,
                    baseDate);

                AddSettlement(
                    result,
                    transaction,
                    "SETTLED",
                    baseAmount,
                    baseDate);

                break;

            case ReconciliationScenario.ToleranceMatch:
                AddPayment(
                    result,
                    transaction,
                    "COMPLETED",
                    baseAmount,
                    baseDate);

                AddBank(
                    result,
                    transaction,
                    "CLEARED",
                    baseAmount,
                    baseDate.AddDays(1));

                AddSettlement(
                    result,
                    transaction,
                    "SETTLED",
                    baseAmount,
                    baseDate.AddDays(1));

                break;

            case ReconciliationScenario.AmountMismatch:
                AddPayment(
                    result,
                    transaction,
                    "COMPLETED",
                    baseAmount,
                    baseDate);

                AddBank(
                    result,
                    transaction,
                    "CLEARED",
                    baseAmount - 10.00m,
                    baseDate);

                AddSettlement(
                    result,
                    transaction,
                    "SETTLED",
                    baseAmount - 10.00m,
                    baseDate);

                break;

            case ReconciliationScenario.DateMismatch:
                AddPayment(
                    result,
                    transaction,
                    "COMPLETED",
                    baseAmount,
                    baseDate);

                AddBank(
                    result,
                    transaction,
                    "CLEARED",
                    baseAmount,
                    baseDate.AddDays(2));

                AddSettlement(
                    result,
                    transaction,
                    "SETTLED",
                    baseAmount,
                    baseDate.AddDays(2));

                break;

            case ReconciliationScenario.MissingBank:
                AddPayment(
                    result,
                    transaction,
                    "COMPLETED",
                    baseAmount,
                    baseDate);

                // Bank intentionally omitted.

                AddSettlement(
                    result,
                    transaction,
                    "SETTLED",
                    baseAmount,
                    baseDate);

                break;

            case ReconciliationScenario.MissingSettlement:
                AddPayment(
                    result,
                    transaction,
                    "COMPLETED",
                    baseAmount,
                    baseDate);

                AddBank(
                    result,
                    transaction,
                    "CLEARED",
                    baseAmount,
                    baseDate);

                // Settlement intentionally omitted.

                break;

            case ReconciliationScenario.MissingPayment:
                // Payment intentionally omitted.

                AddBank(
                    result,
                    transaction,
                    "CLEARED",
                    baseAmount,
                    baseDate);

                AddSettlement(
                    result,
                    transaction,
                    "SETTLED",
                    baseAmount,
                    baseDate);

                break;

            case ReconciliationScenario.DuplicatePayment:
                // First payment uses normal sequence ID.
                AddPayment(
                    result,
                    transaction,
                    "COMPLETED",
                    baseAmount,
                    baseDate);

                // Second payment gets a valid numeric ID above
                // the normal 1-100 range.
                AddPayment(
                    result,
                    transaction,
                    "COMPLETED",
                    baseAmount,
                    baseDate,
                    duplicate: true);

                AddBank(
                    result,
                    transaction,
                    "CLEARED",
                    baseAmount,
                    baseDate);

                AddSettlement(
                    result,
                    transaction,
                    "SETTLED",
                    baseAmount,
                    baseDate);

                break;

            case ReconciliationScenario.DuplicateBank:
                AddPayment(
                    result,
                    transaction,
                    "COMPLETED",
                    baseAmount,
                    baseDate);

                AddBank(
                    result,
                    transaction,
                    "CLEARED",
                    baseAmount,
                    baseDate);

                AddBank(
                    result,
                    transaction,
                    "CLEARED",
                    baseAmount,
                    baseDate,
                    duplicate: true);

                AddSettlement(
                    result,
                    transaction,
                    "SETTLED",
                    baseAmount,
                    baseDate);

                break;

            case ReconciliationScenario.DuplicateSettlement:
                AddPayment(
                    result,
                    transaction,
                    "COMPLETED",
                    baseAmount,
                    baseDate);

                AddBank(
                    result,
                    transaction,
                    "CLEARED",
                    baseAmount,
                    baseDate);

                AddSettlement(
                    result,
                    transaction,
                    "SETTLED",
                    baseAmount,
                    baseDate);

                AddSettlement(
                    result,
                    transaction,
                    "SETTLED",
                    baseAmount,
                    baseDate,
                    duplicate: true);

                break;

            case ReconciliationScenario.UnresolvedReversedFraud:
                AddPayment(
                    result,
                    transaction,
                    "COMPLETED",
                    baseAmount,
                    baseDate);

                AddBank(
                    result,
                    transaction,
                    "REVERSED_FRAUD",
                    baseAmount,
                    baseDate);

                AddSettlement(
                    result,
                    transaction,
                    "SETTLED",
                    baseAmount,
                    baseDate);

                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(scenario),
                    scenario,
                    "Unsupported reconciliation scenario.");
        }
    }

    private static void AddPayment(
        GeneratedSourceRows result,
        SourceTransaction transaction,
        string status,
        decimal amount,
        DateOnly date,
        bool duplicate = false)
    {
        var recordNumber =
            duplicate
                ? 100 + transaction.SequenceNumber
                : transaction.SequenceNumber;

        var recordId =
            $"PAY-{recordNumber:000000}";

        result.Payments.Add(
            new PaymentSourceRow(
                recordId,
                transaction.TransactionReference,
                decimal.Round(amount, 2),
                "INR",
                date,
                status));
    }

    private static void AddBank(
        GeneratedSourceRows result,
        SourceTransaction transaction,
        string status,
        decimal amount,
        DateOnly date,
        bool duplicate = false)
    {
        var recordNumber =
            duplicate
                ? 100 + transaction.SequenceNumber
                : transaction.SequenceNumber;

        var recordId =
            $"BANK-{recordNumber:000000}";

        result.Banks.Add(
            new BankSourceRow(
                recordId,
                transaction.TransactionReference,
                decimal.Round(amount, 2),
                "INR",
                date,
                status));
    }

    private static void AddSettlement(
        GeneratedSourceRows result,
        SourceTransaction transaction,
        string status,
        decimal amount,
        DateOnly date,
        bool duplicate = false)
    {
        var recordNumber =
            duplicate
                ? 100 + transaction.SequenceNumber
                : transaction.SequenceNumber;

        var recordId =
            $"SET-{recordNumber:000000}";

        result.Settlements.Add(
            new SettlementSourceRow(
                recordId,
                transaction.TransactionReference,
                decimal.Round(amount, 2),
                "INR",
                date,
                status));
    }

    private static void ValidateRawRowCounts(
        GeneratedSourceRows result)
    {
        if (result.Payments.Count !=
            GeneratorConfiguration.ExpectedPaymentRows)
        {
            throw new InvalidOperationException(
                $"Expected " +
                $"{GeneratorConfiguration.ExpectedPaymentRows} " +
                $"Payment rows but generated " +
                $"{result.Payments.Count}.");
        }

        if (result.Banks.Count !=
            GeneratorConfiguration.ExpectedBankRows)
        {
            throw new InvalidOperationException(
                $"Expected " +
                $"{GeneratorConfiguration.ExpectedBankRows} " +
                $"Bank rows but generated " +
                $"{result.Banks.Count}.");
        }

        if (result.Settlements.Count !=
            GeneratorConfiguration.ExpectedSettlementRows)
        {
            throw new InvalidOperationException(
                $"Expected " +
                $"{GeneratorConfiguration.ExpectedSettlementRows} " +
                $"Settlement rows but generated " +
                $"{result.Settlements.Count}.");
        }

        var total =
            result.Payments.Count +
            result.Banks.Count +
            result.Settlements.Count;

        if (total !=
            GeneratorConfiguration.ExpectedRawRowCount)
        {
            throw new InvalidOperationException(
                $"Expected " +
                $"{GeneratorConfiguration.ExpectedRawRowCount} " +
                $"total raw rows but generated {total}.");
        }
    }
}