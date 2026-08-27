using CsvHelper;
using CsvHelper.Configuration;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Ingestion;
using System.Globalization;

namespace FinSight.Infrastructure.FileParsing;

public sealed class SourceCsvParser : ISourceCsvParser
{
    private static readonly CsvConfiguration CsvConfiguration = new(
        CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        TrimOptions = TrimOptions.Trim,
        IgnoreBlankLines = true,

        // Header validation is performed explicitly by this parser.
        HeaderValidated = null,

        // Missing/invalid values are handled by the ingestion validation layer.
        MissingFieldFound = null
    };

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

        ValidateHeaders(
            csv,
            "payment_record_id",
            "transaction_reference",
            "amount",
            "currency",
            "transaction_date",
            "payment_status");

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
                PaymentRecordId = csv.GetField<string>("payment_record_id")?.Trim()
                    ?? string.Empty,

                TransactionReference = csv.GetField<string>("transaction_reference")?.Trim()
                    ?? string.Empty,

                Amount = csv.GetField<string>("amount")?.Trim()
                    ?? string.Empty,

                Currency = csv.GetField<string>("currency")?.Trim()
                    ?? string.Empty,

                TransactionDate = csv.GetField<string>("transaction_date")?.Trim()
                    ?? string.Empty,

                PaymentStatus = csv.GetField<string>("payment_status")?.Trim()
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

        ValidateHeaders(
            csv,
            "bank_record_id",
            "transaction_reference",
            "amount",
            "currency",
            "transaction_date",
            "bank_status");

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
                BankRecordId = csv.GetField<string>("bank_record_id")?.Trim()
                    ?? string.Empty,

                TransactionReference = csv.GetField<string>("transaction_reference")?.Trim()
                    ?? string.Empty,

                Amount = csv.GetField<string>("amount")?.Trim()
                    ?? string.Empty,

                Currency = csv.GetField<string>("currency")?.Trim()
                    ?? string.Empty,

                TransactionDate = csv.GetField<string>("transaction_date")?.Trim()
                    ?? string.Empty,

                BankStatus = csv.GetField<string>("bank_status")?.Trim()
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

        ValidateHeaders(
            csv,
            "settlement_record_id",
            "transaction_reference",
            "amount",
            "currency",
            "transaction_date",
            "settlement_status");

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
                SettlementRecordId = csv.GetField<string>("settlement_record_id")?.Trim()
                    ?? string.Empty,

                TransactionReference = csv.GetField<string>("transaction_reference")?.Trim()
                    ?? string.Empty,

                Amount = csv.GetField<string>("amount")?.Trim()
                    ?? string.Empty,

                Currency = csv.GetField<string>("currency")?.Trim()
                    ?? string.Empty,

                TransactionDate = csv.GetField<string>("transaction_date")?.Trim()
                    ?? string.Empty,

                SettlementStatus = csv.GetField<string>("settlement_status")?.Trim()
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

    private static void ValidateHeaders(
        CsvReader csv,
        params string[] requiredHeaders)
    {
        if (!csv.Read())
        {
            throw new InvalidDataException(
                "CSV file is empty.");
        }

        csv.ReadHeader();

        var actualHeaders = csv.HeaderRecord ?? Array.Empty<string>();

        var normalizedHeaders = new HashSet<string>(
            actualHeaders
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var missingHeaders = requiredHeaders
            .Where(header => !normalizedHeaders.Contains(header))
            .ToArray();

        if (missingHeaders.Length > 0)
        {
            throw new InvalidDataException(
                $"Missing required CSV column(s): {string.Join(", ", missingHeaders)}");
        }
    }

    private static bool IsBlankRecord(CsvReader csv)
    {
        var record = csv.Parser.Record;

        return record is null ||
               record.Length == 0 ||
               record.All(string.IsNullOrWhiteSpace);
    }
}