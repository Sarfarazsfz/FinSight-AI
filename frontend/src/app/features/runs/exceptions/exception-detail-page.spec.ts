import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import type { ParamMap } from '@angular/router';
import { ReplaySubject } from 'rxjs';
import { ExceptionDetailPage } from './exception-detail-page';
import { environment } from '../../../../environments/environment';
import type {
  ReconciliationExceptionResponse,
  ReconciliationTransactionDetailResponse,
  SourceTransactionRecordResponse,
} from '../../../core/models/reconciliation.model';
import type { PagedResponse } from '../../../core/models/paged-response.model';

describe('ExceptionDetailPage', () => {
  let fixture: ComponentFixture<ExceptionDetailPage>;
  let httpMock: HttpTestingController;
  let router: Router;
  let paramMapSubject: ReplaySubject<ParamMap>;
  let currentQuerySnapshot: { queryParamMap: ParamMap };

  const runId = '22222222-2222-2222-2222-222222222222';
  const reconciliationBase = `${environment.apiBaseUrl}/reconciliation`;
  const exceptionsUrl = `${reconciliationBase}/runs/${runId}/exceptions`;

  function exceptionUrl(id: string): string {
    return `${reconciliationBase}/exceptions/${id}`;
  }

  function resultDetailUrl(resultId: string): string {
    return `${reconciliationBase}/runs/${runId}/results/${resultId}`;
  }

  function emitParams(exceptionId: string, page?: number): void {
    currentQuerySnapshot.queryParamMap = convertToParamMap(
      page !== undefined ? { page: String(page) } : {},
    );
    paramMapSubject.next(convertToParamMap({ runId, exceptionId }));
  }

  function configure(initialExceptionId: string, initialPage?: number): void {
    paramMapSubject = new ReplaySubject<ParamMap>(1);
    currentQuerySnapshot = { queryParamMap: convertToParamMap({}) };

    TestBed.configureTestingModule({
      imports: [ExceptionDetailPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: paramMapSubject.asObservable(),
            get snapshot() {
              return currentQuerySnapshot;
            },
          },
        },
      ],
    });

    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    spyOn(router, 'navigate');

    fixture = TestBed.createComponent(ExceptionDetailPage);
    fixture.detectChanges();
    emitParams(initialExceptionId, initialPage);
    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function exceptionItem(
    overrides: Partial<ReconciliationExceptionResponse> = {},
  ): ReconciliationExceptionResponse {
    return {
      exceptionId: 'e1',
      runId,
      reconciliationResultId: 'r1',
      transactionReference: 'TXN-0001',
      category: 'AmountMismatch',
      involvedSources: 'Payment,Bank',
      discrepancyDetail: JSON.stringify({ transaction_reference: 'TXN-0001', payment: [] }),
      aiExplanation: null,
      aiSuggestedCategory: null,
      aiExplanationGeneratedAt: null,
      createdAt: '2026-08-29T09:00:00Z',
      updatedAt: null,
      ...overrides,
    };
  }

  function sourceRecord(
    overrides: Partial<SourceTransactionRecordResponse> = {},
  ): SourceTransactionRecordResponse {
    return {
      id: 's1',
      sourceRecordIdentifier: 'PAY-000001',
      transactionReference: 'TXN-0001',
      amount: 120.5,
      currency: 'INR',
      transactionDate: '2026-08-20',
      status: 'SUCCESS',
      createdAt: '2026-08-29T09:00:00Z',
      ...overrides,
    };
  }

  function evidenceDetail(
    overrides: Partial<ReconciliationTransactionDetailResponse> = {},
  ): ReconciliationTransactionDetailResponse {
    return {
      resultId: 'r1',
      runId,
      normalizedTransactionId: 'nt1',
      transactionReference: 'TXN-0001',
      status: 'Mismatched',
      strategyUsed: null,
      reasonCode: 'AMOUNT_MISMATCH',
      payments: [sourceRecord()],
      banks: [sourceRecord({ id: 's2', sourceRecordIdentifier: 'BANK-000001' })],
      settlements: [sourceRecord({ id: 's3', sourceRecordIdentifier: 'SET-000001' })],
      ...overrides,
    };
  }

  function queuePage(
    items: ReconciliationExceptionResponse[],
    pageNumber: number,
    totalPages: number,
    totalCount = items.length,
  ): PagedResponse<ReconciliationExceptionResponse> {
    return { items, pageNumber, pageSize: 50, totalCount, totalPages };
  }

  it('requests the exception, its evidence, and its queue page on a cold load with ?page from the URL', () => {
    configure('e1', 2);

    const exceptionReq = httpMock.expectOne((r) => r.url === exceptionUrl('e1'));
    exceptionReq.flush(exceptionItem());

    const evidenceReq = httpMock.expectOne((r) => r.url === resultDetailUrl('r1'));
    evidenceReq.flush(evidenceDetail());

    const queueReq = httpMock.expectOne((r) => r.url === exceptionsUrl);
    expect(queueReq.request.params.get('pageNumber')).toBe('2');
    queueReq.flush(queuePage([exceptionItem()], 2, 3, 150));
  });

  it('shows a loading state before the exception resolves', () => {
    configure('e1', 1);
    expect(el().querySelector('[data-testid="detail-loading"]')).toBeTruthy();

    httpMock.expectOne((r) => r.url === exceptionUrl('e1')).flush(exceptionItem());
    httpMock.expectOne((r) => r.url === resultDetailUrl('r1')).flush(evidenceDetail());
    httpMock.expectOne((r) => r.url === exceptionsUrl).flush(queuePage([exceptionItem()], 1, 1, 1));
  });

  it('renders exact field values with no recomputation', () => {
    configure('e1', 1);
    httpMock.expectOne((r) => r.url === exceptionUrl('e1')).flush(exceptionItem());
    httpMock.expectOne((r) => r.url === resultDetailUrl('r1')).flush(evidenceDetail());
    httpMock.expectOne((r) => r.url === exceptionsUrl).flush(queuePage([exceptionItem()], 1, 1, 1));
    fixture.detectChanges();

    expect(el().querySelector('h1')!.textContent).toContain('TXN-0001');
    expect(el().querySelector('[data-testid="exception-category"]')!.textContent).toContain('AmountMismatch');
    expect(el().textContent).toContain('Payment, Bank');
    expect(el().textContent).toContain('PAY-000001');
  });

  it('renders discrepancyDetail as pretty-printed JSON', () => {
    configure('e1', 1);
    httpMock.expectOne((r) => r.url === exceptionUrl('e1')).flush(
      exceptionItem({ discrepancyDetail: '{"transaction_reference":"TXN-0001","amount":100}' }),
    );
    httpMock.expectOne((r) => r.url === resultDetailUrl('r1')).flush(evidenceDetail());
    httpMock.expectOne((r) => r.url === exceptionsUrl).flush(queuePage([exceptionItem()], 1, 1, 1));
    fixture.detectChanges();

    const pre = el().querySelector('[data-testid="discrepancy-detail"]')!;
    expect(pre.textContent).toContain('"transaction_reference": "TXN-0001"');
    expect(pre.textContent).toContain('"amount": 100');
  });

  it('falls back to the raw string when discrepancyDetail is not valid JSON, without crashing', () => {
    configure('e1', 1);
    httpMock.expectOne((r) => r.url === exceptionUrl('e1')).flush(
      exceptionItem({ discrepancyDetail: 'not actually json {{{' }),
    );
    httpMock.expectOne((r) => r.url === resultDetailUrl('r1')).flush(evidenceDetail());
    httpMock.expectOne((r) => r.url === exceptionsUrl).flush(queuePage([exceptionItem()], 1, 1, 1));
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="discrepancy-detail"]')!.textContent).toContain(
      'not actually json {{{',
    );
  });

  it('renders an honest empty evidence section and a real multi-row section', () => {
    configure('e1', 1);
    httpMock.expectOne((r) => r.url === exceptionUrl('e1')).flush(exceptionItem());
    httpMock.expectOne((r) => r.url === resultDetailUrl('r1')).flush(
      evidenceDetail({
        payments: [],
        settlements: [
          sourceRecord({ id: 's3', sourceRecordIdentifier: 'SET-000001' }),
          sourceRecord({ id: 's4', sourceRecordIdentifier: 'SET-000002' }),
        ],
      }),
    );
    httpMock.expectOne((r) => r.url === exceptionsUrl).flush(queuePage([exceptionItem()], 1, 1, 1));
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="evidence-section-payment"]')!.textContent).toContain(
      'No matching payment record.',
    );
    expect(el().querySelectorAll('[data-testid="evidence-row-settlement"]').length).toBe(2);
  });

  it('renders no AI UI at all', () => {
    configure('e1', 1);
    httpMock.expectOne((r) => r.url === exceptionUrl('e1')).flush(
      exceptionItem({ aiExplanation: 'some explanation', aiSuggestedCategory: 'AmountMismatch' }),
    );
    httpMock.expectOne((r) => r.url === resultDetailUrl('r1')).flush(evidenceDetail());
    httpMock.expectOne((r) => r.url === exceptionsUrl).flush(queuePage([exceptionItem()], 1, 1, 1));
    fixture.detectChanges();

    expect(el().textContent).not.toContain('some explanation');
    expect(el().textContent!.toLowerCase()).not.toContain('ai explanation');
    expect(el().textContent!.toLowerCase()).not.toContain('coming soon');
  });

  it('moves to the next sibling within the same held page without an additional queue fetch', () => {
    configure('e1', 1);
    httpMock.expectOne((r) => r.url === exceptionUrl('e1')).flush(exceptionItem({ exceptionId: 'e1' }));
    httpMock.expectOne((r) => r.url === resultDetailUrl('r1')).flush(evidenceDetail());
    httpMock
      .expectOne((r) => r.url === exceptionsUrl)
      .flush(
        queuePage(
          [exceptionItem({ exceptionId: 'e1' }), exceptionItem({ exceptionId: 'e2', reconciliationResultId: 'r2' })],
          1,
          1,
          2,
        ),
      );
    fixture.detectChanges();

    el().querySelector<HTMLButtonElement>('[aria-label="Next exception"]')!.click();

    expect(router.navigate).toHaveBeenCalledWith(
      ['/runs', runId, 'exceptions', 'e2'],
      { queryParams: { page: 1 } },
    );

    // Simulate the app actually completing that in-place navigation.
    emitParams('e2', 1);
    fixture.detectChanges();

    httpMock.expectOne((r) => r.url === exceptionUrl('e2')).flush(exceptionItem({ exceptionId: 'e2', reconciliationResultId: 'r2' }));
    httpMock.expectOne((r) => r.url === resultDetailUrl('r2')).flush(evidenceDetail({ resultId: 'r2' }));

    // No third request to the exceptions list URL should appear -- the
    // held page already contains e2.
    const stray = httpMock.match((r) => r.url === exceptionsUrl);
    expect(stray.length).toBe(0);
  });

  it('crosses a page boundary with exactly one adjacent-page fetch, then does not re-fetch on arrival', () => {
    configure('e1', 1);
    httpMock.expectOne((r) => r.url === exceptionUrl('e1')).flush(exceptionItem({ exceptionId: 'e1' }));
    httpMock.expectOne((r) => r.url === resultDetailUrl('r1')).flush(evidenceDetail());
    // Only one item on page 1 of 2 -- e1 is both first and last on this page.
    httpMock.expectOne((r) => r.url === exceptionsUrl).flush(queuePage([exceptionItem({ exceptionId: 'e1' })], 1, 2, 2));
    fixture.detectChanges();

    el().querySelector<HTMLButtonElement>('[aria-label="Next exception"]')!.click();

    const boundaryReq = httpMock.expectOne((r) => r.url === exceptionsUrl);
    expect(boundaryReq.request.params.get('pageNumber')).toBe('2');
    boundaryReq.flush(queuePage([exceptionItem({ exceptionId: 'e2', reconciliationResultId: 'r2' })], 2, 2, 2));

    expect(router.navigate).toHaveBeenCalledWith(
      ['/runs', runId, 'exceptions', 'e2'],
      { queryParams: { page: 2 } },
    );

    emitParams('e2', 2);
    fixture.detectChanges();

    httpMock.expectOne((r) => r.url === exceptionUrl('e2')).flush(exceptionItem({ exceptionId: 'e2', reconciliationResultId: 'r2' }));
    httpMock.expectOne((r) => r.url === resultDetailUrl('r2')).flush(evidenceDetail({ resultId: 'r2' }));

    const stray = httpMock.match((r) => r.url === exceptionsUrl);
    expect(stray.length).toBe(0);
  });

  it('disables Previous on the true first exception and Next on the true last', () => {
    configure('e1', 1);
    httpMock.expectOne((r) => r.url === exceptionUrl('e1')).flush(exceptionItem());
    httpMock.expectOne((r) => r.url === resultDetailUrl('r1')).flush(evidenceDetail());
    httpMock.expectOne((r) => r.url === exceptionsUrl).flush(queuePage([exceptionItem()], 1, 1, 1));
    fixture.detectChanges();

    expect(el().querySelector<HTMLButtonElement>('[aria-label="Previous exception"]')!.disabled).toBeTrue();
    expect(el().querySelector<HTMLButtonElement>('[aria-label="Next exception"]')!.disabled).toBeTrue();
  });

  it('renders a not-found state on a real 404, linking back to the exceptions queue', () => {
    configure('bad-id', 1);
    httpMock.expectOne((r) => r.url === exceptionUrl('bad-id')).flush(
      { title: 'Resource Not Found', status: 404, detail: "Reconciliation exception 'bad-id' was not found." },
      { status: 404, statusText: 'Not Found' },
    );
    fixture.detectChanges();

    const notFound = el().querySelector('[data-testid="detail-not-found"]');
    expect(notFound).toBeTruthy();
    expect(notFound!.querySelector('button')).toBeFalsy();
    expect(el().querySelector(`a[href="/runs/${runId}/exceptions"]`)).toBeTruthy();
  });

  it('renders a generic error state with Retry, which re-issues the identical request', () => {
    configure('e1', 1);
    httpMock.expectOne((r) => r.url === exceptionUrl('e1')).flush(
      { title: 'Internal Server Error', status: 500 },
      { status: 500, statusText: 'Internal Server Error' },
    );
    fixture.detectChanges();

    el().querySelector<HTMLButtonElement>('[data-testid="detail-error"] button')!.click();

    const req = httpMock.expectOne((r) => r.url === exceptionUrl('e1'));
    expect(req.request.method).toBe('GET');
    req.flush(exceptionItem());
    httpMock.expectOne((r) => r.url === resultDetailUrl('r1')).flush(evidenceDetail());
    httpMock.expectOne((r) => r.url === exceptionsUrl).flush(queuePage([exceptionItem()], 1, 1, 1));
  });

  it('does not render bespoke session-expired copy on a 401', () => {
    configure('e1', 1);
    httpMock.expectOne((r) => r.url === exceptionUrl('e1')).flush(
      { title: 'Unauthorized', status: 401 },
      { status: 401, statusText: 'Unauthorized' },
    );
    fixture.detectChanges();

    const text = el().textContent!.toLowerCase();
    expect(text).not.toContain('session');
    expect(text).not.toContain('expired');
  });
});
