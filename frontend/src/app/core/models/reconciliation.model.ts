/**
 * Mirrors FinSight.Application.DTOs.Reconciliation.ReconciliationRunRequest.
 *
 * The only field the backend accepts. Nothing else is sent.
 */
export interface ReconciliationRunRequest {
  batchId: string;
}

/**
 * The clean, string-typed status values as returned by
 * GET /api/reconciliation/runs/{runId} (ReconciliationRunDetailsResponse).
 *
 * This mirrors FinSight.Domain.Enums.ReconciliationRunStatus's *names*, but
 * NOT its wire representation everywhere -- see ReconciliationRunResult
 * below for the asymmetry this type deliberately does not paper over.
 */
export type ReconciliationRunStatus = 'Pending' | 'Running' | 'Completed' | 'Failed';

/**
 * Mirrors FinSight.Application.DTOs.Reconciliation.ReconciliationRunResult --
 * the body of a successful `201` from POST /api/reconciliation/runs.
 *
 * `status` is the RAW backend enum with no JsonStringEnumConverter
 * registered anywhere in the API -- it serializes as a plain NUMBER
 * (0 Pending / 1 Running / 2 Completed / 3 Failed), unlike
 * ReconciliationRunDetailsResponse.status below, which IS a string.
 *
 * This type is used ONLY to read `runId` for navigation. Nothing in this
 * frontend ever renders `ReconciliationRunResult.status` or any of its
 * count fields -- the Run Workspace always re-fetches the GET shape, which
 * has the clean string status. Never normalize these two shapes into one;
 * they are genuinely different DTOs with genuinely different wire types for
 * the same concept.
 */
export interface ReconciliationRunResult {
  runId: string;
  batchId: string;
  status: 0 | 1 | 2 | 3;
  totalReconciliationUnits: number;
  matchedCount: number;
  mismatchedCount: number;
  missingCount: number;
  duplicateCount: number;
  unresolvedCount: number;
  matchRate: number;
}

/**
 * Mirrors FinSight.Application.DTOs.Reconciliation.ReconciliationRunDetailsResponse --
 * the body of GET /api/reconciliation/runs/{runId}. This is the shape the
 * Run Workspace actually renders.
 *
 * `matchRate` and `completedAt` are genuinely nullable on the wire -- a run
 * that hasn't reached a terminal state yet (a crash-artifact "Running" row;
 * see the F6 plan) has neither.
 */
export interface ReconciliationRunDetailsResponse {
  runId: string;
  batchId: string;
  status: ReconciliationRunStatus;
  totalReconciliationUnits: number;
  matchRate: number | null;
  startedAt: string;
  completedAt: string | null;
  createdAt: string;
}

/**
 * Mirrors FinSight.Domain.Enums.MatchStatus's *names* exactly -- this is a
 * per-result classification, not a run-level state. "Pending" is NOT a
 * member here; that value belongs only to ReconciliationRunStatus above.
 */
export type ReconciliationMatchStatus =
  | 'Matched'
  | 'Mismatched'
  | 'Missing'
  | 'Duplicate'
  | 'Unresolved';

/**
 * Mirrors FinSight.Domain.Enums.ReconciliationReasonCode exactly. Rendered
 * verbatim wherever it appears -- never humanized, never reworded.
 */
export type ReconciliationReasonCode =
  | 'EXACT_MATCH'
  | 'TOLERANCE_MATCH'
  | 'AMOUNT_MISMATCH'
  | 'DATE_OUT_OF_TOLERANCE'
  | 'SOURCE_ABSENT_BANK'
  | 'SOURCE_ABSENT_SETTLEMENT'
  | 'SOURCE_ABSENT_PAYMENT'
  | 'DUPLICATE_PAYMENT'
  | 'DUPLICATE_BANK'
  | 'DUPLICATE_SETTLEMENT'
  | 'UNRESOLVED';

/**
 * The two real values `MatchClassifier` ever assigns to `StrategyUsed`, or
 * null for every non-Matched outcome. Verified directly from
 * FinSight.Infrastructure/Reconciliation/MatchClassifier.cs -- not guessed.
 */
export type ReconciliationStrategy =
  | 'StrategyOne_ExactReferenceMatch'
  | 'StrategyTwo_AmountDateToleranceMatch';

/**
 * Mirrors FinSight.Application.DTOs.Reconciliation.ReconciliationResultResponse --
 * one item of GET /api/reconciliation/runs/{runId}/results.
 */
export interface ReconciliationResultResponse {
  resultId: string;
  runId: string;
  normalizedTransactionId: string;
  transactionReference: string;
  status: ReconciliationMatchStatus;
  strategyUsed: ReconciliationStrategy | null;
  reasonCode: ReconciliationReasonCode;
  createdAt: string;
}

/**
 * Mirrors FinSight.Application.DTOs.Reconciliation.SourceTransactionRecordResponse.
 *
 * `status` here is the RAW CSV status column (payment_status/bank_status/
 * settlement_status), uppercased by BatchIngestionService -- a completely
 * different concept from ReconciliationMatchStatus above, and NOT a closed
 * set: BatchIngestionValidator never validates these values against an
 * enum, so this stays a plain `string`, never a union.
 */
export interface SourceTransactionRecordResponse {
  id: string;
  sourceRecordIdentifier: string;
  transactionReference: string;
  amount: number;
  currency: string;
  transactionDate: string;
  status: string;
  createdAt: string;
}

/**
 * Mirrors FinSight.Application.DTOs.Reconciliation.ReconciliationTransactionDetailResponse --
 * the body of GET /api/reconciliation/runs/{runId}/results/{resultId}.
 *
 * `payments`/`banks`/`settlements` are genuinely variable-length arrays, not
 * a fixed one-per-source triple: an empty array is how a `Missing` result
 * is actually represented, and two or more entries is how a `Duplicate`
 * result is actually represented. Never coerce these to a single item or
 * assume exactly one record per source.
 */
export interface ReconciliationTransactionDetailResponse {
  resultId: string;
  runId: string;
  normalizedTransactionId: string;
  transactionReference: string;
  status: ReconciliationMatchStatus;
  strategyUsed: ReconciliationStrategy | null;
  reasonCode: ReconciliationReasonCode;
  payments: SourceTransactionRecordResponse[];
  banks: SourceTransactionRecordResponse[];
  settlements: SourceTransactionRecordResponse[];
}
