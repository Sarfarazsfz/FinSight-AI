import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import type { BatchIngestionResult, BatchResponse } from '../models/batch.model';
import type { PagedResponse } from '../models/paged-response.model';

/**
 * Thin typed wrapper over the batch endpoints this frontend actually
 * consumes: `GET /api/batches` (server-side pagination) and
 * `POST /api/batches` (multipart upload).
 *
 * `GET /api/batches/{batchId}` (detail) has no real consumer yet, so it
 * still has no method here. Add it only when the feature that needs it is
 * actually being built.
 */
@Injectable({ providedIn: 'root' })
export class BatchApi {
  private readonly http = inject(HttpClient);

  private readonly baseUrl = `${environment.apiBaseUrl}/batches`;

  getBatches(
    pageNumber: number,
    pageSize: number,
  ): Observable<PagedResponse<BatchResponse>> {
    return this.http.get<PagedResponse<BatchResponse>>(this.baseUrl, {
      params: { pageNumber, pageSize },
    });
  }

  /**
   * Posts the three source CSVs as `multipart/form-data`, matching
   * `BatchesController.CreateBatch` exactly: `batchLabel`, `createdBy`,
   * `paymentsFile`, `bankFile`, `settlementsFile` -- no other field exists
   * on that endpoint.
   *
   * The `Content-Type` header is deliberately never set here -- the browser
   * must generate the multipart boundary itself. Setting it manually on a
   * `FormData` body corrupts the boundary and breaks the request.
   */
  createBatch(
    batchLabel: string,
    createdBy: string,
    paymentsFile: File,
    bankFile: File,
    settlementsFile: File,
  ): Observable<BatchIngestionResult> {
    const formData = new FormData();
    formData.append('batchLabel', batchLabel);
    formData.append('createdBy', createdBy);
    formData.append('paymentsFile', paymentsFile);
    formData.append('bankFile', bankFile);
    formData.append('settlementsFile', settlementsFile);

    return this.http.post<BatchIngestionResult>(this.baseUrl, formData);
  }
}
