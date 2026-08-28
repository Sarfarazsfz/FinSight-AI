/**
 * Mirrors FinSight.Application.DTOs.Ingestion.BatchResponse.
 *
 * `validationStatus` is declared as `string` in the backend DTO, but the
 * database enforces exactly two values via the real CHECK constraint
 * `CHK_Batch_ValidationStatus` on the `batches` table: "Valid" or "Invalid".
 * Only "Valid" is ever produced by the current ingestion code path (a batch
 * that fails validation is never persisted), but "Invalid" is a real,
 * reachable database state and must render correctly if it ever appears.
 *
 * `createdAt` is an ISO-8601 timestamp string, matching every other
 * DateTime field returned by this API (see LoginResponse.expiresAtUtc).
 */
export interface BatchResponse {
  batchId: string;
  batchLabel: string;
  paymentRecordCount: number;
  bankRecordCount: number;
  settlementRecordCount: number;
  totalRecordCount: number;
  validationStatus: BatchValidationStatus;
  createdBy: string;
  createdAt: string;
}

/**
 * The only two values `CHK_Batch_ValidationStatus` permits. This is not an
 * invented status set -- it is the exact enumeration the database schema
 * enforces.
 */
export type BatchValidationStatus = 'Valid' | 'Invalid';

/**
 * Mirrors FinSight.Application.DTOs.Ingestion.BatchIngestionResult -- the
 * body of a successful `201` from `POST /api/batches`.
 *
 * This is deliberately NOT the same shape as BatchResponse. It has no
 * `batchLabel`, `createdBy` or `createdAt` -- the backend simply does not
 * return them from this endpoint. A caller that wants those fields for the
 * batch it just created has to ask `GET /api/batches/{batchId}` for them;
 * this interface must never be extended with fields the wire response
 * doesn't actually carry.
 */
export interface BatchIngestionResult {
  batchId: string;
  validationStatus: BatchValidationStatus;
  paymentRecordCount: number;
  bankRecordCount: number;
  settlementRecordCount: number;
  totalRecordCount: number;
}
