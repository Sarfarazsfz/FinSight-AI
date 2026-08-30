import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import type {
  AiExplanationResponse,
  FinanceAssistantRequest,
  FinanceAssistantResponse,
  ReconciliationExceptionResponse,
  ReconciliationResultResponse,
  ReconciliationRunDetailsResponse,
  ReconciliationRunRequest,
  ReconciliationRunResult,
  ReconciliationTransactionDetailResponse,
} from '../models/reconciliation.model';
import type { PagedResponse } from '../models/paged-response.model';

/**
 * Thin typed wrapper over the reconciliation endpoints this frontend
 * actually consumes: creating a run, reading one back, listing its results,
 * reading one result's source evidence, listing/reading exceptions, and
 * requesting an AI explanation for one exception.
 *
 * `summary` and ground-truth verification are real backend capabilities
 * with no frontend consumer yet -- no method for them exists here. Add each
 * only when the phase that actually builds its screen is being implemented.
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
