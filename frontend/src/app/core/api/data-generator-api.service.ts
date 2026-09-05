import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

// ---------------------------------------------------------------------------
// Enums (must match backend GenerationMode and CorruptionIntensity values)
// ---------------------------------------------------------------------------

export enum GenerationMode {
  Clean           = 0,
  AmountMismatch  = 1,
  DateMismatch    = 2,
  MissingBank     = 3,
  MissingSettlement = 4,
  MissingPayment  = 5,
  Duplicate       = 6,
  Unresolved      = 7,
  Mixed           = 8,
  RandomChaos     = 9,
}

export enum CorruptionIntensity {
  Low    = 0,
  Medium = 1,
  High   = 2,
}

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------

export interface GenerateDatasetRequest {
  size: number;
  mode: GenerationMode;
  intensity: CorruptionIntensity;
  seed?: number | null;
}

export interface GeneratedDatasetMetadata {
  generationId: string;
  seed: number;
  mode: GenerationMode;
  size: number;
  intensity: CorruptionIntensity | null;
  createdAt: string;
  scenarioDistribution: Record<string, number>;
  expectedMatched: number;
  expectedMismatched: number;
  expectedMissing: number;
  expectedDuplicate: number;
  expectedUnresolved: number;
}

export interface GenerateDatasetResponse {
  metadata: GeneratedDatasetMetadata;
}

// ---------------------------------------------------------------------------
// Service
// ---------------------------------------------------------------------------

@Injectable({ providedIn: 'root' })
export class DataGeneratorApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/test-data`;

  generate(request: GenerateDatasetRequest): Observable<GenerateDatasetResponse> {
    return this.http.post<GenerateDatasetResponse>(`${this.base}/generate`, request);
  }

  /**
   * Downloads a generated CSV file as a Blob so the Bearer token
   * (attached by the auth interceptor) is sent with the request.
   * window.open() cannot send an Authorization header, so we use
   * HttpClient + createObjectURL instead.
   */
  downloadFile(
    generationId: string,
    fileType: 'payments' | 'bank' | 'settlements' | 'ground-truth',
  ): Observable<Blob> {
    return this.http.get(
      `${this.base}/download/${generationId}/${fileType}`,
      { responseType: 'blob' },
    );
  }
}
