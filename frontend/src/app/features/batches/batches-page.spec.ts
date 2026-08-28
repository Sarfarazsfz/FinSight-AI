import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { BatchesPage } from './batches-page';
import { environment } from '../../../environments/environment';
import type { BatchResponse } from '../../core/models/batch.model';
import type { PagedResponse } from '../../core/models/paged-response.model';

describe('BatchesPage', () => {
  let fixture: ComponentFixture<BatchesPage>;
  let httpMock: HttpTestingController;

  const batchesUrl = `${environment.apiBaseUrl}/batches`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [BatchesPage],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    httpMock = TestBed.inject(HttpTestingController);
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
