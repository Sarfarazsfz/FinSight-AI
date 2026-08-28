import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { ResultsPage } from './results-page';
import { environment } from '../../../../environments/environment';
import type {
  ReconciliationMatchStatus,
  ReconciliationResultResponse,
} from '../../../core/models/reconciliation.model';
import type { PagedResponse } from '../../../core/models/paged-response.model';

describe('ResultsPage', () => {
  let fixture: ComponentFixture<ResultsPage>;
  let httpMock: HttpTestingController;

  const runId = '22222222-2222-2222-2222-222222222222';
  const resultsUrl = `${environment.apiBaseUrl}/reconciliation/runs/${runId}/results`;

  function configure(): void {
    TestBed.configureTestingModule({
      imports: [ResultsPage],
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
    fixture = TestBed.createComponent(ResultsPage);
    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function expectInitialRequest() {
    return httpMock.expectOne((r) => r.url === resultsUrl && r.method === 'GET');
  }

  function item(overrides: Partial<ReconciliationResultResponse> = {}): ReconciliationResultResponse {
    return {
      resultId: 'r1',
      runId,
      normalizedTransactionId: 'nt1',
      transactionReference: 'TXN-0001',
      status: 'Matched',
      strategyUsed: 'StrategyOne_ExactReferenceMatch',
      reasonCode: 'EXACT_MATCH',
      createdAt: '2026-08-29T09:15:00Z',
      ...overrides,
    };
  }

  function page(
    items: ReconciliationResultResponse[],
    pageNumber: number,
    totalPages: number,
    totalCount = items.length,
  ): PagedResponse<ReconciliationResultResponse> {
    return { items, pageNumber, pageSize: 50, totalCount, totalPages };
  }

  it('requests page 1 with pageSize 50, and only those two query parameters', () => {
    configure();
    const req = expectInitialRequest();

    expect(req.request.params.get('pageNumber')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('50');
    expect(req.request.params.keys().sort()).toEqual(['pageNumber', 'pageSize']);

    req.flush(page([], 1, 0, 0));
  });

  it('shows a loading skeleton before the request resolves', () => {
    configure();
    expect(el().querySelector('[data-testid="results-skeleton"]')).toBeTruthy();

    expectInitialRequest().flush(page([], 1, 0, 0));
  });

  it('renders populated rows with exact field values, no recomputation', () => {
    configure();
    const i = item();
    expectInitialRequest().flush(page([i], 1, 1, 1));
    fixture.detectChanges();

    const rows = el().querySelectorAll('[data-testid="result-row"]');
    expect(rows.length).toBe(1);

    const text = rows[0].textContent!;
    expect(text).toContain('TXN-0001');
    expect(text).toContain('Matched');
    expect(text).toContain('EXACT_MATCH');
    expect(text).toContain('StrategyOne_ExactReferenceMatch');
  });

  it('renders "—" for a null strategyUsed, never a fabricated value', () => {
    configure();
    expectInitialRequest().flush(
      page([item({ status: 'Missing', strategyUsed: null, reasonCode: 'SOURCE_ABSENT_BANK' })], 1, 1, 1),
    );
    fixture.detectChanges();

    const row = el().querySelector('[data-testid="result-row"]')!;
    expect(row.textContent).toContain('—');
    expect(row.textContent).not.toContain('null');
  });

  it('renders the empty state only on a genuine 200 with zero items', () => {
    configure();
    expectInitialRequest().flush(page([], 1, 0, 0));
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="results-empty"]')).toBeTruthy();
    expect(el().querySelector('[data-testid="results-table"]')).toBeFalsy();
  });

  it('renders a not-found state on a real 404, with a link back to Batches', () => {
    configure();
    expectInitialRequest().flush(
      { title: 'Resource Not Found', status: 404, detail: `Reconciliation run '${runId}' was not found.` },
      { status: 404, statusText: 'Not Found' },
    );
    fixture.detectChanges();

    const notFound = el().querySelector('[data-testid="results-not-found"]');
    expect(notFound).toBeTruthy();
    expect(notFound!.querySelector('button')).toBeFalsy();
    expect(el().querySelector('a[href="/batches"]')).toBeTruthy();
  });

  it('renders a generic error state with Retry on a non-404 failure, which re-issues the identical request', () => {
    configure();
    expectInitialRequest().flush(
      { title: 'Internal Server Error', status: 500 },
      { status: 500, statusText: 'Internal Server Error' },
    );
    fixture.detectChanges();

    const retryButton = el().querySelector<HTMLButtonElement>('[data-testid="results-error"] button')!;
    retryButton.click();

    const req = httpMock.expectOne((r) => r.url === resultsUrl);
    expect(req.request.params.get('pageNumber')).toBe('1');
    req.flush(page([item()], 1, 1, 1));
  });

  it('does not render bespoke session-expired copy on a 401', () => {
    configure();
    expectInitialRequest().flush(
      { title: 'Unauthorized', status: 401 },
      { status: 401, statusText: 'Unauthorized' },
    );
    fixture.detectChanges();

    const text = el().textContent!.toLowerCase();
    expect(text).not.toContain('session');
    expect(text).not.toContain('expired');
  });

  it('First/Previous/Next/Last request the correct pages and disable at boundaries', () => {
    configure();
    expectInitialRequest().flush(page([item()], 1, 3, 150));
    fixture.detectChanges();

    const buttons = () =>
      el().querySelectorAll<HTMLButtonElement>('[data-testid="results-pagination"] button');

    // Order: First, Previous, Next, Last
    expect(buttons()[0].disabled).toBeTrue();
    expect(buttons()[1].disabled).toBeTrue();
    expect(buttons()[2].disabled).toBeFalse();
    expect(buttons()[3].disabled).toBeFalse();

    buttons()[3].click(); // Last
    let req = httpMock.expectOne((r) => r.url === resultsUrl);
    expect(req.request.params.get('pageNumber')).toBe('3');
    req.flush(page([item({ resultId: 'r3' })], 3, 3, 150));
    fixture.detectChanges();

    expect(buttons()[2].disabled).toBeTrue(); // Next disabled on last page
    expect(buttons()[3].disabled).toBeTrue(); // Last disabled on last page

    buttons()[0].click(); // First
    req = httpMock.expectOne((r) => r.url === resultsUrl);
    expect(req.request.params.get('pageNumber')).toBe('1');
    req.flush(page([item()], 1, 3, 150));
  });

  it('hides pagination entirely when there is only one page', () => {
    configure();
    expectInitialRequest().flush(page([item()], 1, 1, 1));
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="results-pagination"]')).toBeFalsy();
  });

  const statuses: ReconciliationMatchStatus[] = ['Matched', 'Mismatched', 'Missing', 'Duplicate', 'Unresolved'];

  it('renders all 5 real MatchStatus values distinctly', () => {
    configure();
    const items = statuses.map((status, i) => item({ resultId: `r${i}`, status }));
    expectInitialRequest().flush(page(items, 1, 1, items.length));
    fixture.detectChanges();

    const badges = el().querySelectorAll('[data-testid="result-status"]');
    expect(badges.length).toBe(5);

    const classNames = new Set<string>();
    statuses.forEach((status, i) => {
      expect(badges[i].textContent).toContain(status);
      classNames.add(badges[i].className);
    });
    // Every status gets visually distinct treatment, not just distinct text.
    expect(classNames.size).toBe(5);
  });

  it('links each row to its own evidence page with an unambiguous accessible name', () => {
    configure();
    expectInitialRequest().flush(page([item({ resultId: 'abc', transactionReference: 'TXN-9999' })], 1, 1, 1));
    fixture.detectChanges();

    const link = el().querySelector<HTMLAnchorElement>('[data-testid="result-row"] a')!;
    expect(link.getAttribute('href')).toBe(`/runs/${runId}/results/abc`);
    expect(link.getAttribute('aria-label')).toBe('View evidence for TXN-9999');
  });

  it('links back to the run overview', () => {
    configure();
    expectInitialRequest().flush(page([], 1, 0, 0));
    fixture.detectChanges();

    const backLink = el().querySelector<HTMLAnchorElement>(`a[href="/runs/${runId}"]`);
    expect(backLink).toBeTruthy();
  });

  it('contains no challenge-track or internal roadmap language', () => {
    configure();
    const text = el().textContent!.toLowerCase();

    expect(text).not.toContain('track 04');
    expect(text).not.toContain('buildathon');
    expect(text).not.toContain('phase');
    expect(text).not.toContain('sprint');
    expect(text).not.toContain('roadmap');

    expectInitialRequest().flush(page([], 1, 0, 0));
  });
});
