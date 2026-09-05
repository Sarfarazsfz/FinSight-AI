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
 * Mirrors FinSight.Application.DTOs.Reconciliation.ReconciliationRunSummaryResponse --
 * the body of GET /api/reconciliation/runs/{runId}/summary.
 *
 * This is the ONLY server-authoritative source of the five status counts.
 * `ReconciliationSummaryBuilder` computes them from every persisted result
 * for the run (`GetByRunIdAsync`, unpaginated), so `matched + mismatched +
 * missing + duplicate + unresolved === totalUnits` by construction.
 *
 * Counting a page of `GET .../results` would describe only that page and
 * would be wrong for any run larger than one page -- never derive these
 * numbers client-side.
 *
 * `matchRate` here is the same persisted `ReconciliationRun.MatchRate` that
 * `ReconciliationRunDetailsResponse.matchRate` exposes, so the two can
 * never disagree.
 */
export interface ReconciliationRunSummaryResponse {
  runId: string;
  batchId: string;
  status: string;
  totalUnits: number;
  matched: number;
  mismatched: number;
  missing: number;
  duplicate: number;
  unresolved: number;
  matchRate: number;
  exceptionCount: number;

  /**
   * Wall-clock milliseconds between the run's persisted StartedAt and
   * CompletedAt, computed server-side. Null while a run has not completed
   * -- an unfinished run has no duration, and the backend deliberately
   * returns null rather than 0.
   *
   * A single measurement of one run on whatever machine executed it. Not a
   * benchmark; nothing distinguishes a cold run from a warm one.
   */
  durationMs: number | null;

  /** totalUnits / duration. Null when the duration is null or zero. */
  recordsPerSecond: number | null;
}

/**
 * Mirrors FinSight.Application.Evaluation.GroundTruthRow -- one expected
 * outcome, supplied by the operator, for POST
 * .../ground-truth-verification.
 *
 * These labels are generated independently of reconciliation (by
 * FinSight.DataGenerator, from the scenario plan, before any run exists).
 * They are nonetheless *operator-supplied* at the point of verification --
 * the UI must never present them as something the system proved by itself.
 */
export interface GroundTruthRow {
  transactionReference: string;
  scenarioType: string;
  expectedStatus: string;
  expectedReasonCode: string;
  expectedExceptionCategory: string;
  expectedPaymentPresent: boolean;
  expectedBankPresent: boolean;
  expectedSettlementPresent: boolean;
  expectedAmountRelationship: string;
  expectedDateRelationship: string;
}

/**
 * Mirrors FinSight.Application.Evaluation.GroundTruthComparisonResult.
 *
 * The backend owns the entire comparison; nothing here is recomputed
 * client-side. `failures` is human-readable prose produced by
 * GroundTruthComparer -- it is rendered verbatim and never parsed into
 * pseudo-structured records.
 *
 * Note what this response does NOT contain: no verification id, no
 * timestamp, no persistence. The endpoint is stateless, so the UI must not
 * imply a stored verification.
 */
export interface GroundTruthComparisonResult {
  isSuccess: boolean;
  expectedTotalUnits: number;
  actualTotalUnits: number;
  expectedMatched: number;
  actualMatched: number;
  expectedMismatched: number;
  actualMismatched: number;
  expectedMissing: number;
  actualMissing: number;
  expectedDuplicate: number;
  actualDuplicate: number;
  expectedUnresolved: number;
  actualUnresolved: number;
  expectedMatchRate: number;
  actualMatchRate: number;
  failures: string[];
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

/**
 * Mirrors FinSight.Domain.Enums.ExceptionCategory exactly. These are NOT
 * aliases of ReconciliationMatchStatus -- different enum, different names,
 * even though every exception exists because of a non-Matched result.
 */
export type ReconciliationExceptionCategory =
  | 'AmountMismatch'
  | 'DateMismatch'
  | 'MissingRecord'
  | 'DuplicateRecord'
  | 'Unresolved';

/**
 * Mirrors FinSight.Application.DTOs.Reconciliation.ReconciliationExceptionResponse --
 * the body of both GET /api/reconciliation/runs/{runId}/exceptions (paged
 * items) and GET /api/reconciliation/exceptions/{exceptionId} (single).
 *
 * `discrepancyDetail` is a STRING containing serialized JSON built by
 * ReconciliationOrchestrator.BuildExceptionDetail -- real, but not a
 * compiler-enforced contract (the DTO only promises `string`). It is
 * rendered as pretty-printed JSON with a raw-string fallback, never parsed
 * into a bespoke structured UI -- see ExceptionDetailPage.
 *
 * `involvedSources` is a comma-joined string (e.g. "Payment,Bank"), not an
 * array on the wire.
 *
 * `aiExplanation`/`aiSuggestedCategory`/`aiExplanationGeneratedAt` are real
 * fields on this response -- modeled here because this is an honest mirror
 * of what the endpoint returns, not because F8 renders them. They stay
 * unused until F9.
 */
export interface ReconciliationExceptionResponse {
  exceptionId: string;
  runId: string;
  reconciliationResultId: string;
  transactionReference: string;
  category: ReconciliationExceptionCategory;
  involvedSources: string;
  discrepancyDetail: string;
  aiExplanation: string | null;
  aiSuggestedCategory: string | null;
  aiExplanationGeneratedAt: string | null;
  createdAt: string;
  updatedAt: string | null;
}

/**
 * Mirrors FinSight.Application.DTOs.Ai.AiExplanationResponse -- the body of
 * `POST /api/reconciliation/exceptions/{exceptionId}/ai-explanation`.
 *
 * `suggestedCategory` is advisory only (see AiProviderRouter/AiExplanationService
 * on the backend): it must never be rendered with the same visual weight as
 * the verified `ReconciliationExceptionResponse.category`, and this frontend
 * never computes, recomputes, or otherwise treats it as authoritative.
 */
export interface AiExplanationResponse {
  provider: string;
  explanation: string;
  suggestedCategory: string | null;
  generatedAtUtc: string;
}

/**
 * Mirrors FinSight.Domain.Enums.AuditEventType exactly (the CHK_Audit_EventType
 * constraint on the audit_logs table enumerates the same twelve values).
 */
export type AuditEventType =
  | 'BatchCreated'
  | 'BatchValidated'
  | 'ReconciliationStarted'
  | 'ReconciliationCompleted'
  | 'ReconciliationFailed'
  | 'ReconciliationDecisionRecorded'
  | 'ExceptionCreated'
  | 'AiQuestionAsked'
  | 'AiToolInvoked'
  | 'AiExplanationRequested'
  | 'AiExplanationFailed'
  | 'AiAssistantFailed';

/**
 * Mirrors FinSight.Application.DTOs.Reconciliation.AuditLogEntryResponse --
 * one item of GET /api/reconciliation/runs/{runId}/audit
 * (PagedResponse<AuditLogEntryResponse>).
 *
 * This is evidence ABOUT the run's execution -- timing, throughput, which
 * events fired, in what order -- never a second source of financial truth.
 * Match status, match rate, exception counts and classification remain
 * whatever GET .../summary and Ground Truth Verification say they are.
 *
 * The backend's AuditLog entity carries no actor/user-identity column, so
 * there is deliberately no such field here -- never render or infer one.
 *
 * `detail` is the raw JSON payload exactly as persisted (a jsonb column),
 * passed through unparsed -- the same convention this API already uses for
 * ReconciliationExceptionResponse.discrepancyDetail. Parse it defensively
 * and never assume it matches a fixed shape: different event types carry
 * different fields, and a parse failure must never break the viewer.
 */
export interface AuditLogEntryResponse {
  id: string;
  occurredAt: string;
  eventType: AuditEventType;
  runId: string | null;
  relatedEntityType: string | null;
  relatedEntityId: string | null;
  detail: string;
}

/**
 * Mirrors FinSight.Application.AI.FinanceAssistantRequest -- the body of
 * `POST /api/finance-assistant/ask`. `runId` always comes from the current
 * route/workspace context, never from user input.
 */
export interface FinanceAssistantRequest {
  runId: string;
  question: string;
}

/**
 * Mirrors FinSight.Application.AI.FinanceAssistantResponse.
 *
 * `toolsUsed` is real backend provenance (which read-only tools the
 * assistant actually invoked) -- the frontend renders it verbatim and never
 * infers, reconstructs, or invents tool names. `traceId` is optional
 * metadata, present only when the backend supplies one.
 */
export interface FinanceAssistantResponse {
  answer: string;
  toolsUsed: string[];
  traceId: string | null;
}
