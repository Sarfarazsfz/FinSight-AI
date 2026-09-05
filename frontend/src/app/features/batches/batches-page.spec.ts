import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { BatchesPage } from './batches-page';
import { environment } from '../../../environments/environment';
import type { BatchResponse } from '../../core/models/batch.model';
import type { PagedResponse } from '../../core/models/paged-response.model';

describe('BatchesPage', () => {
  let fixture: ComponentFixture<BatchesPage>;
  let httpMock: HttpTestingController;
  let router: Router;

  const batchesUrl = `${environment.apiBaseUrl}/batches`;
  const runsUrl = `${environment.apiBaseUrl}/reconciliation/runs`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [BatchesPage],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl');
    fixture = TestBed.createComponent(BatchesPage);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function expectInitialRequest() {
    return httpMock.expectOne(
      (r) => r.url === batchesUrl && r.method === 'GET',
    );
  }

  function page(
    items: BatchResponse[],
    pageNumber: number,
    totalPages: number,
    totalCount = items.length,
  ): PagedResponse<BatchResponse> {
    return { items, pageNumber, pageSize: 20, totalCount, totalPages };
  }

  function batch(overrides: Partial<BatchResponse> = {}): BatchResponse {
    return {
      batchId: '11111111-1111-1111-1111-111111111111',
      batchLabel: 'August Batch 1',
      paymentRecordCount: 120,
      bankRecordCount: 118,
      settlementRecordCount: 115,
      totalRecordCount: 353,
      validationStatus: 'Valid',
      createdBy: 'ops-analyst@finsight.test',
      createdAt: '2026-08-27T09:15:00Z',
      ...overrides,
    };
  }

  it('requests page 1 with pageSize 20 immediately on load', () => {
    const req = expectInitialRequest();

    expect(req.request.params.get('pageNumber')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('20');

    req.flush(page([], 1, 0, 0));
  });

  it('shows a loading skeleton before the request resolves', () => {
    expect(el().querySelector('[data-testid="batches-skeleton"]')).toBeTruthy();

    expectInitialRequest().flush(page([], 1, 0, 0));
  });

  it('renders populated rows with exact field values, no recomputation', () => {
    const b = batch();
    expectInitialRequest().flush(page([b], 1, 1, 1));
    fixture.detectChanges();

    const rows = el().querySelectorAll('[data-testid="batch-row"]');
    expect(rows.length).toBe(1);

    const text = rows[0].textContent!;
    expect(text).toContain('August Batch 1');
    expect(text).toContain('120');
    expect(text).toContain('118');
    expect(text).toContain('115');
    expect(text).toContain('353');
    expect(text).toContain('ops-analyst@finsight.test');
  });

  it('renders the empty state only on a genuine 200 with zero items', () => {
    expectInitialRequest().flush(page([], 1, 0, 0));
    fixture.detectChanges();

    const empty = el().querySelector('[data-testid="batches-empty"]');
    expect(empty).toBeTruthy();
    expect(empty!.textContent).toContain('No batches found');
    expect(el().querySelector('[data-testid="batches-error"]')).toBeFalsy();
    expect(el().querySelector('[data-testid="batches-table"]')).toBeFalsy();
  });

  it('fabricates no digits in the empty state', () => {
    expectInitialRequest().flush(page([], 1, 0, 0));
    fixture.detectChanges();

    const empty = el().querySelector('[data-testid="batches-empty"]')!;
    expect(/\d/.test(empty.textContent!)).toBeFalse();
  });

  it('renders an error state with Retry on a non-2xx response', () => {
    expectInitialRequest().flush(
      { title: 'Internal Server Error', status: 500 },
      { status: 500, statusText: 'Internal Server Error' },
    );
    fixture.detectChanges();

    const error = el().querySelector('[data-testid="batches-error"]');
    expect(error).toBeTruthy();
    expect(error!.querySelector('button')).toBeTruthy();
  });

  it('retry re-issues an identical request', () => {
    expectInitialRequest().flush(
      { title: 'Internal Server Error', status: 500 },
      { status: 500, statusText: 'Internal Server Error' },
    );
    fixture.detectChanges();

    const retryButton = el().querySelector<HTMLButtonElement>(
      '[data-testid="batches-error"] button',
    )!;
    retryButton.click();

    const req = httpMock.expectOne((r) => r.url === batchesUrl);
    expect(req.request.params.get('pageNumber')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('20');

    req.flush(page([batch()], 1, 1, 1));
  });

  it('does not render bespoke session-expired copy on a 401 — that is the global interceptor’s job', () => {
    expectInitialRequest().flush(
      { title: 'Unauthorized', status: 401 },
      { status: 401, statusText: 'Unauthorized' },
    );
    fixture.detectChanges();

    const text = el().textContent!.toLowerCase();
    expect(text).not.toContain('session');
    expect(text).not.toContain('expired');
  });

  it('requests the next page and disables Next on the last page', () => {
    expectInitialRequest().flush(page([batch()], 1, 2, 21));
    fixture.detectChanges();

    const nextButton = el().querySelectorAll<HTMLButtonElement>(
      '[data-testid="batches-pagination"] button',
    )[1];
    expect(nextButton.disabled).toBeFalse();
    nextButton.click();

    const req = httpMock.expectOne((r) => r.url === batchesUrl);
    expect(req.request.params.get('pageNumber')).toBe('2');
    req.flush(page([batch({ batchId: '2', batchLabel: 'August Batch 2' })], 2, 2, 21));
    fixture.detectChanges();

    const nextButtonAfter = el().querySelectorAll<HTMLButtonElement>(
      '[data-testid="batches-pagination"] button',
    )[1];
    expect(nextButtonAfter.disabled).toBeTrue();
  });

  it('disables Previous on the first page', () => {
    expectInitialRequest().flush(page([batch()], 1, 2, 21));
    fixture.detectChanges();

    const previousButton = el().querySelectorAll<HTMLButtonElement>(
      '[data-testid="batches-pagination"] button',
    )[0];
    expect(previousButton.disabled).toBeTrue();
  });

  it('hides pagination entirely when there is only one page', () => {
    expectInitialRequest().flush(page([batch()], 1, 1, 1));
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="batches-pagination"]')).toBeFalsy();
  });

  it('renders both Valid and Invalid status text distinctly', () => {
    expectInitialRequest().flush(
      page(
        [
          batch({ batchId: 'a', validationStatus: 'Valid' }),
          batch({ batchId: 'b', validationStatus: 'Invalid' }),
        ],
        1,
        1,
        2,
      ),
    );
    fixture.detectChanges();

    const badges = el().querySelectorAll('[data-testid="batch-status"]');
    expect(badges.length).toBe(2);
    expect(badges[0].textContent).toContain('Valid');
    expect(badges[1].textContent).toContain('Invalid');
    // Distinct styling, not merely distinct text -- colour is never the only signal,
    // but it must still be present alongside the text.
    expect(badges[0].className).not.toBe(badges[1].className);
  });

  it('links to the real upload route', () => {
    const link = el().querySelector<HTMLAnchorElement>('a[href="/batches/upload"]');
    expect(link).toBeTruthy();
    expect(link!.textContent).toContain('Upload batch');

    expectInitialRequest().flush(page([], 1, 0, 0));
  });

  it('shows "Run reconciliation" only for Valid batches', () => {
    expectInitialRequest().flush(
      page(
        [
          batch({ batchId: 'a', validationStatus: 'Valid' }),
          batch({ batchId: 'b', validationStatus: 'Invalid' }),
        ],
        1,
        1,
        2,
      ),
    );
    fixture.detectChanges();

    const rows = el().querySelectorAll('[data-testid="batch-row"]');
    expect(rows[0].querySelector('[data-testid="run-reconciliation-button"]')).toBeTruthy();
    expect(rows[1].querySelector('[data-testid="run-reconciliation-button"]')).toBeFalsy();
  });

  it('shows a submitting state and disables all row actions while a run is being created', () => {
    expectInitialRequest().flush(
      page(
        [batch({ batchId: 'a' }), batch({ batchId: 'b' })],
        1,
        1,
        2,
      ),
    );
    fixture.detectChanges();

    const buttons = el().querySelectorAll<HTMLButtonElement>(
      '[data-testid="run-reconciliation-button"]',
    );
    buttons[0].click();
    fixture.detectChanges();

    expect(buttons[0].textContent).toContain('Running…');
    expect(buttons[0].disabled).toBeTrue();
    expect(buttons[1].disabled).toBeTrue();

    httpMock.expectOne((r) => r.url === runsUrl).flush({
      runId: 'r1',
      batchId: 'a',
      status: 2,
      totalReconciliationUnits: 3,
      matchedCount: 3,
      mismatchedCount: 0,
      missingCount: 0,
      duplicateCount: 0,
      unresolvedCount: 0,
      matchRate: 100,
    });
  });

  it('sends exactly { batchId } and navigates to the real returned runId on success', () => {
    expectInitialRequest().flush(page([batch({ batchId: 'a' })], 1, 1, 1));
    fixture.detectChanges();

    el().querySelector<HTMLButtonElement>('[data-testid="run-reconciliation-button"]')!.click();

    const req = httpMock.expectOne((r) => r.url === runsUrl && r.method === 'POST');
    expect(req.request.body).toEqual({ batchId: 'a' });

    req.flush({
      runId: 'real-run-id-123',
      batchId: 'a',
      status: 2,
      totalReconciliationUnits: 3,
      matchedCount: 3,
      mismatchedCount: 0,
      missingCount: 0,
      duplicateCount: 0,
      unresolvedCount: 0,
      matchRate: 100,
    });

    expect(router.navigateByUrl).toHaveBeenCalledWith('/runs/real-run-id-123');
  });

  it('shows an inline banner and preserves the table when run creation fails', () => {
    expectInitialRequest().flush(page([batch({ batchId: 'a' })], 1, 1, 1));
    fixture.detectChanges();

    el().querySelector<HTMLButtonElement>('[data-testid="run-reconciliation-button"]')!.click();

    httpMock.expectOne((r) => r.url === runsUrl).flush(
      { title: 'Resource Not Found', status: 404, detail: "Batch 'a' was not found." },
      { status: 404, statusText: 'Not Found' },
    );
    fixture.detectChanges();

    const banner = el().querySelector('[data-testid="run-creation-error"]');
    expect(banner).toBeTruthy();
    expect(banner!.textContent).toContain("Batch 'a' was not found.");
    expect(router.navigateByUrl).not.toHaveBeenCalled();
    // The table itself is untouched by the failure.
    expect(el().querySelectorAll('[data-testid="batch-row"]').length).toBe(1);

    el().querySelector<HTMLButtonElement>('[data-testid="run-creation-error"] button')!.click();
    fixture.detectChanges();
    expect(el().querySelector('[data-testid="run-creation-error"]')).toBeFalsy();
  });

  it('does not render bespoke session-expired copy on a run-creation 401', () => {
    expectInitialRequest().flush(page([batch({ batchId: 'a' })], 1, 1, 1));
    fixture.detectChanges();

    el().querySelector<HTMLButtonElement>('[data-testid="run-reconciliation-button"]')!.click();

    httpMock.expectOne((r) => r.url === runsUrl).flush(
      { title: 'Unauthorized', status: 401 },
      { status: 401, statusText: 'Unauthorized' },
    );
    fixture.detectChanges();

    const text = el().textContent!.toLowerCase();
    expect(text).not.toContain('session');
    expect(text).not.toContain('expired');
  });

  it('uses a fixed table layout so column widths stay stable regardless of content', () => {
    expectInitialRequest().flush(page([batch()], 1, 1, 1));
    fixture.detectChanges();

    const table = el().querySelector('[data-testid="batches-table"]')!;
    expect(table.className).toContain('table-fixed');
  });

  it('isolates table scrolling to its own viewport, separate from the page root', () => {
    expectInitialRequest().flush(page([batch()], 1, 1, 1));
    fixture.detectChanges();

    const table = el().querySelector('[data-testid="batches-table"]')!;
    const scrollViewport = table.parentElement!;
    expect(scrollViewport.className).toContain('overflow-auto');

    // The page header must sit outside the scrollable viewport, not
    // inside it -- otherwise scrolling the table would also scroll the
    // heading away.
    const heading = el().querySelector('#batch-history-heading')!;
    expect(scrollViewport.contains(heading)).toBeFalse();
  });

  it('wraps a long batch name within its own cell without forcing single-word breaks', () => {
    expectInitialRequest().flush(
      page([batch({ batchLabel: 'Phase 9 - 100 Unit Cold Warm Benchmark' })], 1, 1, 1),
    );
    fixture.detectChanges();

    const label = el().querySelector('[data-testid="batch-row"]')!.querySelector('.break-words')!;
    expect(label.textContent).toContain('Phase 9 - 100 Unit Cold Warm Benchmark');
    expect(label.className).not.toContain('break-all');
    expect(label.className).not.toContain('truncate');
  });

  it('keeps the pagination control outside the scrollable table viewport', () => {
    expectInitialRequest().flush(page([batch()], 1, 2, 21));
    fixture.detectChanges();

    const table = el().querySelector('[data-testid="batches-table"]')!;
    const scrollViewport = table.parentElement!;
    const pagination = el().querySelector('[data-testid="batches-pagination"]')!;

    expect(scrollViewport.contains(pagination)).toBeFalse();
  });

  it('truncates a long createdBy value with the full value preserved as a title', () => {
    const longEmail = 'a-very-long-operator-address-for-testing@finsight-enterprise.example.com';
    expectInitialRequest().flush(page([batch({ createdBy: longEmail })], 1, 1, 1));
    fixture.detectChanges();

    const createdByLine = Array.from(
      el().querySelectorAll<HTMLElement>('[data-testid="batch-row"] td div'),
    ).find((div) => div.textContent?.includes(longEmail))!;

    expect(createdByLine.className).toContain('truncate');
    expect(createdByLine.getAttribute('title')).toBe(`by ${longEmail}`);
  });

  it('contains no challenge-track or internal roadmap language', () => {
    const text = el().textContent!.toLowerCase();

    expect(text).not.toContain('track 04');
    expect(text).not.toContain('buildathon');
    expect(text).not.toContain('phase');
    expect(text).not.toContain('sprint');
    expect(text).not.toContain('roadmap');

    expectInitialRequest().flush(page([], 1, 0, 0));
  });
});
