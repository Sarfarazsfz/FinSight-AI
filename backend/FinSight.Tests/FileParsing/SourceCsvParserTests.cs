using System.Text;
using FinSight.Infrastructure.FileParsing;

namespace FinSight.Tests.FileParsing;

/// <summary>
/// Phase 10 (Flexible CSV Column Mapping): direct unit tests for
/// SourceCsvParser. Before this phase the parser had no dedicated test
/// file at all -- only FakeSourceCsvParser was exercised elsewhere.
///
/// These tests go through the public ISourceCsvParser surface only
/// (no reflection into private alias tables), matching how the rest of
/// the ingestion pipeline is tested in this project.
/// </summary>
[TestFixture]
public sealed class SourceCsvParserTests
{
    private SourceCsvParser _parser = null!;

    [SetUp]
    public void SetUp()
    {
        _parser = new SourceCsvParser();
    }

    // ------------------------------------------------------------
    // 1-3: canonical headers still parse correctly (backward compat)
    // ------------------------------------------------------------

    [Test]
    public async Task ParsePaymentsAsync_WithCanonicalHeaders_ParsesCorrectly()
    {
        await using var stream = CreateStream(
            """
            payment_record_id,transaction_reference,amount,currency,transaction_date,payment_status
            PAY-000001,TXN-0001,100.00,INR,2026-01-01,COMPLETED
            """);

        var rows = await _parser.ParsePaymentsAsync(stream);

        Assert.That(rows, Has.Count.EqualTo(1));

        Assert.Multiple(() =>
        {
            Assert.That(rows[0].PaymentRecordId, Is.EqualTo("PAY-000001"));
            Assert.That(rows[0].TransactionReference, Is.EqualTo("TXN-0001"));
            Assert.That(rows[0].Amount, Is.EqualTo("100.00"));
            Assert.That(rows[0].Currency, Is.EqualTo("INR"));
            Assert.That(rows[0].TransactionDate, Is.EqualTo("2026-01-01"));
            Assert.That(rows[0].PaymentStatus, Is.EqualTo("COMPLETED"));
        });
    }

    [Test]
    public async Task ParseBankAsync_WithCanonicalHeaders_ParsesCorrectly()
    {
        await using var stream = CreateStream(
            """
            bank_record_id,transaction_reference,amount,currency,transaction_date,bank_status
            BANK-000001,TXN-0001,100.00,INR,2026-01-01,CLEARED
            """);

        var rows = await _parser.ParseBankAsync(stream);

        Assert.That(rows, Has.Count.EqualTo(1));

        Assert.Multiple(() =>
        {
            Assert.That(rows[0].BankRecordId, Is.EqualTo("BANK-000001"));
            Assert.That(rows[0].TransactionReference, Is.EqualTo("TXN-0001"));
            Assert.That(rows[0].Amount, Is.EqualTo("100.00"));
            Assert.That(rows[0].Currency, Is.EqualTo("INR"));
            Assert.That(rows[0].TransactionDate, Is.EqualTo("2026-01-01"));
            Assert.That(rows[0].BankStatus, Is.EqualTo("CLEARED"));
        });
    }

    [Test]
    public async Task ParseSettlementsAsync_WithCanonicalHeaders_ParsesCorrectly()
    {
        await using var stream = CreateStream(
            """
            settlement_record_id,transaction_reference,amount,currency,transaction_date,settlement_status
            SET-000001,TXN-0001,100.00,INR,2026-01-01,SETTLED
            """);

        var rows = await _parser.ParseSettlementsAsync(stream);

        Assert.That(rows, Has.Count.EqualTo(1));

        Assert.Multiple(() =>
        {
            Assert.That(rows[0].SettlementRecordId, Is.EqualTo("SET-000001"));
            Assert.That(rows[0].TransactionReference, Is.EqualTo("TXN-0001"));
            Assert.That(rows[0].Amount, Is.EqualTo("100.00"));
            Assert.That(rows[0].Currency, Is.EqualTo("INR"));
            Assert.That(rows[0].TransactionDate, Is.EqualTo("2026-01-01"));
            Assert.That(rows[0].SettlementStatus, Is.EqualTo("SETTLED"));
        });
    }

    // ------------------------------------------------------------
    // 4: case variation
    // ------------------------------------------------------------

    [Test]
    public async Task ParsePaymentsAsync_WithUppercaseHeaders_ParsesCorrectly()
    {
        // Proves the previous latent defect is fixed: header existence
        // used to be checked case-insensitively but extraction used a
        // hardcoded lowercase GetField<string> lookup, which is
        // case-sensitive in CsvHelper -- so an uppercase header used to
        // pass validation and then silently extract as empty.
        await using var stream = CreateStream(
            """
            PAYMENT_RECORD_ID,TRANSACTION_REFERENCE,AMOUNT,CURRENCY,TRANSACTION_DATE,PAYMENT_STATUS
            PAY-000001,TXN-0001,100.00,INR,2026-01-01,COMPLETED
            """);

        var rows = await _parser.ParsePaymentsAsync(stream);

        Assert.That(rows, Has.Count.EqualTo(1));

        Assert.Multiple(() =>
        {
            Assert.That(rows[0].PaymentRecordId, Is.EqualTo("PAY-000001"));
            Assert.That(rows[0].TransactionReference, Is.EqualTo("TXN-0001"));
            Assert.That(rows[0].Amount, Is.EqualTo("100.00"));
        });
    }

    // ------------------------------------------------------------
    // 5: whitespace variation
    // ------------------------------------------------------------

    [Test]
    public async Task ParsePaymentsAsync_WithSpaceSeparatedHeaders_ParsesCorrectly()
    {
        await using var stream = CreateStream(
            """
            payment record id,transaction reference,amount,currency,transaction date,payment status
            PAY-000001,TXN-0001,100.00,INR,2026-01-01,COMPLETED
            """);

        var rows = await _parser.ParsePaymentsAsync(stream);

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].PaymentRecordId, Is.EqualTo("PAY-000001"));
        Assert.That(rows[0].TransactionReference, Is.EqualTo("TXN-0001"));
    }

    // ------------------------------------------------------------
    // 6: hyphen / camelCase normalization
    // ------------------------------------------------------------

    [Test]
    public async Task ParsePaymentsAsync_WithHyphenatedAndCamelCaseHeaders_ParsesCorrectly()
    {
        await using var stream = CreateStream(
            """
            payment-record-id,transactionReference,amount,currency,transactionDate,payment-status
            PAY-000001,TXN-0001,100.00,INR,2026-01-01,COMPLETED
            """);

        var rows = await _parser.ParsePaymentsAsync(stream);

        Assert.That(rows, Has.Count.EqualTo(1));

        Assert.Multiple(() =>
        {
            Assert.That(rows[0].PaymentRecordId, Is.EqualTo("PAY-000001"));
            Assert.That(rows[0].TransactionReference, Is.EqualTo("TXN-0001"));
            Assert.That(rows[0].TransactionDate, Is.EqualTo("2026-01-01"));
        });
    }

    // ------------------------------------------------------------
    // 7: approved Payment aliases map to the correct DTO properties
    // ------------------------------------------------------------

    [Test]
    public async Task ParsePaymentsAsync_WithApprovedAliasesForEveryField_MapsToCorrectProperties()
    {
        await using var stream = CreateStream(
            """
            payment_id,txn_ref,amount_paid,currency_code,txn_date,payment_state
            PAY-000001,TXN-0001,100.00,INR,2026-01-01,COMPLETED
            """);

        var rows = await _parser.ParsePaymentsAsync(stream);

        Assert.That(rows, Has.Count.EqualTo(1));

        Assert.Multiple(() =>
        {
            Assert.That(rows[0].PaymentRecordId, Is.EqualTo("PAY-000001"));
            Assert.That(rows[0].TransactionReference, Is.EqualTo("TXN-0001"));
            Assert.That(rows[0].Amount, Is.EqualTo("100.00"));
            Assert.That(rows[0].Currency, Is.EqualTo("INR"));
            Assert.That(rows[0].TransactionDate, Is.EqualTo("2026-01-01"));
            Assert.That(rows[0].PaymentStatus, Is.EqualTo("COMPLETED"));
        });
    }

    // ------------------------------------------------------------
    // 8: column order does not matter
    // ------------------------------------------------------------

    [Test]
    public async Task ParsePaymentsAsync_WithReorderedColumns_ParsesCorrectly()
    {
        await using var stream = CreateStream(
            """
            payment_status,currency,payment_record_id,transaction_date,amount,transaction_reference
            COMPLETED,INR,PAY-000001,2026-01-01,100.00,TXN-0001
            """);

        var rows = await _parser.ParsePaymentsAsync(stream);

        Assert.That(rows, Has.Count.EqualTo(1));

        Assert.Multiple(() =>
        {
            Assert.That(rows[0].PaymentRecordId, Is.EqualTo("PAY-000001"));
            Assert.That(rows[0].TransactionReference, Is.EqualTo("TXN-0001"));
            Assert.That(rows[0].Amount, Is.EqualTo("100.00"));
            Assert.That(rows[0].Currency, Is.EqualTo("INR"));
            Assert.That(rows[0].TransactionDate, Is.EqualTo("2026-01-01"));
            Assert.That(rows[0].PaymentStatus, Is.EqualTo("COMPLETED"));
        });
    }

    // ------------------------------------------------------------
    // 9: extra unrecognized columns are accepted
    // ------------------------------------------------------------

    [Test]
    public async Task ParsePaymentsAsync_WithExtraUnrecognizedColumn_IgnoresIt()
    {
        await using var stream = CreateStream(
            """
            payment_record_id,transaction_reference,amount,currency,transaction_date,payment_status,internal_notes
            PAY-000001,TXN-0001,100.00,INR,2026-01-01,COMPLETED,ignore this
            """);

        var rows = await _parser.ParsePaymentsAsync(stream);

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].PaymentRecordId, Is.EqualTo("PAY-000001"));
    }

    // ------------------------------------------------------------
    // 10: missing required semantic field fails clearly
    // ------------------------------------------------------------

    [Test]
    public void ParsePaymentsAsync_WithNoColumnForTransactionReference_ThrowsInvalidDataException()
    {
        var stream = CreateStream(
            """
            payment_record_id,amount,currency,transaction_date,payment_status
            PAY-000001,100.00,INR,2026-01-01,COMPLETED
            """);

        var ex =
            Assert.ThrowsAsync<InvalidDataException>(
                async () => await _parser.ParsePaymentsAsync(stream));

        Assert.That(
            ex!.Message,
            Is.EqualTo("Missing required CSV column(s): transaction_reference"));
    }

    // ------------------------------------------------------------
    // 11: ambiguous mapping fails clearly, never guesses
    // ------------------------------------------------------------

    [Test]
    public void ParsePaymentsAsync_WithTwoColumnsMatchingSameField_ThrowsInvalidDataException()
    {
        var stream = CreateStream(
            """
            payment_record_id,transaction_reference,txn_ref,amount,currency,transaction_date,payment_status
            PAY-000001,TXN-0001,TXN-0001,100.00,INR,2026-01-01,COMPLETED
            """);

        var ex =
            Assert.ThrowsAsync<InvalidDataException>(
                async () => await _parser.ParsePaymentsAsync(stream));

        Assert.That(
            ex!.Message,
            Is.EqualTo(
                "Ambiguous column mapping for 'transaction_reference': multiple CSV " +
                "columns match this field (transaction_reference, txn_ref). " +
                "Rename one column and re-upload."));
    }

    // ------------------------------------------------------------
    // 12: extraction uses the actual resolved header, not a hardcoded
    // canonical name, for mixed-case headers
    // ------------------------------------------------------------

    [Test]
    public async Task ParsePaymentsAsync_WithMixedCaseHeaders_ExtractsByResolvedActualHeader()
    {
        await using var stream = CreateStream(
            """
            Payment_Record_Id,Transaction_Reference,Amount,Currency,Transaction_Date,Payment_Status
            PAY-000001,TXN-0001,100.00,INR,2026-01-01,COMPLETED
            """);

        var rows = await _parser.ParsePaymentsAsync(stream);

        Assert.That(rows, Has.Count.EqualTo(1));

        Assert.Multiple(() =>
        {
            Assert.That(rows[0].PaymentRecordId, Is.EqualTo("PAY-000001"));
            Assert.That(rows[0].TransactionReference, Is.EqualTo("TXN-0001"));
            Assert.That(rows[0].Amount, Is.EqualTo("100.00"));
            Assert.That(rows[0].Currency, Is.EqualTo("INR"));
            Assert.That(rows[0].TransactionDate, Is.EqualTo("2026-01-01"));
            Assert.That(rows[0].PaymentStatus, Is.EqualTo("COMPLETED"));
        });
    }

    // ------------------------------------------------------------
    // 13: invalid/missing values are still delegated to the existing
    // validation layer -- the parser itself does not reject them
    // ------------------------------------------------------------

    [Test]
    public async Task ParsePaymentsAsync_WithInvalidAmountValueViaAliasHeader_StoresValueVerbatimWithoutValidating()
    {
        await using var stream = CreateStream(
            """
            payment_id,txn_ref,amount_paid,currency_code,txn_date,payment_state
            PAY-000001,TXN-0001,not-a-number,INR,2026-01-01,COMPLETED
            """);

        var rows = await _parser.ParsePaymentsAsync(stream);

        // The parser passes the raw string through unchanged; it is
        // BatchIngestionValidator's job (unchanged by this phase) to
        // reject it.
        Assert.That(rows[0].Amount, Is.EqualTo("not-a-number"));
    }

    // ------------------------------------------------------------
    // 14: approved aliases for Bank and Settlement resolve without any
    // cross-field collision within their schema's alias table
    // ------------------------------------------------------------

    [Test]
    public async Task ParseBankAsync_WithApprovedAliasesForEveryField_ResolvesWithoutCrossFieldCollision()
    {
        await using var stream = CreateStream(
            """
            bank_id,txn_ref,amount_paid,currency_code,txn_date,bank_state
            BANK-000001,TXN-0001,100.00,INR,2026-01-01,CLEARED
            """);

        var rows = await _parser.ParseBankAsync(stream);

        Assert.That(rows, Has.Count.EqualTo(1));

        Assert.Multiple(() =>
        {
            Assert.That(rows[0].BankRecordId, Is.EqualTo("BANK-000001"));
            Assert.That(rows[0].TransactionReference, Is.EqualTo("TXN-0001"));
            Assert.That(rows[0].Amount, Is.EqualTo("100.00"));
            Assert.That(rows[0].Currency, Is.EqualTo("INR"));
            Assert.That(rows[0].TransactionDate, Is.EqualTo("2026-01-01"));
            Assert.That(rows[0].BankStatus, Is.EqualTo("CLEARED"));
        });
    }

    [Test]
    public async Task ParseSettlementsAsync_WithApprovedAliasesForEveryField_ResolvesWithoutCrossFieldCollision()
    {
        await using var stream = CreateStream(
            """
            settlement_id,txn_ref,amount_paid,currency_code,txn_date,settlement_state
            SET-000001,TXN-0001,100.00,INR,2026-01-01,SETTLED
            """);

        var rows = await _parser.ParseSettlementsAsync(stream);

        Assert.That(rows, Has.Count.EqualTo(1));

        Assert.Multiple(() =>
        {
            Assert.That(rows[0].SettlementRecordId, Is.EqualTo("SET-000001"));
            Assert.That(rows[0].TransactionReference, Is.EqualTo("TXN-0001"));
            Assert.That(rows[0].Amount, Is.EqualTo("100.00"));
            Assert.That(rows[0].Currency, Is.EqualTo("INR"));
            Assert.That(rows[0].TransactionDate, Is.EqualTo("2026-01-01"));
            Assert.That(rows[0].SettlementStatus, Is.EqualTo("SETTLED"));
        });
    }

    private static MemoryStream CreateStream(string content)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }
}
