using CsvHelper;
using CsvHelper.Configuration;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Ingestion;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FinSight.Infrastructure.FileParsing;

/// <summary>
/// Parses the three source CSV files (Payment/Bank/Settlement) into their
/// canonical row DTOs.
///
/// Phase 10 (flexible column mapping): callers are no longer required to
/// use the exact canonical header names. Each schema below has its own
/// small, explicit alias list; an uploaded CSV's actual headers are
/// resolved -- by name, not position -- against that schema's aliases
/// once per file, immediately after the header row is read. Row
/// extraction then reads by the *actual* resolved header text rather
/// than a hardcoded canonical name, which also eliminates a latent
/// case-sensitivity gap the previous implementation had (header
/// existence was checked case-insensitively, but CsvHelper's
/// GetField&lt;string&gt;(name) lookup is case-sensitive).
///
/// Everything downstream of this parser -- the row DTOs,
/// BatchIngestionValidator, BatchIngestionService, and reconciliation --
/// is unaffected: it only ever sees canonical field values.
/// </summary>
public sealed class SourceCsvParser : ISourceCsvParser
{
    private static readonly CsvConfiguration CsvConfiguration = new(
        CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        TrimOptions = TrimOptions.Trim,
        IgnoreBlankLines = true,

        // Header validation/resolution is performed explicitly by this
        // parser (see ResolveColumnMap).
        HeaderValidated = null,

        // Missing/invalid values are handled by the ingestion validation
        // layer.
        MissingFieldFound = null
    };

    // Inserts a separator before an uppercase letter that immediately
    // follows a lowercase letter or digit, so camelCase headers
    // (e.g. "transactionReference") normalize the same way as
    // snake_case ones.
    private static readonly Regex CamelCaseBoundary =
        new(@"(?<=[a-z0-9])(?=[A-Z])", RegexOptions.Compiled);

    // Whitespace and hyphen runs are treated as the same separator as
    // underscores.
    private static readonly Regex SeparatorRun =
        new(@"[\s\-]+", RegexOptions.Compiled);

    private static readonly Regex UnderscoreRun =
        new(@"_+", RegexOptions.Compiled);

    // Alias lists shared by every schema for the fields that appear in
    // all three source files. Declared before the per-schema tables
    // below because static field initializers run in declaration order.
    private static readonly string[] TransactionReferenceAliases =
    {
        "transaction_reference",
        "transactionReference",
        "txn_reference",
        "txn_ref",
        "reference_no",
        "reference_number",
        "payment_reference"
    };

    private static readonly string[] AmountAliases =
    {
        "amount",
        "transaction_amount",
        "amount_paid",
        "paid_amount"
    };

    private static readonly string[] CurrencyAliases =
    {
        "currency",
        "currency_code",
        "ccy"
    };

    private static readonly string[] TransactionDateAliases =
    {
        "transaction_date",
        "transactionDate",
        "txn_date",
        "transaction_time"
    };

    private static readonly string[] PaymentRequiredFields =
    {
        "payment_record_id",
        "transaction_reference",
        "amount",
        "currency",
        "transaction_date",
        "payment_status"
    };

    private static readonly string[] BankRequiredFields =
    {
        "bank_record_id",
        "transaction_reference",
        "amount",
        "currency",
        "transaction_date",
        "bank_status"
    };

    private static readonly string[] SettlementRequiredFields =
    {
        "settlement_record_id",
        "transaction_reference",
        "amount",
        "currency",
        "transaction_date",
        "settlement_status"
    };

    private static readonly IReadOnlyDictionary<string, string[]> PaymentAliases =
        new Dictionary<string, string[]>
        {
            ["payment_record_id"] = new[]
            {
                "payment_record_id", "payment_id", "payment_reference_id"
            },
            ["transaction_reference"] = TransactionReferenceAliases,
            ["amount"] = AmountAliases,
            ["currency"] = CurrencyAliases,
            ["transaction_date"] = TransactionDateAliases,
            ["payment_status"] = new[]
            {
                "payment_status", "paymentStatus", "payment_state"
            }
        };

    private static readonly IReadOnlyDictionary<string, string[]> BankAliases =
        new Dictionary<string, string[]>
        {
            ["bank_record_id"] = new[] { "bank_record_id", "bank_id" },
            ["transaction_reference"] = TransactionReferenceAliases,
            ["amount"] = AmountAliases,
            ["currency"] = CurrencyAliases,
            ["transaction_date"] = TransactionDateAliases,
            ["bank_status"] = new[]
            {
                "bank_status", "bankStatus", "bank_state"
            }
        };

    private static readonly IReadOnlyDictionary<string, string[]> SettlementAliases =
        new Dictionary<string, string[]>
        {
            ["settlement_record_id"] = new[]
            {
                "settlement_record_id", "settlement_id"
            },
            ["transaction_reference"] = TransactionReferenceAliases,
            ["amount"] = AmountAliases,
            ["currency"] = CurrencyAliases,
            ["transaction_date"] = TransactionDateAliases,
            ["settlement_status"] = new[]
            {
                "settlement_status", "settlementStatus", "settlement_state"
            }
        };

    // Built once from the alias tables above: normalized header key ->
    // the single canonical field it resolves to. Constructed eagerly so
    // an alias-table misconfiguration (the same normalized alias listed
    // under two different canonical fields in one schema) fails loudly
    // at class initialization rather than silently at ingestion time.
    private static readonly IReadOnlyDictionary<string, string> PaymentAliasLookup =
        BuildAliasLookup("Payment", PaymentAliases);

    private static readonly IReadOnlyDictionary<string, string> BankAliasLookup =
        BuildAliasLookup("Bank", BankAliases);

    private static readonly IReadOnlyDictionary<string, string> SettlementAliasLookup =
        BuildAliasLookup("Settlement", SettlementAliases);

    public Task<IReadOnlyList<PaymentCsvRow>> ParsePaymentsAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<PaymentCsvRow>>(
            ParsePayments(stream, cancellationToken));
    }

    public Task<IReadOnlyList<BankCsvRow>> ParseBankAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<BankCsvRow>>(
            ParseBank(stream, cancellationToken));
    }

    public Task<IReadOnlyList<SettlementCsvRow>> ParseSettlementsAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<SettlementCsvRow>>(
            ParseSettlements(stream, cancellationToken));
    }

    private static List<PaymentCsvRow> ParsePayments(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var reader = CreateReader(stream);
        using var csv = new CsvReader(reader, CsvConfiguration);

        var columns = ResolveColumnMap(
            csv,
            PaymentAliasLookup,
            PaymentRequiredFields);

        var records = new List<PaymentCsvRow>();

        while (csv.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsBlankRecord(csv))
            {
                continue;
            }

            records.Add(new PaymentCsvRow
            {
                PaymentRecordId = csv.GetField<string>(columns["payment_record_id"])?.Trim()
                    ?? string.Empty,

                TransactionReference = csv.GetField<string>(columns["transaction_reference"])?.Trim()
                    ?? string.Empty,

                Amount = csv.GetField<string>(columns["amount"])?.Trim()
                    ?? string.Empty,

                Currency = csv.GetField<string>(columns["currency"])?.Trim()
                    ?? string.Empty,

                TransactionDate = csv.GetField<string>(columns["transaction_date"])?.Trim()
                    ?? string.Empty,

                PaymentStatus = csv.GetField<string>(columns["payment_status"])?.Trim()
                    ?? string.Empty
            });
        }

        return records;
    }

    private static List<BankCsvRow> ParseBank(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var reader = CreateReader(stream);
        using var csv = new CsvReader(reader, CsvConfiguration);

        var columns = ResolveColumnMap(
            csv,
            BankAliasLookup,
            BankRequiredFields);

        var records = new List<BankCsvRow>();

        while (csv.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsBlankRecord(csv))
            {
                continue;
            }

            records.Add(new BankCsvRow
            {
                BankRecordId = csv.GetField<string>(columns["bank_record_id"])?.Trim()
                    ?? string.Empty,

                TransactionReference = csv.GetField<string>(columns["transaction_reference"])?.Trim()
                    ?? string.Empty,

                Amount = csv.GetField<string>(columns["amount"])?.Trim()
                    ?? string.Empty,

                Currency = csv.GetField<string>(columns["currency"])?.Trim()
                    ?? string.Empty,

                TransactionDate = csv.GetField<string>(columns["transaction_date"])?.Trim()
                    ?? string.Empty,

                BankStatus = csv.GetField<string>(columns["bank_status"])?.Trim()
                    ?? string.Empty
            });
        }

        return records;
    }

    private static List<SettlementCsvRow> ParseSettlements(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var reader = CreateReader(stream);
        using var csv = new CsvReader(reader, CsvConfiguration);

        var columns = ResolveColumnMap(
            csv,
            SettlementAliasLookup,
            SettlementRequiredFields);

        var records = new List<SettlementCsvRow>();

        while (csv.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsBlankRecord(csv))
            {
                continue;
            }

            records.Add(new SettlementCsvRow
            {
                SettlementRecordId = csv.GetField<string>(columns["settlement_record_id"])?.Trim()
                    ?? string.Empty,

                TransactionReference = csv.GetField<string>(columns["transaction_reference"])?.Trim()
                    ?? string.Empty,

                Amount = csv.GetField<string>(columns["amount"])?.Trim()
                    ?? string.Empty,

                Currency = csv.GetField<string>(columns["currency"])?.Trim()
                    ?? string.Empty,

                TransactionDate = csv.GetField<string>(columns["transaction_date"])?.Trim()
                    ?? string.Empty,

                SettlementStatus = csv.GetField<string>(columns["settlement_status"])?.Trim()
                    ?? string.Empty
            });
        }

        return records;
    }

    private static StreamReader CreateReader(Stream stream)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (!stream.CanRead)
        {
            throw new ArgumentException(
                "The provided stream is not readable.",
                nameof(stream));
        }

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        return new StreamReader(
            stream,
            leaveOpen: true);
    }

    /// <summary>
    /// Resolves every required canonical field to exactly one actual CSV
    /// header, using the supplied alias lookup. Column order does not
    /// matter (matching is by name) and unrecognized extra columns are
    /// silently ignored. Fails clearly -- never guesses -- when a
    /// required field has no matching column, or when more than one
    /// actual column matches the same field.
    /// </summary>
    private static IReadOnlyDictionary<string, string> ResolveColumnMap(
        CsvReader csv,
        IReadOnlyDictionary<string, string> aliasLookup,
        string[] requiredCanonicalFields)
    {
        if (!csv.Read())
        {
            throw new InvalidDataException(
                "CSV file is empty.");
        }

        csv.ReadHeader();

        var actualHeaders = (csv.HeaderRecord ?? Array.Empty<string>())
            .Where(header => !string.IsNullOrWhiteSpace(header))
            .ToArray();

        var matchesByCanonicalField = requiredCanonicalFields
            .ToDictionary(field => field, _ => new List<string>());

        foreach (var actualHeader in actualHeaders)
        {
            var normalized = NormalizeHeaderKey(actualHeader);

            if (aliasLookup.TryGetValue(normalized, out var canonicalField) &&
                matchesByCanonicalField.TryGetValue(canonicalField, out var matches))
            {
                matches.Add(actualHeader);
            }

            // A header that matches no alias for any required field in
            // this schema is an unrecognized extra column and is
            // intentionally ignored.
        }

        var missingFields = requiredCanonicalFields
            .Where(field => matchesByCanonicalField[field].Count == 0)
            .ToArray();

        if (missingFields.Length > 0)
        {
            throw new InvalidDataException(
                $"Missing required CSV column(s): {string.Join(", ", missingFields)}");
        }

        var ambiguousField = requiredCanonicalFields
            .FirstOrDefault(field => matchesByCanonicalField[field].Count > 1);

        if (ambiguousField is not null)
        {
            var conflictingColumns = matchesByCanonicalField[ambiguousField];

            throw new InvalidDataException(
                $"Ambiguous column mapping for '{ambiguousField}': multiple CSV " +
                $"columns match this field ({string.Join(", ", conflictingColumns)}). " +
                "Rename one column and re-upload.");
        }

        return requiredCanonicalFields.ToDictionary(
            field => field,
            field => matchesByCanonicalField[field][0]);
    }

    /// <summary>
    /// Builds the normalized-alias -> canonical-field lookup for one
    /// schema, throwing at construction time if the same normalized
    /// alias is listed under two different canonical fields.
    /// </summary>
    private static IReadOnlyDictionary<string, string> BuildAliasLookup(
        string schemaName,
        IReadOnlyDictionary<string, string[]> aliasesByCanonicalField)
    {
        var lookup = new Dictionary<string, string>();

        foreach (var (canonicalField, aliases) in aliasesByCanonicalField)
        {
            foreach (var alias in aliases)
            {
                var normalized = NormalizeHeaderKey(alias);

                if (lookup.TryGetValue(normalized, out var existingField) &&
                    !string.Equals(existingField, canonicalField, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Alias table misconfiguration in the {schemaName} schema: " +
                        $"'{alias}' normalizes to a key already claimed by " +
                        $"'{existingField}', but is also listed under '{canonicalField}'. " +
                        "Every alias must resolve to exactly one canonical field.");
                }

                lookup[normalized] = canonicalField;
            }
        }

        return lookup;
    }

    /// <summary>
    /// Normalizes a header for comparison: trims surrounding whitespace,
    /// splits camelCase word boundaries, treats whitespace/hyphen runs
    /// as underscores, collapses repeated underscores, and lowercases
    /// the result. "Transaction_Reference", "transaction-reference",
    /// "transaction reference" and "transactionReference" all normalize
    /// to "transaction_reference".
    /// </summary>
    private static string NormalizeHeaderKey(string header)
    {
        var withCamelBoundaries = CamelCaseBoundary.Replace(header.Trim(), "_");
        var withUnifiedSeparators = SeparatorRun.Replace(withCamelBoundaries, "_");
        var withCollapsedUnderscores = UnderscoreRun.Replace(withUnifiedSeparators, "_");

        return withCollapsedUnderscores.Trim('_').ToLowerInvariant();
    }

    private static bool IsBlankRecord(CsvReader csv)
    {
        var record = csv.Parser.Record;

        return record is null ||
               record.Length == 0 ||
               record.All(string.IsNullOrWhiteSpace);
    }
}
