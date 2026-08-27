using System.Globalization;
using System.Text.RegularExpressions;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Ingestion;

namespace FinSight.Infrastructure.Ingestion;

public sealed class BatchIngestionValidator : IBatchIngestionValidator
{
    private static readonly Regex PaymentIdPattern =
        new(@"^PAY-\d{6}$", RegexOptions.Compiled);

    private static readonly Regex BankIdPattern =
        new(@"^BANK-\d{6}$", RegexOptions.Compiled);

    private static readonly Regex SettlementIdPattern =
        new(@"^SET-\d{6}$", RegexOptions.Compiled);

    private static readonly Regex TransactionReferencePattern =
        new(@"^TXN-\d{4}$", RegexOptions.Compiled);

    private static readonly Regex DatePattern =
        new(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled);

    public BatchIngestionValidationResult ValidatePayments(
        IReadOnlyList<PaymentCsvRow> rows)
    {
        var errors = new List<IngestionValidationError>();

        var duplicateSourceIds =
            FindDuplicateValues(
                rows.Select(x => x.PaymentRecordId));

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var rowNumber = index + 2;

            ValidateRequired(
                errors,
                "Payment",
                rowNumber,
                "payment_record_id",
                row.PaymentRecordId);

            ValidateRequired(
                errors,
                "Payment",
                rowNumber,
                "transaction_reference",
                row.TransactionReference);

            ValidateRequired(
                errors,
                "Payment",
                rowNumber,
                "amount",
                row.Amount);

            ValidateRequired(
                errors,
                "Payment",
                rowNumber,
                "currency",
                row.Currency);

            ValidateRequired(
                errors,
                "Payment",
                rowNumber,
                "transaction_date",
                row.TransactionDate);

            ValidateRequired(
                errors,
                "Payment",
                rowNumber,
                "payment_status",
                row.PaymentStatus);

            if (!string.IsNullOrWhiteSpace(row.PaymentRecordId) &&
                !PaymentIdPattern.IsMatch(row.PaymentRecordId.Trim()))
            {
                AddError(
                    errors,
                    "Payment",
                    rowNumber,
                    "payment_record_id",
                    "Must match PAY-000001 style.");
            }

            if (duplicateSourceIds.Contains(
                    row.PaymentRecordId.Trim()))
            {
                AddError(
                    errors,
                    "Payment",
                    rowNumber,
                    "payment_record_id",
                    "Source record identifier must be unique within the Payment file.");
            }

            ValidateTransactionReference(
                errors,
                "Payment",
                rowNumber,
                row.TransactionReference);

            ValidateAmount(
                errors,
                "Payment",
                rowNumber,
                row.Amount);

            ValidateCurrency(
                errors,
                "Payment",
                rowNumber,
                row.Currency);

            ValidateDate(
                errors,
                "Payment",
                rowNumber,
                row.TransactionDate);
        }

        return new BatchIngestionValidationResult
        {
            Errors = errors
        };
    }

    public BatchIngestionValidationResult ValidateBank(
        IReadOnlyList<BankCsvRow> rows)
    {
        var errors = new List<IngestionValidationError>();

        var duplicateSourceIds =
            FindDuplicateValues(
                rows.Select(x => x.BankRecordId));

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var rowNumber = index + 2;

            ValidateRequired(
                errors,
                "Bank",
                rowNumber,
                "bank_record_id",
                row.BankRecordId);

            ValidateRequired(
                errors,
                "Bank",
                rowNumber,
                "transaction_reference",
                row.TransactionReference);

            ValidateRequired(
                errors,
                "Bank",
                rowNumber,
                "amount",
                row.Amount);

            ValidateRequired(
                errors,
                "Bank",
                rowNumber,
                "currency",
                row.Currency);

            ValidateRequired(
                errors,
                "Bank",
                rowNumber,
                "transaction_date",
                row.TransactionDate);

            ValidateRequired(
                errors,
                "Bank",
                rowNumber,
                "bank_status",
                row.BankStatus);

            if (!string.IsNullOrWhiteSpace(row.BankRecordId) &&
                !BankIdPattern.IsMatch(row.BankRecordId.Trim()))
            {
                AddError(
                    errors,
                    "Bank",
                    rowNumber,
                    "bank_record_id",
                    "Must match BANK-000001 style.");
            }

            if (duplicateSourceIds.Contains(
                    row.BankRecordId.Trim()))
            {
                AddError(
                    errors,
                    "Bank",
                    rowNumber,
                    "bank_record_id",
                    "Source record identifier must be unique within the Bank file.");
            }

            ValidateTransactionReference(
                errors,
                "Bank",
                rowNumber,
                row.TransactionReference);

            ValidateAmount(
                errors,
                "Bank",
                rowNumber,
                row.Amount);

            ValidateCurrency(
                errors,
                "Bank",
                rowNumber,
                row.Currency);

            ValidateDate(
                errors,
                "Bank",
                rowNumber,
                row.TransactionDate);
        }

        return new BatchIngestionValidationResult
        {
            Errors = errors
        };
    }

    public BatchIngestionValidationResult ValidateSettlements(
        IReadOnlyList<SettlementCsvRow> rows)
    {
        var errors = new List<IngestionValidationError>();

        var duplicateSourceIds =
            FindDuplicateValues(
                rows.Select(x => x.SettlementRecordId));

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var rowNumber = index + 2;

            ValidateRequired(
                errors,
                "Settlement",
                rowNumber,
                "settlement_record_id",
                row.SettlementRecordId);

            ValidateRequired(
                errors,
                "Settlement",
                rowNumber,
                "transaction_reference",
                row.TransactionReference);

            ValidateRequired(
                errors,
                "Settlement",
                rowNumber,
                "amount",
                row.Amount);

            ValidateRequired(
                errors,
                "Settlement",
                rowNumber,
                "currency",
                row.Currency);

            ValidateRequired(
                errors,
                "Settlement",
                rowNumber,
                "transaction_date",
                row.TransactionDate);

            ValidateRequired(
                errors,
                "Settlement",
                rowNumber,
                "settlement_status",
                row.SettlementStatus);

            if (!string.IsNullOrWhiteSpace(row.SettlementRecordId) &&
                !SettlementIdPattern.IsMatch(
                    row.SettlementRecordId.Trim()))
            {
                AddError(
                    errors,
                    "Settlement",
                    rowNumber,
                    "settlement_record_id",
                    "Must match SET-000001 style.");
            }

            if (duplicateSourceIds.Contains(
                    row.SettlementRecordId.Trim()))
            {
                AddError(
                    errors,
                    "Settlement",
                    rowNumber,
                    "settlement_record_id",
                    "Source record identifier must be unique within the Settlement file.");
            }

            ValidateTransactionReference(
                errors,
                "Settlement",
                rowNumber,
                row.TransactionReference);

            ValidateAmount(
                errors,
                "Settlement",
                rowNumber,
                row.Amount);

            ValidateCurrency(
                errors,
                "Settlement",
                rowNumber,
                row.Currency);

            ValidateDate(
                errors,
                "Settlement",
                rowNumber,
                row.TransactionDate);
        }

        return new BatchIngestionValidationResult
        {
            Errors = errors
        };
    }

    private static void ValidateRequired(
        List<IngestionValidationError> errors,
        string source,
        int rowNumber,
        string field,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(
                errors,
                source,
                rowNumber,
                field,
                "Required value is missing.");
        }
    }

    private static void ValidateTransactionReference(
        List<IngestionValidationError> errors,
        string source,
        int rowNumber,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!TransactionReferencePattern.IsMatch(value.Trim()))
        {
            AddError(
                errors,
                source,
                rowNumber,
                "transaction_reference",
                "Must match TXN-0001 style.");
        }
    }

    private static void ValidateAmount(
        List<IngestionValidationError> errors,
        string source,
        int rowNumber,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!decimal.TryParse(
                value.Trim(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var amount))
        {
            AddError(
                errors,
                source,
                rowNumber,
                "amount",
                "Amount must be a valid decimal number.");
            return;
        }

        if (amount <= 0)
        {
            AddError(
                errors,
                source,
                rowNumber,
                "amount",
                "Amount must be greater than zero.");
            return;
        }

        if (decimal.Round(amount, 2) != amount)
        {
            AddError(
                errors,
                source,
                rowNumber,
                "amount",
                "Amount must have at most two decimal places.");
        }
    }

    private static void ValidateCurrency(
        List<IngestionValidationError> errors,
        string source,
        int rowNumber,
        string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return;
        }

        if (!string.Equals(
                currency.Trim(),
                "INR",
                StringComparison.OrdinalIgnoreCase))
        {
            AddError(
                errors,
                source,
                rowNumber,
                "currency",
                "Currency must be INR.");
        }
    }

    private static void ValidateDate(
        List<IngestionValidationError> errors,
        string source,
        int rowNumber,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var trimmed = value.Trim();

        if (!DatePattern.IsMatch(trimmed) ||
            !DateOnly.TryParseExact(
                trimmed,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            AddError(
                errors,
                source,
                rowNumber,
                "transaction_date",
                "Date must be a valid ISO date in YYYY-MM-DD format.");
        }
    }

    private static HashSet<string> FindDuplicateValues(
        IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(
                value => value.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static void AddError(
        List<IngestionValidationError> errors,
        string source,
        int rowNumber,
        string field,
        string message)
    {
        errors.Add(new IngestionValidationError
        {
            Source = source,
            RowNumber = rowNumber,
            Field = field,
            Message = message
        });
    }
}