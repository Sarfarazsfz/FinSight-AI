import { GroundTruthCsvError, parseGroundTruthCsv } from './ground-truth-csv';

const HEADER =
  'transaction_reference,scenario_type,expected_status,expected_reason_code,' +
  'expected_exception_category,expected_payment_present,expected_bank_present,' +
  'expected_settlement_present,expected_amount_relationship,expected_date_relationship';

const MATCHED_ROW =
  'TXN-0001,ExactMatch,Matched,EXACT_MATCH,,true,true,true,Exact,Exact';

const MISSING_ROW =
  'TXN-0002,MissingBank,Missing,SOURCE_ABSENT_BANK,MissingRecord,true,false,true,NotComparable,NotComparable';

describe('parseGroundTruthCsv', () => {
  it('maps the generator header to the request field names', () => {
    const rows = parseGroundTruthCsv(`${HEADER}\n${MATCHED_ROW}`);

    expect(rows.length).toBe(1);
    expect(rows[0]).toEqual({
      transactionReference: 'TXN-0001',
      scenarioType: 'ExactMatch',
      expectedStatus: 'Matched',
      expectedReasonCode: 'EXACT_MATCH',
      expectedExceptionCategory: '',
      expectedPaymentPresent: true,
      expectedBankPresent: true,
      expectedSettlementPresent: true,
      expectedAmountRelationship: 'Exact',
      expectedDateRelationship: 'Exact',
    });
  });

  it('converts the presence columns to real booleans, not strings', () => {
    const rows = parseGroundTruthCsv(`${HEADER}\n${MISSING_ROW}`);

    expect(rows[0].expectedPaymentPresent).toBeTrue();
    expect(rows[0].expectedBankPresent).toBeFalse();
    expect(rows[0].expectedSettlementPresent).toBeTrue();
  });

  it('parses every data row', () => {
    const rows = parseGroundTruthCsv(`${HEADER}\n${MATCHED_ROW}\n${MISSING_ROW}`);

    expect(rows.length).toBe(2);
    expect(rows.map((r) => r.transactionReference)).toEqual([
      'TXN-0001',
      'TXN-0002',
    ]);
  });

  it('tolerates a UTF-8 BOM, which the generator writes', () => {
    const rows = parseGroundTruthCsv(`﻿${HEADER}\n${MATCHED_ROW}`);

    expect(rows[0].transactionReference).toBe('TXN-0001');
  });

  it('tolerates CRLF line endings and a trailing newline', () => {
    const rows = parseGroundTruthCsv(`${HEADER}\r\n${MATCHED_ROW}\r\n`);

    expect(rows.length).toBe(1);
  });

  it('is order-independent -- fields are bound by header name', () => {
    const swapped =
      'expected_status,transaction_reference,scenario_type,expected_reason_code,' +
      'expected_exception_category,expected_payment_present,expected_bank_present,' +
      'expected_settlement_present,expected_amount_relationship,expected_date_relationship';

    const rows = parseGroundTruthCsv(
      `${swapped}\nMatched,TXN-0009,ExactMatch,EXACT_MATCH,,true,true,true,Exact,Exact`,
    );

    expect(rows[0].transactionReference).toBe('TXN-0009');
    expect(rows[0].expectedStatus).toBe('Matched');
  });

  it('rejects a file missing a required column', () => {
    const header = HEADER.replace(',expected_status', '');

    expect(() => parseGroundTruthCsv(`${header}\nTXN-0001,ExactMatch,EXACT_MATCH,,true,true,true,Exact,Exact`))
      .toThrowMatching(
        (e: Error) =>
          e instanceof GroundTruthCsvError && e.message.includes('expected_status'),
      );
  });

  it('rejects an empty file', () => {
    expect(() => parseGroundTruthCsv('   ')).toThrowMatching(
      (e: Error) => e instanceof GroundTruthCsvError,
    );
  });

  it('rejects a header with no data rows', () => {
    expect(() => parseGroundTruthCsv(HEADER)).toThrowMatching(
      (e: Error) =>
        e instanceof GroundTruthCsvError && e.message.includes('no ground-truth rows'),
    );
  });

  it('rejects a row with the wrong column count, naming the line', () => {
    expect(() => parseGroundTruthCsv(`${HEADER}\nTXN-0001,ExactMatch`)).toThrowMatching(
      (e: Error) => e instanceof GroundTruthCsvError && e.message.includes('Line 2'),
    );
  });

  it('rejects a non-boolean presence value rather than coercing it', () => {
    const bad = MATCHED_ROW.replace(',true,true,true,', ',yes,true,true,');

    expect(() => parseGroundTruthCsv(`${HEADER}\n${bad}`)).toThrowMatching(
      (e: Error) =>
        e instanceof GroundTruthCsvError && e.message.includes('true or false'),
    );
  });

  it('rejects a row with an empty transaction reference', () => {
    const bad = MATCHED_ROW.replace('TXN-0001', '');

    expect(() => parseGroundTruthCsv(`${HEADER}\n${bad}`)).toThrowMatching(
      (e: Error) =>
        e instanceof GroundTruthCsvError &&
        e.message.includes('transaction_reference'),
    );
  });

  it('keeps a quoted field containing a comma intact', () => {
    const quoted = MATCHED_ROW.replace(
      ',ExactMatch,',
      ',"ExactMatch, tolerance",',
    );

    const rows = parseGroundTruthCsv(`${HEADER}\n${quoted}`);

    expect(rows[0].scenarioType).toBe('ExactMatch, tolerance');
  });
});
