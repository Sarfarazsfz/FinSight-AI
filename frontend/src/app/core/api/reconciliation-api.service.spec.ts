import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ReconciliationApi } from './reconciliation-api.service';
import { environment } from '../../../environments/environment';
import { isProblemDetails } from '../models/problem-details.model';
import type {
  ReconciliationRunDetailsResponse,
  ReconciliationRunResult,
} from '../models/reconciliation.model';

describe('ReconciliationApi', () => {
  let api: ReconciliationApi;
  let httpMock: HttpTestingController;

  const runsUrl = `${environment.apiBaseUrl}/reconciliation/runs`;
  const batchId = '11111111-1111-1111-1111-111111111111';
  const runId = '22222222-2222-2222-2222-222222222222';

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    api = TestBed.inject(ReconciliationApi);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  describe('createRun', () => {
    it('POSTs to the configured runs URL', () => {
      api.createRun(batchId).subscribe();

      const req = httpMock.expectOne((r) => r.url === runsUrl && r.method === 'POST');
      expect(req.request.method).toBe('POST');
      req.flush(makeResult());
    });

    it('sends exactly { batchId }, nothing else', () => {
      api.createRun(batchId).subscribe();

      const req = httpMock.expectOne((r) => r.url === runsUrl);
      expect(req.request.body).toEqual({ batchId });

      req.flush(makeResult());
    });

    it('maps a real 201 verbatim, including the numeric status untouched', () => {
      const wire = makeResult({ status: 2 });

      let received: ReconciliationRunResult | undefined;
      api.createRun(batchId).subscribe((r) => (received = r));

      httpMock.expectOne((r) => r.url === runsUrl).flush(wire, { status: 201, statusText: 'Created' });

      expect(received).toEqual(wire);
      expect(typeof received!.status).toBe('number');
      expect(received!.status).toBe(2);
    });

    it('surfaces a 400 ProblemDetails unmodified', () => {
      let body: unknown;

      api.createRun('').subscribe({
        next: () => fail('expected the 400 to error'),
        error: (err) => (body = err.error),
      });

      httpMock.expectOne((r) => r.url === runsUrl).flush(
        { title: 'Bad Request', status: 400, detail: 'A valid batchId is required.' },
        { status: 400, statusText: 'Bad Request' },
      );

      expect(isProblemDetails(body)).toBeTrue();
      expect((body as { detail: string }).detail).toBe('A valid batchId is required.');
    });

    it('surfaces a 404 ProblemDetails unmodified', () => {
      let body: unknown;

      api.createRun(batchId).subscribe({
        next: () => fail('expected the 404 to error'),
        error: (err) => (body = err.error),
      });

      httpMock.expectOne((r) => r.url === runsUrl).flush(
        { title: 'Resource Not Found', status: 404, detail: `Batch '${batchId}' was not found.` },
        { status: 404, statusText: 'Not Found' },
      );

      expect(isProblemDetails(body)).toBeTrue();
      expect((body as { detail: string }).detail).toBe(`Batch '${batchId}' was not found.`);
    });
  });

  describe('getRun', () => {
    it('GETs the configured run URL', () => {
      api.getRun(runId).subscribe();

      const req = httpMock.expectOne((r) => r.url === `${runsUrl}/${runId}` && r.method === 'GET');
      expect(req.request.method).toBe('GET');
      req.flush(makeDetails());
    });

    it('maps a real 200 verbatim, status as a string', () => {
      const wire = makeDetails({ status: 'Completed' });

      let received: ReconciliationRunDetailsResponse | undefined;
      api.getRun(runId).subscribe((r) => (received = r));

      httpMock.expectOne((r) => r.url === `${runsUrl}/${runId}`).flush(wire);

      expect(received).toEqual(wire);
      expect(typeof received!.status).toBe('string');
    });

    it('surfaces a 404 unmodified', () => {
      let body: unknown;

      api.getRun(runId).subscribe({
        next: () => fail('expected the 404 to error'),
        error: (err) => (body = err.error),
      });

      httpMock.expectOne((r) => r.url === `${runsUrl}/${runId}`).flush(
        { title: 'Resource Not Found', status: 404, detail: `Reconciliation run '${runId}' was not found.` },
        { status: 404, statusText: 'Not Found' },
      );

      expect(isProblemDetails(body)).toBeTrue();
      expect((body as { detail: string }).detail).toBe(
        `Reconciliation run '${runId}' was not found.`,
      );
    });
  });

  function makeResult(overrides: Partial<ReconciliationRunResult> = {}): ReconciliationRunResult {
    return {
      runId,
      batchId,
      status: 2,
      totalReconciliationUnits: 30,
      matchedCount: 25,
      mismatchedCount: 2,
      missingCount: 1,
      duplicateCount: 1,
      unresolvedCount: 1,
      matchRate: 83.33,
      ...overrides,
    };
  }

  function makeDetails(
    overrides: Partial<ReconciliationRunDetailsResponse> = {},
  ): ReconciliationRunDetailsResponse {
    return {
      runId,
      batchId,
      status: 'Completed',
      totalReconciliationUnits: 30,
      matchRate: 83.33,
      startedAt: '2026-08-29T09:00:00Z',
      completedAt: '2026-08-29T09:00:05Z',
      createdAt: '2026-08-29T09:00:00Z',
      ...overrides,
    };
  }
});
