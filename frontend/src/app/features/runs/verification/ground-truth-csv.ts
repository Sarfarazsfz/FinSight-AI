import type { GroundTruthRow } from '../../../core/models/reconciliation.model';

/**
 * Parses a ground-truth CSV into the exact `GroundTruthRow[]` the backend's
 * verification endpoint expects.
 *
 * This is transport only. It performs **no** comparison, applies no
 * business rules, and makes no judgement about whether the labels are
 * correct -- it converts the operator's file into the request body and
 * nothing more. Every decision about pass or fail belongs to
 * GroundTruthComparer on the server.
 *
 * The column names match the header emitted by FinSight.DataGenerator's
 * CsvWriter, so a generated `ground-truth.csv` can be uploaded unmodified.
 */

/** Snake_case CSV header -> camelCase request field. */
const COLUMN_MAP: ReadonlyMap<string, keyof GroundTruthRow> = new Map([
  ['transaction_reference', 'transactionReference'],
  ['scenario_type', 'scenarioType'],
  ['expected_status', 'expectedStatus'],
  ['expected_reason_code', 'expectedReasonCode'],
  ['expected_exception_category', 'expectedExceptionCategory'],
  ['expected_payment_present', 'expectedPaymentPresent'],
  ['expected_bank_present', 'expectedBankPresent'],
  ['expected_settlement_present', 'expectedSettlementPresent'],
  ['expected_amount_relationship', 'expectedAmountRelationship'],
  ['expected_date_relationship', 'expectedDateRelationship'],
]);

const BOOLEAN_FIELDS: ReadonlySet<keyof GroundTruthRow> = new Set([
  'expectedPaymentPresent',
  'expectedBankPresent',
  'expectedSettlementPresent',
]);

export const GROUND_TRUTH_COLUMNS: readonly string[] = [
  ...COLUMN_MAP.keys(),
];

export class GroundTruthCsvError extends Error {}

/**
 * Splits one CSV line, honouring double-quoted fields so a quoted value
 * containing a comma is not split. Deliberately minimal -- the generated
 * file has no embedded newlines, and a full CSV grammar would be more
 * machinery than this format needs.
 */
function splitLine(line: string): string[] {
  const values: string[] = [];
  let current = '';
  let inQuotes = false;

  for (let i = 0; i < line.length; i++) {
    const char = line[i];

    if (char === '"') {
      // A doubled quote inside a quoted field is one literal quote.
      if (inQuotes && line[i + 1] === '"') {
        current += '"';
        i++;
        continue;
      }

      inQuotes = !inQuotes;
      continue;
    }

    if (char === ',' && !inQuotes) {
      values.push(current);
      current = '';
      continue;
    }

    current += char;
  }

  values.push(current);

  return values.map((value) => value.trim());
}

function toBoolean(raw: string, column: string, lineNumber: number): boolean {
  const value = raw.trim().toLowerCase();

  if (value === 'true') {
    return true;
  }

  if (value === 'false') {
    return false;
  }

  throw new GroundTruthCsvError(
    `Line ${lineNumber}: "${column}" must be true or false, but was "${raw}".`,
  );
}

export function parseGroundTruthCsv(text: string): GroundTruthRow[] {
  // Strip a UTF-8 BOM: the generator writes one, and it would otherwise
  // corrupt the first header name.
  const withoutBom = text.replace(/^﻿/, '');

  const lines = withoutBom
    .split(/\r?\n/)
    .filter((line) => line.trim().length > 0);

  if (lines.length === 0) {
    throw new GroundTruthCsvError('The file is empty.');
  }

  const header = splitLine(lines[0]).map((column) => column.toLowerCase());

  const missing = GROUND_TRUTH_COLUMNS.filter(
    (column) => !header.includes(column),
  );

  if (missing.length > 0) {
    throw new GroundTruthCsvError(
      `Missing required column${missing.length === 1 ? '' : 's'}: ${missing.join(', ')}.`,
    );
  }

  if (lines.length === 1) {
    throw new GroundTruthCsvError(
      'The file contains a header but no ground-truth rows.',
    );
  }

  const rows: GroundTruthRow[] = [];

  for (let i = 1; i < lines.length; i++) {
    const values = splitLine(lines[i]);
    const lineNumber = i + 1;

    if (values.length !== header.length) {
      throw new GroundTruthCsvError(
        `Line ${lineNumber}: expected ${header.length} columns but found ${values.length}.`,
      );
    }

    // Built field by field from the header, so column order in the file
    // does not matter.
    const row: Record<string, string | boolean> = {};

    for (let column = 0; column < header.length; column++) {
      const field = COLUMN_MAP.get(header[column]);

      if (field === undefined) {
        // Unknown extra column -- ignored rather than rejected, so a file
        // carrying additional annotations still verifies.
        continue;
      }

      row[field] = BOOLEAN_FIELDS.has(field)
        ? toBoolean(values[column], header[column], lineNumber)
        : values[column];
    }

    if ((row['transactionReference'] as string).length === 0) {
      throw new GroundTruthCsvError(
        `Line ${lineNumber}: transaction_reference cannot be empty.`,
      );
    }

    rows.push(row as unknown as GroundTruthRow);
  }

  return rows;
}
