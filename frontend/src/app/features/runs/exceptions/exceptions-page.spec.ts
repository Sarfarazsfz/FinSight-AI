import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { ExceptionsPage } from './exceptions-page';
import { environment } from '../../../../environments/environment';
import type {
  ReconciliationExceptionCategory,
  ReconciliationExceptionResponse,
} from '../../../core/models/reconciliation.model';
import type { PagedResponse } from '../../../core/models/paged-response.model';

describe('ExceptionsPage', () => {
  let fixture: ComponentFixture<ExceptionsPage>;
  let httpMock: HttpTestingController;

  const runId = '22222222-2222-2222-2222-222222222222';
  const exceptionsUrl = `${environment.apiBaseUrl}/reconciliation/runs/${runId}/exceptions`;

  function configure(): void {
    TestBed.configureTestingModule({
      imports: [ExceptionsPage],
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
    fixture = TestBed.createComponent(ExceptionsPage);
    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function expectInitialRequest() {
    return httpMock.expectOne((r) => r.url === exceptionsUrl && r.method === 'GET');
  }

  function item(overrides: Partial<ReconciliationExceptionResponse> = {}): ReconciliationExceptionResponse {
    return {
      exceptionId: 'e1',
      runId,
      reconciliationResultId: 'r1',
      transactionReference: 'TXN-0001',
      category: 'AmountMismatch',
      involvedSources: 'Payment,Bank',
      discrepancyDetail: '{"transaction_reference":"TXN-0001"}',
      aiExplanation: null,
      aiSuggestedCategory: null,
      aiExplanationGeneratedAt: null,
      createdAt: '2026-08-29T09:15:00Z',
      updatedAt: null,
      ...overrides,
    };
  }

  function page(
    items: ReconciliationExceptionResponse[],
    pageNumber: number,
    totalPages: number,
    totalCount = items.length,
  ): PagedResponse<ReconciliationExceptionResponse> {
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
    expect(el().querySelector('[data-testid="exceptions-skeleton"]')).toBeTruthy();

    expectInitialRequest().flush(page([], 1, 0, 0));
  });

  it('renders populated rows with exact field values, no recomputation', () => {
    configure();
    expectInitialRequest().flush(page([item()], 1, 1, 1));
    fixture.detectChanges();

    const rows = el().querySelectorAll('[data-testid="exception-row"]');
    expect(rows.length).toBe(1);

    const text = rows[0].textContent!;
    expect(text).toContain('TXN-0001');
    expect(text).toContain('AmountMismatch');
    expect(text).toContain('Payment, Bank');
  });

  it('renders the SUCCESS empty state (not styled as an error) on a genuine zero-item 200', () => {
    configure();
    expectInitialRequest().flush(page([], 1, 0, 0));
    fixture.detectChanges();

    const empty = el().querySelector('[data-testid="exceptions-empty"]');
    expect(empty).toBeTruthy();
    expect(empty!.textContent).toContain('No exceptions — every unit in this run matched.');
    expect(el().querySelector('[data-testid="exceptions-error"]')).toBeFalsy();
    expect(empty!.getAttribute('role')).not.toBe('alert');
  });

  it('renders a not-found state on a real 404, with a link back to Batches', () => {
    configure();
    expectInitialRequest().flush(
      { title: 'Resource Not Found', status: 404, detail: `Reconciliation run '${runId}' was not found.` },
      { status: 404, statusText: 'Not Found' },
    );
    fixture.detectChanges();

    const notFound = el().querySelector('[data-testid="exceptions-not-found"]');
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

    const retryButton = el().querySelector<HTMLButtonElement>('[data-testid="exceptions-error"] button')!;
    retryButton.click();

    const req = httpMock.expectOne((r) => r.url === exceptionsUrl);
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
      el().querySelectorAll<HTMLButtonElement>('[data-testid="exceptions-pagination"] button');

    expect(buttons()[0].disabled).toBeTrue(); // First
    expect(buttons()[1].disabled).toBeTrue(); // Previous
    expect(buttons()[2].disabled).toBeFalse(); // Next
    expect(buttons()[3].disabled).toBeFalse(); // Last

    buttons()[3].click();
    let req = httpMock.expectOne((r) => r.url === exceptionsUrl);
    expect(req.request.params.get('pageNumber')).toBe('3');
    req.flush(page([item({ exceptionId: 'e3' })], 3, 3, 150));
    fixture.detectChanges();

    expect(buttons()[2].disabled).toBeTrue();
    expect(buttons()[3].disabled).toBeTrue();

    buttons()[0].click();
    req = httpMock.expectOne((r) => r.url === exceptionsUrl);
    expect(req.request.params.get('pageNumber')).toBe('1');
    req.flush(page([item()], 1, 3, 150));
  });

  it('hides pagination entirely when there is only one page', () => {
    configure();
    expectInitialRequest().flush(page([item()], 1, 1, 1));
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="exceptions-pagination"]')).toBeFalsy();
  });

  const categories: ReconciliationExceptionCategory[] = [
    'AmountMismatch',
    'DateMismatch',
    'MissingRecord',
    'DuplicateRecord',
    'Unresolved',
  ];

  it('renders all 5 real ExceptionCategory values, using their exact names', () => {
    configure();
    const items = categories.map((category, i) => item({ exceptionId: `e${i}`, category }));
    expectInitialRequest().flush(page(items, 1, 1, items.length));
    fixture.detectChanges();

    const badges = el().querySelectorAll('[data-testid="exception-category"]');
    expect(badges.length).toBe(5);
    categories.forEach((category, i) => {
      expect(badges[i].textContent).toContain(category);
    });
  });

  it('maps AmountMismatch and DateMismatch to the same reused "mismatched" token, others distinct', () => {
    configure();
    const items = categories.map((category, i) => item({ exceptionId: `e${i}`, category }));
    expectInitialRequest().flush(page(items, 1, 1, items.length));
    fixture.detectChanges();

    const badges = el().querySelectorAll('[data-testid="exception-category"]');
    expect(badges[0].className).toBe(badges[1].className); // AmountMismatch === DateMismatch
    expect(badges[0].className).not.toBe(badges[2].className); // vs MissingRecord
    expect(badges[2].className).not.toBe(badges[3].className); // vs DuplicateRecord
    expect(badges[3].className).not.toBe(badges[4].className); // vs Unresolved
  });

  it('links each row with the correct ?page query param matching the current page', () => {
    configure();
    expectInitialRequest().flush(page([item()], 1, 2, 100));
    fixture.detectChanges();

    el().querySelectorAll<HTMLButtonElement>('[data-testid="exceptions-pagination"] button')[2].click(); // Next
    httpMock.expectOne((r) => r.url === exceptionsUrl).flush(page([item({ exceptionId: 'abc' })], 2, 2, 100));
    fixture.detectChanges();

    const link = el().querySelector<HTMLAnchorElement>('[data-testid="exception-row"] a')!;
    expect(link.getAttribute('href')).toBe(`/runs/${runId}/exceptions/abc?page=2`);
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
