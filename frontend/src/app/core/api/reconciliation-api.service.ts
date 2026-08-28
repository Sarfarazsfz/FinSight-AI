import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import type {
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
 * and reading one result's source evidence.
 *
 * `summary`, `exceptions`, AI explanation and ground-truth verification are
 * all real backend capabilities with no frontend consumer yet -- no method
 * for them exists here. Add each only when the phase that actually builds
 * its screen is being implemented.
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
}
