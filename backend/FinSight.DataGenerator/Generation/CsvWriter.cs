using System.Globalization;
using System.Text;
using FinSight.Application.Evaluation;
using FinSight.DataGenerator.Models;

namespace FinSight.DataGenerator.Generation;

public sealed class CsvWriter
{
    public void WriteAll(
        GeneratedSourceRows sourceRows,
        IReadOnlyList<GroundTruthRow> groundTruthRows,
        string outputDirectory)
    {
        if (sourceRows is null)
        {
            throw new ArgumentNullException(nameof(sourceRows));
        }

        if (groundTruthRows is null)
        {
            throw new ArgumentNullException(nameof(groundTruthRows));
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException(
                "Output directory is required.",
                nameof(outputDirectory));
        }

        Directory.CreateDirectory(outputDirectory);

        WritePayments(
            sourceRows.Payments,
            Path.Combine(outputDirectory, "payments.csv"));

        WriteBanks(
            sourceRows.Banks,
            Path.Combine(outputDirectory, "bank.csv"));

        WriteSettlements(
            sourceRows.Settlements,
            Path.Combine(outputDirectory, "settlements.csv"));

        WriteGroundTruth(
            groundTruthRows,
            Path.Combine(outputDirectory, "ground-truth.csv"));
    }

    private static void WritePayments(
        IReadOnlyList<PaymentSourceRow> rows,
        string filePath)
    {
        using var writer = CreateWriter(filePath);

        writer.WriteLine(
            "payment_record_id,transaction_reference,amount,currency,transaction_date,payment_status");

        foreach (var row in rows)
        {
            writer.WriteLine(
                string.Join(
                    ",",
                    Escape(row.PaymentRecordId),
                    Escape(row.TransactionReference),
                    row.Amount.ToString(
                        "0.00",
                        CultureInfo.InvariantCulture),
                    Escape(row.Currency),
                    row.TransactionDate.ToString(
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture),
                    Escape(row.PaymentStatus)));
        }
    }

    private static void WriteBanks(
        IReadOnlyList<BankSourceRow> rows,
        string filePath)
    {
        using var writer = CreateWriter(filePath);

        writer.WriteLine(
            "bank_record_id,transaction_reference,amount,currency,transaction_date,bank_status");

        foreach (var row in rows)
        {
            writer.WriteLine(
                string.Join(
                    ",",
                    Escape(row.BankRecordId),
                    Escape(row.TransactionReference),
                    row.Amount.ToString(
                        "0.00",
                        CultureInfo.InvariantCulture),
                    Escape(row.Currency),
                    row.TransactionDate.ToString(
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture),
                    Escape(row.BankStatus)));
        }
    }

    private static void WriteSettlements(
        IReadOnlyList<SettlementSourceRow> rows,
        string filePath)
    {
        using var writer = CreateWriter(filePath);

        writer.WriteLine(
            "settlement_record_id,transaction_reference,amount,currency,transaction_date,settlement_status");

        foreach (var row in rows)
        {
            writer.WriteLine(
                string.Join(
                    ",",
                    Escape(row.SettlementRecordId),
                    Escape(row.TransactionReference),
                    row.Amount.ToString(
                        "0.00",
                        CultureInfo.InvariantCulture),
                    Escape(row.Currency),
                    row.TransactionDate.ToString(
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture),
                    Escape(row.SettlementStatus)));
        }
    }

    private static void WriteGroundTruth(
        IReadOnlyList<GroundTruthRow> rows,
        string filePath)
    {
        using var writer = CreateWriter(filePath);

        writer.WriteLine(
            "transaction_reference,scenario_type,expected_status,expected_reason_code,expected_exception_category,expected_payment_present,expected_bank_present,expected_settlement_present,expected_amount_relationship,expected_date_relationship");

        foreach (var row in rows)
        {
            writer.WriteLine(
                string.Join(
                    ",",
                    Escape(row.TransactionReference),
                    Escape(row.ScenarioType),
                    Escape(row.ExpectedStatus),
                    Escape(row.ExpectedReasonCode),
                    Escape(row.ExpectedExceptionCategory),
                    row.ExpectedPaymentPresent
                        ? "true"
                        : "false",
                    row.ExpectedBankPresent
                        ? "true"
                        : "false",
                    row.ExpectedSettlementPresent
                        ? "true"
                        : "false",
                    Escape(row.ExpectedAmountRelationship),
                    Escape(row.ExpectedDateRelationship)));
        }
    }

    private static StreamWriter CreateWriter(
        string filePath)
    {
        return new StreamWriter(
            new FileStream(
                filePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read),
            new UTF8Encoding(false));
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (!value.Contains(',') &&
            !value.Contains('"') &&
            !value.Contains('\r') &&
            !value.Contains('\n'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}