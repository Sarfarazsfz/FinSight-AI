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
  ReconciliationRunSummaryResponse,
} from '../../core/models/reconciliation.model';

describe('RunWorkspacePage', () => {
  let fixture: ComponentFixture<RunWorkspacePage>;
  let httpMock: HttpTestingController;

  const runId = '22222222-2222-2222-2222-222222222222';
  const batchId = '11111111-1111-1111-1111-111111111111';
  const runsUrl = `${environment.apiBaseUrl}/reconciliation/runs`;
  const runUrl = `${runsUrl}/${runId}`;
  const summaryUrl = `${runUrl}/summary`;
  const batchUrl = `${environment.apiBaseUrl}/batches/${batchId}`;
  const auditUrl = `${runUrl}/audit`;

  function emptyAuditPage() {
    return { items: [], pageNumber: 1, pageSize: 10, totalCount: 0, totalPages: 0 };
  }

  function summaryResponse(
    overrides: Partial<ReconciliationRunSummaryResponse> = {},
  ): ReconciliationRunSummaryResponse {
    return {
      runId,
      batchId,
      status: 'Completed',
      totalUnits: 100,
      matched: 97,
      mismatched: 1,
      missing: 2,
      duplicate: 0,
      unresolved: 0,
      matchRate: 97,
      exceptionCount: 3,
      // Server-computed from the run's persisted timestamps.
      durationMs: 50,
      recordsPerSecond: 2000,
      ...overrides,
    };
  }

  /** Flushes the workspace's secondary summary request. */
  function flushSummary(
    overrides: Partial<ReconciliationRunSummaryResponse> = {},
  ): void {
    httpMock
      .expectOne((r) => r.url === summaryUrl && r.method === 'GET')
      .flush(summaryResponse(overrides));
    fixture.detectChanges();
  }

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

  afterEach(() => {
    // The workspace also issues a secondary GET .../summary to populate the
    // reconciliation breakdown. Tests written before that section exists
    // assert run/batch behaviour and deliberately don't stub it, so drain
    // it here -- verify() still catches genuinely unexpected requests.
    // Dedicated coverage lives in the "reconciliation breakdown" block.
    httpMock
      .match((r) => r.url === summaryUrl)
      .forEach((request) => request.flush(summaryResponse()));

    // Same reasoning for the embedded AuditEvidencePanel's own
    // GET .../audit request -- it fetches independently of everything
    // above the instant the Run Workspace reaches its "loaded" state.
    // Dedicated coverage lives in AuditEvidencePanel's own spec.
    httpMock
      .match((r) => r.url === auditUrl)
      .forEach((request) => request.flush(emptyAuditPage()));

    httpMock.verify();
  });

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

    // No further request should appear on its own -- only a click issues
    // one. The one-shot breakdown summary and audit-evidence fetches are
    // excluded: both are secondary fetches for this same load, not a poll.
    const requests = httpMock.match((r) => r.url !== summaryUrl && r.url !== auditUrl);
    expect(requests.length).toBe(0);
  });

  it('hides the Refresh action once a run has reached a terminal status', () => {
    configure();

    httpMock.expectOne((r) => r.url === runUrl).flush(runDetails({ status: 'Completed' }));
    httpMock.expectOne((r) => r.url === batchUrl).flush(batchResponse());
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="run-refresh"]')).toBeFalsy();
  });

  it('renders the Finance Assistant panel scoped to the current run once loaded, without issuing any request of its own', () => {
    configure();

    httpMock.expectOne((r) => r.url === runUrl).flush(runDetails());
    httpMock.expectOne((r) => r.url === batchUrl).flush(batchResponse());
    fixture.detectChanges();

    const panel = el().querySelector('app-finance-assistant-panel');
    expect(panel).toBeTruthy();
    expect(panel!.querySelector('[data-testid="assistant-empty-state"]')).toBeTruthy();

    // The panel itself must not have called the Finance Assistant endpoint
    // on load -- AI runs only after an explicit user submission.
    const financeAssistantRequests = httpMock.match(
      (r) => r.url === `${environment.apiBaseUrl}/finance-assistant/ask`,
    );
    expect(financeAssistantRequests.length).toBe(0);
  });

  it('renders a desktop close button for the Finance Assistant rail, and closing it hides the rail without destroying the panel', () => {
    configure();

    httpMock.expectOne((r) => r.url === runUrl).flush(runDetails());
    httpMock.expectOne((r) => r.url === batchUrl).flush(batchResponse());
    fixture.detectChanges();

    const closeButton = el().querySelector<HTMLButtonElement>('[data-testid="assistant-close-button"]');
    expect(closeButton).toBeTruthy();

    closeButton!.click();
    fixture.detectChanges();

    // The rail element itself collapses to bare `hidden` (display:none,
    // no lg:flex override left to win at any width), but
    // app-finance-assistant-panel must still be present in the DOM --
    // never destroyed by closing -- so its conversation state survives.
    const aside = el().querySelector('aside[aria-label="Finance Assistant Rail"]');
    expect(aside?.className.trim()).toBe('hidden');
    expect(aside?.querySelector('app-finance-assistant-panel')).toBeTruthy();
  });

  it('shows a reopen trigger once the desktop rail is closed, and reopening restores the same conversation', () => {
    configure();

    httpMock.expectOne((r) => r.url === runUrl).flush(runDetails());
    httpMock.expectOne((r) => r.url === batchUrl).flush(batchResponse());
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="open-assistant-desktop-button"]')).toBeFalsy();

    // Ask a question so there is real conversation state to lose if
    // closing were to destroy the panel.
    const questionInput = el().querySelector<HTMLTextAreaElement>(
      '[data-testid="assistant-question-input"]',
    )!;
    questionInput.value = 'What is the match rate?';
    questionInput.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    el().querySelector<HTMLButtonElement>('[data-testid="assistant-ask-button"]')!.click();

    const askUrl = `${environment.apiBaseUrl}/finance-assistant/ask`;
    httpMock
      .expectOne((r) => r.url === askUrl)
      .flush({ answer: 'The match rate is 91.5%.', toolsUsed: ['getReconciliationSummary'], traceId: null });
    fixture.detectChanges();

    el().querySelector<HTMLButtonElement>('[data-testid="assistant-close-button"]')!.click();
    fixture.detectChanges();

    const reopenButton = el().querySelector<HTMLButtonElement>('[data-testid="open-assistant-desktop-button"]');
    expect(reopenButton).toBeTruthy();
    expect(reopenButton!.getAttribute('aria-label')).toBe('Open Finance Assistant');

    reopenButton!.click();
    fixture.detectChanges();

    // Reopened: the full responsive class string (base `hidden`,
    // overridden by `lg:flex` at desktop widths) is restored -- distinct
    // from the closed state's bare `hidden` with no override at all.
    const aside = el().querySelector('aside[aria-label="Finance Assistant Rail"]');
    expect(aside?.className).toContain('lg:flex');
    expect(aside?.textContent).toContain('The match rate is 91.5%.');
  });

  it('mobile drawer: closing does not destroy the panel, and reopening restores the same conversation', () => {
    configure();

    httpMock.expectOne((r) => r.url === runUrl).flush(runDetails());
    httpMock.expectOne((r) => r.url === batchUrl).flush(batchResponse());
    fixture.detectChanges();

    el().querySelector<HTMLButtonElement>('[data-testid="open-assistant-mobile-button"]')!.click();
    fixture.detectChanges();

    const mobilePanel = () =>
      el().querySelectorAll('app-finance-assistant-panel')[1] as HTMLElement | undefined;

    expect(mobilePanel()).toBeTruthy();

    const questionInput = mobilePanel()!.querySelector<HTMLTextAreaElement>(
      '[data-testid="assistant-question-input"]',
    )!;
    questionInput.value = 'Which records are missing?';
    questionInput.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    mobilePanel()!.querySelector<HTMLButtonElement>('[data-testid="assistant-ask-button"]')!.click();

    const askUrl = `${environment.apiBaseUrl}/finance-assistant/ask`;
    httpMock
      .expectOne((r) => r.url === askUrl)
      .flush({ answer: '3 records are missing settlement.', toolsUsed: ['getUnmatchedRecords'], traceId: null });
    fixture.detectChanges();

    // Close via the panel's own close button inside the drawer.
    mobilePanel()!.querySelector<HTMLButtonElement>('[data-testid="assistant-close-button"]')!.click();
    fixture.detectChanges();

    // Still mounted -- an @if would have destroyed it here.
    expect(mobilePanel()).toBeTruthy();
    expect(mobilePanel()!.textContent).toContain('3 records are missing settlement.');

    el().querySelector<HTMLButtonElement>('[data-testid="open-assistant-mobile-button"]')!.click();
    fixture.detectChanges();

    expect(mobilePanel()!.textContent).toContain('3 records are missing settlement.');
  });

  it('does not render the Finance Assistant panel while the run is still loading or has failed to load', () => {
    configure();

    expect(el().querySelector('app-finance-assistant-panel')).toBeFalsy();

    httpMock.expectOne((r) => r.url === runUrl).flush(
      { title: 'Resource Not Found', status: 404, detail: `Reconciliation run '${runId}' was not found.` },
      { status: 404, statusText: 'Not Found' },
    );
    fixture.detectChanges();

    expect(el().querySelector('app-finance-assistant-panel')).toBeFalsy();
  });

  // ---------------------------------------------------------------------
  // Reconciliation breakdown (five-count) + ground-truth framing
  // ---------------------------------------------------------------------

  describe('reconciliation breakdown', () => {
    /** Loads the workspace with a summary already applied. */
    function loadWith(
      overrides: Partial<ReconciliationRunSummaryResponse> = {},
    ): void {
      configure();
      httpMock.expectOne((r) => r.url === runUrl).flush(runDetails());
      httpMock.expectOne((r) => r.url === batchUrl).flush(batchResponse());
      fixture.detectChanges();
      flushSummary(overrides);
    }

    function countOf(key: string): string {
      return el()
        .querySelector(`[data-testid="breakdown-count-${key}"]`)!
        .textContent!.trim();
    }

    it('requests the whole-run summary endpoint, not a page of results', () => {
      configure();
      httpMock.expectOne((r) => r.url === runUrl).flush(runDetails());

      const request = httpMock.expectOne(
        (r) => r.url === summaryUrl && r.method === 'GET',
      );

      // No paging parameters: this is an aggregate over the entire run.
      expect(request.request.params.keys().length).toBe(0);

      request.flush(summaryResponse());
      httpMock.expectOne((r) => r.url === batchUrl).flush(batchResponse());
      fixture.detectChanges();
    });

    it('renders all five statuses with their server-authoritative counts', () => {
      loadWith({
        totalUnits: 100,
        matched: 97,
        mismatched: 1,
        missing: 2,
        duplicate: 0,
        unresolved: 0,
      });

      expect(el().querySelector('[data-testid="reconciliation-breakdown"]')).toBeTruthy();

      expect(countOf('matched')).toBe('97');
      expect(countOf('mismatched')).toBe('1');
      expect(countOf('missing')).toBe('2');
      expect(countOf('duplicate')).toBe('0');
      expect(countOf('unresolved')).toBe('0');
    });

    it('renders zero-value categories rather than hiding them', () => {
      loadWith({
        totalUnits: 10,
        matched: 10,
        mismatched: 0,
        missing: 0,
        duplicate: 0,
        unresolved: 0,
      });

      // A collapsed or omitted category would make the exception list look
      // curated -- every outcome stays visible even at zero.
      for (const key of ['mismatched', 'missing', 'duplicate', 'unresolved']) {
        expect(el().querySelector(`[data-testid="breakdown-${key}"]`))
          .withContext(key)
          .toBeTruthy();
        expect(countOf(key)).toBe('0');
      }
    });

    it('keeps the five outcomes distinct, never collapsing them into one "unmatched" figure', () => {
      loadWith({
        totalUnits: 20,
        matched: 10,
        mismatched: 4,
        missing: 3,
        duplicate: 2,
        unresolved: 1,
      });

      const grid = el().querySelector('[data-testid="breakdown-grid"]')!;

      expect(grid.querySelectorAll('a').length).toBe(5);
      expect(countOf('mismatched')).toBe('4');
      expect(countOf('missing')).toBe('3');
      expect(countOf('duplicate')).toBe('2');
      expect(countOf('unresolved')).toBe('1');

      // Scoped to the breakdown: the Finance Assistant's suggested
      // questions legitimately use the word "unmatched" in prose.
      const breakdown = el().querySelector(
        '[data-testid="reconciliation-breakdown"]',
      )!;
      expect(breakdown.textContent!.toLowerCase()).not.toContain('unmatched');
    });

    it('shows that the five counts account for the total exactly', () => {
      loadWith({
        totalUnits: 100,
        matched: 97,
        mismatched: 1,
        missing: 2,
        duplicate: 0,
        unresolved: 0,
        exceptionCount: 3,
      });

      const completeness = el().querySelector(
        '[data-testid="breakdown-completeness"]',
      );

      expect(completeness).toBeTruthy();
      expect(completeness!.textContent).toContain('= 100 units');
      expect(el().querySelector('[data-testid="breakdown-mismatch"]')).toBeNull();
    });

    it('flags rather than hides a summary whose counts do not sum to the total', () => {
      loadWith({
        totalUnits: 100,
        matched: 50,
        mismatched: 1,
        missing: 1,
        duplicate: 0,
        unresolved: 0,
      });

      const warning = el().querySelector('[data-testid="breakdown-mismatch"]');

      expect(warning).toBeTruthy();
      expect(warning!.getAttribute('role')).toBe('alert');
      expect(el().querySelector('[data-testid="breakdown-completeness"]')).toBeNull();
    });

    it('reports the match rate from the server, agreeing with the run details', () => {
      // Both surfaces read the same persisted ReconciliationRun.MatchRate,
      // so the breakdown must echo whatever the run details reported --
      // never a separately recomputed figure.
      const serverMatchRate = runDetails().matchRate!;

      loadWith({ matchRate: serverMatchRate });

      const breakdown = el().querySelector('[data-testid="reconciliation-breakdown"]')!;
      const metadata = el().querySelector('[data-testid="run-metadata"]')!;

      expect(breakdown.textContent).toContain(`${serverMatchRate}%`);
      expect(metadata.textContent).toContain(`${serverMatchRate}%`);
    });

    it('shows a skeleton while the summary is still in flight', () => {
      configure();
      httpMock.expectOne((r) => r.url === runUrl).flush(runDetails());
      httpMock.expectOne((r) => r.url === batchUrl).flush(batchResponse());
      fixture.detectChanges();

      expect(el().querySelector('[data-testid="breakdown-loading"]')).toBeTruthy();
      expect(el().querySelector('[data-testid="breakdown-grid"]')).toBeNull();

      flushSummary();

      expect(el().querySelector('[data-testid="breakdown-loading"]')).toBeNull();
      expect(el().querySelector('[data-testid="breakdown-grid"]')).toBeTruthy();
    });

    it('states the breakdown is unavailable rather than estimating it when the summary fails', () => {
      configure();
      httpMock.expectOne((r) => r.url === runUrl).flush(runDetails());
      httpMock.expectOne((r) => r.url === batchUrl).flush(batchResponse());
      fixture.detectChanges();

      httpMock
        .expectOne((r) => r.url === summaryUrl)
        .flush({ title: 'Server Error', status: 500 }, { status: 500, statusText: 'Error' });
      fixture.detectChanges();

      expect(el().querySelector('[data-testid="breakdown-unavailable"]')).toBeTruthy();
      expect(el().querySelector('[data-testid="breakdown-grid"]')).toBeNull();

      // The rest of the run is unaffected by a secondary failure.
      expect(el().querySelector('[data-testid="run-metadata"]')).toBeTruthy();
    });

    it('marks the breakdown as deterministic and subordinates the assistant to it', () => {
      loadWith();

      expect(el().querySelector('[data-testid="ground-truth-badge"]')!.textContent)
        .toContain('Deterministic engine');

      const text = el().textContent!;
      expect(text).toContain(
        'Deterministic reconciliation results are the financial source of truth.',
      );
      expect(text).toContain('it does not determine them');
    });

    it('links each count to a page where those units can actually be seen', () => {
      loadWith();

      const matched = el().querySelector<HTMLAnchorElement>(
        '[data-testid="breakdown-matched"]',
      )!;
      const missing = el().querySelector<HTMLAnchorElement>(
        '[data-testid="breakdown-missing"]',
      )!;

      expect(matched.getAttribute('href')).toBe(`/runs/${runId}/results`);
      expect(missing.getAttribute('href')).toBe(`/runs/${runId}/exceptions`);
    });

    it('offers a route to independent ground-truth verification', () => {
      loadWith();

      const link = el().querySelector<HTMLAnchorElement>(
        '[data-testid="verify-ground-truth-link"]',
      );

      expect(link).toBeTruthy();
      expect(link!.getAttribute('href')).toBe(`/runs/${runId}/verify`);
      expect(link!.textContent).toContain('Verify against ground truth');
    });

    it('renders run performance from the server-supplied figures', () => {
      loadWith({ totalUnits: 100, durationMs: 52.072, recordsPerSecond: 1920.4 });

      expect(el().querySelector('[data-testid="run-performance"]')).toBeTruthy();

      expect(el().querySelector('[data-testid="throughput-units"]')!.textContent!.trim())
        .toBe('100');
      expect(el().querySelector('[data-testid="throughput-duration"]')!.textContent)
        .toContain('52.1');
      expect(el().querySelector('[data-testid="throughput-rate"]')!.textContent)
        .toContain('1,920');
    });

    it('reports no duration rather than a number when the run has not completed', () => {
      loadWith({ durationMs: null, recordsPerSecond: null });

      expect(el().querySelector('[data-testid="throughput-unavailable"]')).toBeTruthy();
      expect(el().querySelector('[data-testid="throughput-duration"]')).toBeNull();
      expect(el().querySelector('[data-testid="throughput-rate"]')).toBeNull();
    });

    it('does not call the measurement a benchmark', () => {
      loadWith();

      const performance = el().querySelector('[data-testid="run-performance"]')!;
      const text = performance.textContent!.toLowerCase();

      // No cold/warm harness exists, so none may be implied.
      expect(text).toContain('not a benchmark');
      expect(text).not.toContain('cold run');
      expect(text).not.toContain('production throughput');
    });

    it('gives every count an accessible name that does not rely on colour', () => {
      loadWith({ totalUnits: 100, missing: 2 });

      const missing = el().querySelector('[data-testid="breakdown-missing"]')!;
      const label = missing.getAttribute('aria-label')!;

      expect(label).toContain('Missing');
      expect(label).toContain('2');
      expect(label).toContain('100');

      // The coloured dot is decorative; the text carries the meaning.
      expect(missing.querySelector('[aria-hidden="true"]')).toBeTruthy();
      expect(missing.textContent).toContain('Missing');
    });
  });
});
