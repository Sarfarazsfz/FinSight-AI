import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuditEvidencePanel } from './audit-evidence-panel';
import { environment } from '../../../../environments/environment';
import type { AuditLogEntryResponse } from '../../../core/models/reconciliation.model';
import type { PagedResponse } from '../../../core/models/paged-response.model';

describe('AuditEvidencePanel', () => {
  let fixture: ComponentFixture<AuditEvidencePanel>;
  let httpMock: HttpTestingController;

  const runId = '22222222-2222-2222-2222-222222222222';
  const auditUrl = `${environment.apiBaseUrl}/reconciliation/runs/${runId}/audit`;

  function configure(): void {
    TestBed.configureTestingModule({
      imports: [AuditEvidencePanel],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(AuditEvidencePanel);
    fixture.componentRef.setInput('runId', runId);
    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function entry(overrides: Partial<AuditLogEntryResponse> = {}): AuditLogEntryResponse {
    return {
      id: '33333333-3333-3333-3333-333333333333',
      occurredAt: '2026-08-29T09:00:05Z',
      eventType: 'ReconciliationCompleted',
      runId,
      relatedEntityType: 'ReconciliationRun',
      relatedEntityId: runId,
      detail: JSON.stringify({ run_id: runId, duration_ms: 42, records_per_second: 100 }),
      ...overrides,
    };
  }

  function page(
    items: AuditLogEntryResponse[],
    overrides: Partial<PagedResponse<AuditLogEntryResponse>> = {},
  ): PagedResponse<AuditLogEntryResponse> {
    return {
      items,
      pageNumber: 1,
      pageSize: 10,
      totalCount: items.length,
      totalPages: 1,
      ...overrides,
    };
  }

  function flushInitial(response: PagedResponse<AuditLogEntryResponse>): void {
    httpMock.expectOne((r) => r.url === auditUrl && r.method === 'GET').flush(response);
    fixture.detectChanges();
  }

  // ---------------------------------------------------------------------
  // Loading / empty / error states
  // ---------------------------------------------------------------------

  it('shows a loading skeleton before the first response arrives', () => {
    configure();
    expect(el().querySelector('[data-testid="audit-evidence-loading"]')).toBeTruthy();
    flushInitial(page([]));
  });

  it('shows an honest empty state, never a fabricated placeholder record, when there are no events', () => {
    configure();
    flushInitial(page([]));

    const empty = el().querySelector('[data-testid="audit-evidence-empty"]');
    expect(empty).toBeTruthy();
    expect(empty!.textContent).toContain('No audit evidence is available for this run.');
    expect(el().querySelector('[data-testid="audit-evidence-entry"]')).toBeFalsy();
  });

  it('renders a request failure with a retry action, and retry re-issues the request', () => {
    configure();

    httpMock
      .expectOne((r) => r.url === auditUrl)
      .flush({ detail: 'boom' }, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    const errorEl = el().querySelector('[data-testid="audit-evidence-error"]');
    expect(errorEl).toBeTruthy();

    el().querySelector<HTMLButtonElement>('[data-testid="audit-evidence-retry"]')!.click();
    fixture.detectChanges();

    flushInitial(page([entry()]));
    expect(el().querySelector('[data-testid="audit-evidence-entry"]')).toBeTruthy();
  });

  // ---------------------------------------------------------------------
  // Rendering real entries
  // ---------------------------------------------------------------------

  it('renders each entry\'s readable event label and timestamp, verbatim from the backend', () => {
    configure();
    flushInitial(page([entry({ eventType: 'ExceptionCreated' })]));

    const row = el().querySelector('[data-testid="audit-evidence-entry"]')!;
    expect(row.querySelector('[data-testid="audit-evidence-event-type"]')!.textContent).toContain(
      'Exception created',
    );
    expect(row.querySelector('time')!.getAttribute('datetime')).toBe('2026-08-29T09:00:05Z');
  });

  it('never fabricates or renders an actor/user identity field, because the backend does not provide one', () => {
    configure();
    flushInitial(page([entry()]));

    // The row's own text must not claim any per-user attribution --
    // AuditLog carries no actor column, so this view must not invent one.
    const row = el().querySelector('[data-testid="audit-evidence-entry"]')!;
    expect(row.textContent).not.toMatch(/actor|performed by|requested by/i);
  });

  it('shows the raw JSON detail only behind an explicit, keyboard-accessible disclosure', () => {
    configure();
    flushInitial(page([entry()]));

    const details = el().querySelector('details')!;
    const payload = el().querySelector('[data-testid="audit-evidence-detail-payload"]')!;

    expect(details.hasAttribute('open')).toBeFalse();
    expect(payload.textContent).toContain('"duration_ms": 42');
  });

  it('falls back to the raw string instead of crashing when the detail payload is not valid JSON', () => {
    configure();
    flushInitial(page([entry({ detail: 'not-json-at-all' })]));

    const payload = el().querySelector('[data-testid="audit-evidence-detail-payload"]')!;
    expect(payload.textContent).toContain('not-json-at-all');
  });

  it('surfaces duration/throughput for a ReconciliationCompleted event, labelled as a single-run measurement', () => {
    configure();
    flushInitial(page([entry()]));

    const row = el().querySelector('[data-testid="audit-evidence-entry"]')!;
    expect(row.textContent).toContain('42');
    expect(row.textContent).toContain('100');
    expect(row.textContent?.toLowerCase()).toContain('not a benchmark');
  });

  it('does not show a throughput line for an event type that carries no duration/throughput fields', () => {
    configure();
    flushInitial(
      page([entry({ eventType: 'ExceptionCreated', detail: JSON.stringify({ run_id: runId }) })]),
    );

    const row = el().querySelector('[data-testid="audit-evidence-entry"]')!;
    expect(row.textContent?.toLowerCase()).not.toContain('benchmark');
  });

  it('marks a failure event distinctly by icon and text, never by colour alone', () => {
    configure();
    flushInitial(page([entry({ eventType: 'ReconciliationFailed' })]));

    const row = el().querySelector('[data-testid="audit-evidence-entry"]')!;
    // The label text itself says "failed" -- the accessible name does not
    // depend on perceiving a colour.
    expect(row.querySelector('[data-testid="audit-evidence-event-type"]')!.textContent).toContain(
      'Reconciliation failed',
    );
  });

  // ---------------------------------------------------------------------
  // Pagination ("load more")
  // ---------------------------------------------------------------------

  it('offers "Load older events" only when more exist, and appends the next page on click', () => {
    configure();
    flushInitial(page([entry({ id: 'a' })], { totalCount: 2, pageNumber: 1 }));

    const loadMore = el().querySelector<HTMLButtonElement>('[data-testid="audit-evidence-load-more"]');
    expect(loadMore).toBeTruthy();

    loadMore!.click();
    fixture.detectChanges();

    httpMock
      .expectOne((r) => r.url === auditUrl && r.params.get('pageNumber') === '2')
      .flush(page([entry({ id: 'b' })], { totalCount: 2, pageNumber: 2 }));
    fixture.detectChanges();

    expect(el().querySelectorAll('[data-testid="audit-evidence-entry"]').length).toBe(2);
    expect(el().querySelector('[data-testid="audit-evidence-load-more"]')).toBeFalsy();
  });

  it('hides "Load older events" when the first page already contains everything', () => {
    configure();
    flushInitial(page([entry()], { totalCount: 1 }));

    expect(el().querySelector('[data-testid="audit-evidence-load-more"]')).toBeFalsy();
  });

  // ---------------------------------------------------------------------
  // Read-only surface
  // ---------------------------------------------------------------------

  it('issues only GET requests -- this panel has no create/update/delete affordance of any kind', () => {
    configure();
    flushInitial(page([entry()]));

    expect(el().querySelector('form')).toBeFalsy();
    expect(el().querySelectorAll('input, textarea').length).toBe(0);
  });
});
