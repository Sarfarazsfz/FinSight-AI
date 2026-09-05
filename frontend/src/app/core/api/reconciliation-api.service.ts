import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import type {
  AiExplanationResponse,
  AuditLogEntryResponse,
  FinanceAssistantRequest,
  FinanceAssistantResponse,
  GroundTruthComparisonResult,
  GroundTruthRow,
  ReconciliationExceptionResponse,
  ReconciliationResultResponse,
  ReconciliationRunDetailsResponse,
  ReconciliationRunRequest,
  ReconciliationRunResult,
  ReconciliationRunSummaryResponse,
  ReconciliationTransactionDetailResponse,
} from '../models/reconciliation.model';
import type { PagedResponse } from '../models/paged-response.model';

/**
 * Thin typed wrapper over the reconciliation endpoints this frontend
 * actually consumes: creating a run, reading one back, listing its results,
 * reading one result's source evidence, listing/reading exceptions, and
 * requesting an AI explanation for one exception.
 *
 * `summary` and ground-truth verification are both consumed by the Run
 * Workspace and the verification page respectively.
 */
@Injectable({ providedIn: 'root' })
export class ReconciliationApi {
  private readonly http = inject(HttpClient);

  private readonly baseUrl = `${environment.apiBaseUrl}/reconciliation`;

  /**
   * Returns the raw `201` body. Callers must only read `runId` from it --
   * see ReconciliationRunResult's doc comment for why its `status` and
   * count fields are never rendered.
   */
  createRun(batchId: string): Observable<ReconciliationRunResult> {
    const request: ReconciliationRunRequest = { batchId };
    return this.http.post<ReconciliationRunResult>(`${this.baseUrl}/runs`, request);
  }

  getRun(runId: string): Observable<ReconciliationRunDetailsResponse> {
    return this.http.get<ReconciliationRunDetailsResponse>(`${this.baseUrl}/runs/${runId}`);
  }

  /**
   * Whole-run status totals. The only correct source for the five-count
   * breakdown -- `getResults` returns one page, so counting its items
   * would describe that page rather than the run.
   */
  getSummary(runId: string): Observable<ReconciliationRunSummaryResponse> {
    return this.http.get<ReconciliationRunSummaryResponse>(
      `${this.baseUrl}/runs/${runId}/summary`,
    );
  }

  /**
   * Compares operator-supplied ground-truth labels against this run's
   * persisted deterministic results.
   *
   * The backend performs the entire comparison (GroundTruthComparer) and
   * owns the pass/fail decision -- callers must render the result, never
   * recompute any part of it. The endpoint is stateless: nothing is
   * persisted, and the response carries no verification id or timestamp.
   */
  verifyGroundTruth(
    runId: string,
    rows: GroundTruthRow[],
  ): Observable<GroundTruthComparisonResult> {
    return this.http.post<GroundTruthComparisonResult>(
      `${this.baseUrl}/runs/${runId}/ground-truth-verification`,
      rows,
    );
  }

  getResults(
    runId: string,
    pageNumber: number,
    pageSize: number,
  ): Observable<PagedResponse<ReconciliationResultResponse>> {
    return this.http.get<PagedResponse<ReconciliationResultResponse>>(
      `${this.baseUrl}/runs/${runId}/results`,
      { params: { pageNumber, pageSize } },
    );
  }

  /** Maps to the backend's GetTransactionDetail action. */
  getResultDetail(
    runId: string,
    resultId: string,
  ): Observable<ReconciliationTransactionDetailResponse> {
    return this.http.get<ReconciliationTransactionDetailResponse>(
      `${this.baseUrl}/runs/${runId}/results/${resultId}`,
    );
  }

  getExceptions(
    runId: string,
    pageNumber: number,
    pageSize: number,
  ): Observable<PagedResponse<ReconciliationExceptionResponse>> {
    return this.http.get<PagedResponse<ReconciliationExceptionResponse>>(
      `${this.baseUrl}/runs/${runId}/exceptions`,
      { params: { pageNumber, pageSize } },
    );
  }

  /**
   * Maps to the backend's GetException action -- `api/reconciliation/
   * exceptions/{exceptionId}`. Deliberately takes no `runId`: unlike every
   * other single-item endpoint in this service, the real route is not
   * run-scoped.
   */
  getException(exceptionId: string): Observable<ReconciliationExceptionResponse> {
    return this.http.get<ReconciliationExceptionResponse>(
      `${this.baseUrl}/exceptions/${exceptionId}`,
    );
  }

  /**
   * Maps to the backend's GenerateAiExplanation action. No request body --
   * the backend derives every grounded fact itself from the exception
   * already persisted. The response is rendered as-is; this method (and
   * every caller) must never transform, recompute, or validate the
   * financial content of the result -- it is advisory text only.
   */
  generateAiExplanation(exceptionId: string): Observable<AiExplanationResponse> {
    return this.http.post<AiExplanationResponse>(
      `${this.baseUrl}/exceptions/${exceptionId}/ai-explanation`,
      null,
    );
  }

  /**
   * Read-only audit evidence for a run, from the backend's existing
   * audit_logs store -- the same events ReconciliationOrchestrator,
   * BatchIngestionService, AiExplanationService and FinanceAssistantService
   * already write. There is no corresponding write endpoint anywhere in
   * this API; this call can only ever read.
   */
  getAuditLog(
    runId: string,
    pageNumber: number,
    pageSize: number,
  ): Observable<PagedResponse<AuditLogEntryResponse>> {
    return this.http.get<PagedResponse<AuditLogEntryResponse>>(
      `${this.baseUrl}/runs/${runId}/audit`,
      { params: { pageNumber, pageSize } },
    );
  }

  /**
   * Maps to the backend's Finance Assistant `Ask` action --
   * `POST /api/finance-assistant/ask`. Not under `/api/reconciliation`, so
   * this builds its own URL rather than stretching `this.baseUrl`'s scoping
   * to cover an unrelated backend surface. `runId` must come from the
   * caller's current route/workspace context -- never invented here or
   * left for the user to type.
   */
  askFinanceAssistant(runId: string, question: string): Observable<FinanceAssistantResponse> {
    const request: FinanceAssistantRequest = { runId, question };
    return this.http.post<FinanceAssistantResponse>(
      `${environment.apiBaseUrl}/finance-assistant/ask`,
      request,
    );
  }
}
