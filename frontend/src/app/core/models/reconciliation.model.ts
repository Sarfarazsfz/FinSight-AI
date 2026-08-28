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
