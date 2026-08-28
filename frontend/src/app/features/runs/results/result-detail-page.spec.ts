import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { ResultDetailPage } from './result-detail-page';
import { environment } from '../../../../environments/environment';
import type {
  ReconciliationTransactionDetailResponse,
  SourceTransactionRecordResponse,
} from '../../../core/models/reconciliation.model';

describe('ResultDetailPage', () => {
  let fixture: ComponentFixture<ResultDetailPage>;
  let httpMock: HttpTestingController;

  const runId = '22222222-2222-2222-2222-222222222222';
  const resultId = '33333333-3333-3333-3333-333333333333';
  const detailUrl = `${environment.apiBaseUrl}/reconciliation/runs/${runId}/results/${resultId}`;

  function configure(): void {
    TestBed.configureTestingModule({
      imports: [ResultDetailPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ runId, resultId }) } },
        },
      ],
    });

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ResultDetailPage);
    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function expectInitialRequest() {
    return httpMock.expectOne((r) => r.url === detailUrl && r.method === 'GET');
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

  function detail(
    overrides: Partial<ReconciliationTransactionDetailResponse> = {},
  ): ReconciliationTransactionDetailResponse {
    return {
      resultId,
      runId,
      normalizedTransactionId: 'nt1',
      transactionReference: 'TXN-0001',
      status: 'Matched',
      strategyUsed: 'StrategyOne_ExactReferenceMatch',
      reasonCode: 'EXACT_MATCH',
      payments: [sourceRecord()],
      banks: [sourceRecord({ id: 's2', sourceRecordIdentifier: 'BANK-000001' })],
      settlements: [sourceRecord({ id: 's3', sourceRecordIdentifier: 'SET-000001' })],
      ...overrides,
    };
  }

  it('requests the exact run/result URL', () => {
    configure();
    const req = expectInitialRequest();
    expect(req.request.method).toBe('GET');
    req.flush(detail());
  });

  it('shows a loading state before the request resolves', () => {
    configure();
    expect(el().querySelector('[data-testid="detail-loading"]')).toBeTruthy();

    expectInitialRequest().flush(detail());
  });

  it('renders exact field values with no recomputation', () => {
    configure();
    expectInitialRequest().flush(detail());
    fixture.detectChanges();

    expect(el().querySelector('h1')!.textContent).toContain('TXN-0001');
    expect(el().querySelector('[data-testid="detail-status"]')!.textContent).toContain('Matched');
    expect(el().textContent).toContain('EXACT_MATCH');
    expect(el().textContent).toContain('StrategyOne_ExactReferenceMatch');
    expect(el().textContent).toContain('PAY-000001');
    expect(el().textContent).toContain('120.5');
  });

  it('renders "—" for a null strategyUsed', () => {
    configure();
    expectInitialRequest().flush(detail({ strategyUsed: null, status: 'Missing', reasonCode: 'SOURCE_ABSENT_BANK' }));
    fixture.detectChanges();

    expect(el().textContent).toContain('—');
  });

  it('renders an honest empty section when a source array has zero items', () => {
    configure();
    expectInitialRequest().flush(detail({ banks: [] }));
    fixture.detectChanges();

    const bankSection = el().querySelector('[data-testid="evidence-section-bank"]')!;
    expect(bankSection.textContent).toContain('No matching bank record.');
    expect(bankSection.querySelectorAll('[data-testid="evidence-row-bank"]').length).toBe(0);
  });

  it('renders every item when a source array has multiple entries (a real Duplicate scenario)', () => {
    configure();
    expectInitialRequest().flush(
      detail({
        status: 'Duplicate',
        reasonCode: 'DUPLICATE_SETTLEMENT',
        strategyUsed: null,
        settlements: [
          sourceRecord({ id: 's3', sourceRecordIdentifier: 'SET-000001' }),
          sourceRecord({ id: 's4', sourceRecordIdentifier: 'SET-000002' }),
        ],
      }),
    );
    fixture.detectChanges();

    const rows = el().querySelectorAll('[data-testid="evidence-row-settlement"]');
    expect(rows.length).toBe(2);
    expect(el().textContent).toContain('SET-000001');
    expect(el().textContent).toContain('SET-000002');
  });

  it('renders a not-found state on a real 404, linking back to Results, not Batches', () => {
    configure();
    expectInitialRequest().flush(
      {
        title: 'Resource Not Found',
        status: 404,
        detail: `Reconciliation result '${resultId}' was not found for run '${runId}'.`,
      },
      { status: 404, statusText: 'Not Found' },
    );
    fixture.detectChanges();

    const notFound = el().querySelector('[data-testid="detail-not-found"]');
    expect(notFound).toBeTruthy();
    expect(notFound!.querySelector('button')).toBeFalsy();
    expect(el().querySelector(`a[href="/runs/${runId}/results"]`)).toBeTruthy();
  });

  it('renders a generic error state with Retry, which re-issues the identical request', () => {
    configure();
    expectInitialRequest().flush(
      { title: 'Internal Server Error', status: 500 },
      { status: 500, statusText: 'Internal Server Error' },
    );
    fixture.detectChanges();

    el().querySelector<HTMLButtonElement>('[data-testid="detail-error"] button')!.click();

    const req = httpMock.expectOne((r) => r.url === detailUrl);
    expect(req.request.method).toBe('GET');
    req.flush(detail());
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
});
