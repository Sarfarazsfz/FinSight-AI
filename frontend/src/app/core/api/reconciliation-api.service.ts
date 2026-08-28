import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import type {
  ReconciliationRunDetailsResponse,
  ReconciliationRunRequest,
  ReconciliationRunResult,
} from '../models/reconciliation.model';

/**
 * Thin typed wrapper over the two reconciliation endpoints F6 consumes:
 * creating a run and reading one back.
 *
 * `summary`, `results`, `exceptions`, transaction detail, AI explanation and
 * ground-truth verification are all real backend capabilities with no
 * frontend consumer yet -- no method for them exists here. Add each only
 * when the phase that actually builds its screen is being implemented.
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
}
