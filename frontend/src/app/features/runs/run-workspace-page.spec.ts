import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { RunWorkspacePage } from './run-workspace-page';
import { environment } from '../../../environments/environment';
import type { BatchResponse } from '../../core/models/batch.model';
import type {
  ReconciliationRunDetailsResponse,
  ReconciliationRunStatus,
} from '../../core/models/reconciliation.model';

describe('RunWorkspacePage', () => {
  let fixture: ComponentFixture<RunWorkspacePage>;
  let httpMock: HttpTestingController;

  const runId = '22222222-2222-2222-2222-222222222222';
  const batchId = '11111111-1111-1111-1111-111111111111';
  const runsUrl = `${environment.apiBaseUrl}/reconciliation/runs`;
  const runUrl = `${runsUrl}/${runId}`;
  const batchUrl = `${environment.apiBaseUrl}/batches/${batchId}`;

  function configure(): void {
    TestBed.configureTestingModule({
      imports: [RunWorkspacePage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ runId }) } },
        },
      ],
    });

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(RunWorkspacePage);
    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function runDetails(
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

  function batchResponse(overrides: Partial<BatchResponse> = {}): BatchResponse {
    return {
      batchId,
      batchLabel: 'August Batch 1',
      paymentRecordCount: 10,
      bankRecordCount: 10,
      settlementRecordCount: 10,
      totalRecordCount: 30,
      validationStatus: 'Valid',
      createdBy: 'ops-analyst@finsight.test',
      createdAt: '2026-08-29T08:00:00Z',
      ...overrides,
    };
  }

  it('issues the run request first, then the batch request using the run’s batchId', () => {
    configure();

    const runReq = httpMock.expectOne((r) => r.url === runUrl && r.method === 'GET');
    runReq.flush(runDetails());

    const batchReq = httpMock.expectOne((r) => r.url === batchUrl && r.method === 'GET');
    expect(batchReq.request.method).toBe('GET');
    batchReq.flush(batchResponse());
  });

  it('shows a loading state before either request resolves', () => {
    configure();

    expect(el().querySelector('[data-testid="run-loading"]')).toBeTruthy();

    httpMock.expectOne((r) => r.url === runUrl).flush(runDetails());
    httpMock.expectOne((r) => r.url === batchUrl).flush(batchResponse());
  });

  it('renders the real response verbatim, with no recomputed or invented values', () => {
    configure();

    httpMock.expectOne((r) => r.url === runUrl).flush(
      runDetails({
        status: 'Completed',
        totalReconciliationUnits: 353,
        matchRate: 91.5,
        completedAt: '2026-08-29T09:05:00Z',
      }),
    );
    httpMock.expectOne((r) => r.url === batchUrl).flush(batchResponse({ batchLabel: 'August Batch 1' }));
    fixture.detectChanges();

    const text = el().textContent!;
    expect(text).toContain('August Batch 1');
    expect(text).toContain('Completed');
    expect(text).toContain('353');
    expect(text).toContain('91.5%');
    expect(el().querySelector('[data-testid="run-status"]')!.textContent).toContain('Completed');
  });

  it('shows "—" for a null completedAt and null matchRate, never a fabricated value', () => {
    configure();

    httpMock.expectOne((r) => r.url === runUrl).flush(
      runDetails({ status: 'Running', completedAt: null, matchRate: null }),
    );
    httpMock.expectOne((r) => r.url === batchUrl).flush(batchResponse());
    fixture.detectChanges();

    const metadata = el().querySelector('[data-testid="run-metadata"]')!.textContent!;
    expect(metadata).toContain('—');
  });

  it('renders a not-found state on a real 404, with no retry loop, only a link back to Batches', () => {
    configure();

    httpMock.expectOne((r) => r.url === runUrl).flush(
      { title: 'Resource Not Found', status: 404, detail: `Reconciliation run '${runId}' was not found.` },
      { status: 404, statusText: 'Not Found' },
    );
    fixture.detectChanges();

    const notFound = el().querySelector('[data-testid="run-not-found"]');
    expect(notFound).toBeTruthy();
    expect(notFound!.querySelector('button')).toBeFalsy();
    expect(el().querySelector('a[href="/batches"]')).toBeTruthy();
  });

  it('renders a generic error state with Retry on a non-404 failure', () => {
    configure();

    httpMock.expectOne((r) => r.url === runUrl).flush(
      { title: 'An unexpected error occurred.', status: 500 },
      { status: 500, statusText: 'Internal Server Error' },
    );
    fixture.detectChanges();

    const error = el().querySelector('[data-testid="run-error"]');
    expect(error).toBeTruthy();
    expect(error!.querySelector('button')).toBeTruthy();
  });

  it('retry re-issues the run request', () => {
    configure();

    httpMock.expectOne((r) => r.url === runUrl).flush(
      { title: 'Internal Server Error', status: 500 },
      { status: 500, statusText: 'Internal Server Error' },
    );
    fixture.detectChanges();

    el().querySelector<HTMLButtonElement>('[data-testid="run-error"] button')!.click();

    const req = httpMock.expectOne((r) => r.url === runUrl);
    expect(req.request.method).toBe('GET');
    req.flush(runDetails());
    httpMock.expectOne((r) => r.url === batchUrl).flush(batchResponse());
  });

  it('does not render bespoke session-expired copy on a 401 — that is the global interceptor’s job', () => {
    configure();

    httpMock.expectOne((r) => r.url === runUrl).flush(
      { title: 'Unauthorized', status: 401 },
      { status: 401, statusText: 'Unauthorized' },
    );
    fixture.detectChanges();

    const text = el().textContent!.toLowerCase();
    expect(text).not.toContain('session');
    expect(text).not.toContain('expired');
  });

  it('falls back to the raw batch id when the batch label fetch fails, without failing the whole page', () => {
    configure();

    httpMock.expectOne((r) => r.url === runUrl).flush(runDetails());
    httpMock.expectOne((r) => r.url === batchUrl).flush(
      { title: 'Resource Not Found', status: 404, detail: `Batch '${batchId}' was not found.` },
      { status: 404, statusText: 'Not Found' },
    );
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="run-metadata"]')).toBeTruthy();
    expect(el().querySelector('h1')!.textContent).toContain(batchId);
  });

  const statuses: ReconciliationRunStatus[] = ['Pending', 'Running', 'Completed', 'Failed'];

  for (const status of statuses) {
    it(`renders the real "${status}" status distinctly`, () => {
      configure();

      httpMock.expectOne((r) => r.url === runUrl).flush(runDetails({ status }));
      httpMock.expectOne((r) => r.url === batchUrl).flush(batchResponse());
      fixture.detectChanges();

      const badge = el().querySelector('[data-testid="run-status"]')!;
      expect(badge.textContent).toContain(status);
    });
  }

  it('shows a manual Refresh action for a Running run, never automatic polling', () => {
    configure();

    httpMock.expectOne((r) => r.url === runUrl).flush(runDetails({ status: 'Running', completedAt: null }));
    httpMock.expectOne((r) => r.url === batchUrl).flush(batchResponse());
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="run-refresh"]')).toBeTruthy();

    // No further request should appear on its own -- only a click issues one.
    const requests = httpMock.match(() => true);
    expect(requests.length).toBe(0);
  });

  it('hides the Refresh action once a run has reached a terminal status', () => {
    configure();

    httpMock.expectOne((r) => r.url === runUrl).flush(runDetails({ status: 'Completed' }));
    httpMock.expectOne((r) => r.url === batchUrl).flush(batchResponse());
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="run-refresh"]')).toBeFalsy();
  });
});
