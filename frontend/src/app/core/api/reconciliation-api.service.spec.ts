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
  AiExplanationResponse,
  FinanceAssistantResponse,
  ReconciliationExceptionResponse,
  ReconciliationResultResponse,
  ReconciliationRunDetailsResponse,
  ReconciliationRunResult,
  ReconciliationTransactionDetailResponse,
} from '../models/reconciliation.model';
import type { PagedResponse } from '../models/paged-response.model';

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

  const resultId = '33333333-3333-3333-3333-333333333333';

  describe('getResults', () => {
    const resultsUrl = `${runsUrl}/${runId}/results`;

    it('GETs the configured results URL', () => {
      api.getResults(runId, 1, 50).subscribe();

      const req = httpMock.expectOne((r) => r.url === resultsUrl && r.method === 'GET');
      expect(req.request.method).toBe('GET');
      req.flush(makeResultsPage([]));
    });

    it('sends pageNumber and pageSize as query parameters, nothing else', () => {
      api.getResults(runId, 2, 50).subscribe();

      const req = httpMock.expectOne((r) => r.url === resultsUrl);
      expect(req.request.params.get('pageNumber')).toBe('2');
      expect(req.request.params.get('pageSize')).toBe('50');
      expect(req.request.params.keys().sort()).toEqual(['pageNumber', 'pageSize']);

      req.flush(makeResultsPage([]));
    });

    it('maps a real 200 verbatim, including nullable strategyUsed', () => {
      const wire: PagedResponse<ReconciliationResultResponse> = makeResultsPage([
        makeResultItem({ status: 'Matched', strategyUsed: 'StrategyOne_ExactReferenceMatch' }),
        makeResultItem({ resultId: 'r2', status: 'Missing', strategyUsed: null, reasonCode: 'SOURCE_ABSENT_BANK' }),
      ]);

      let received: PagedResponse<ReconciliationResultResponse> | undefined;
      api.getResults(runId, 1, 50).subscribe((r) => (received = r));

      httpMock.expectOne((r) => r.url === resultsUrl).flush(wire);

      expect(received).toEqual(wire);
      expect(received!.items[1].strategyUsed).toBeNull();
    });

    it('maps a genuine zero-result page without alteration', () => {
      const wire = makeResultsPage([]);

      let received: PagedResponse<ReconciliationResultResponse> | undefined;
      api.getResults(runId, 1, 50).subscribe((r) => (received = r));

      httpMock.expectOne((r) => r.url === resultsUrl).flush(wire);

      expect(received).toEqual(wire);
    });

    it('surfaces a 404 (run not found) unmodified', () => {
      let body: unknown;

      api.getResults(runId, 1, 50).subscribe({
        next: () => fail('expected the 404 to error'),
        error: (err) => (body = err.error),
      });

      httpMock.expectOne((r) => r.url === resultsUrl).flush(
        { title: 'Resource Not Found', status: 404, detail: `Reconciliation run '${runId}' was not found.` },
        { status: 404, statusText: 'Not Found' },
      );

      expect(isProblemDetails(body)).toBeTrue();
    });

    it('surfaces a 400 unmodified', () => {
      let body: unknown;

      api.getResults(runId, 0, 50).subscribe({
        next: () => fail('expected the 400 to error'),
        error: (err) => (body = err.error),
      });

      httpMock.expectOne((r) => r.url === resultsUrl).flush(
        { title: 'Bad Request', status: 400, detail: 'pageNumber must be greater than or equal to 1.' },
        { status: 400, statusText: 'Bad Request' },
      );

      expect(isProblemDetails(body)).toBeTrue();
    });
  });

  describe('getResultDetail', () => {
    const detailUrl = `${runsUrl}/${runId}/results/${resultId}`;

    it('GETs the configured result-detail URL', () => {
      api.getResultDetail(runId, resultId).subscribe();

      const req = httpMock.expectOne((r) => r.url === detailUrl && r.method === 'GET');
      expect(req.request.method).toBe('GET');
      req.flush(makeDetailResponse());
    });

    it('maps a real 200 verbatim, preserving array lengths exactly (0, 1, and many)', () => {
      const wire: ReconciliationTransactionDetailResponse = makeDetailResponse({
        payments: [],
        banks: [makeSourceRecord('BANK-000001')],
        settlements: [makeSourceRecord('SET-000001'), makeSourceRecord('SET-000002')],
      });

      let received: ReconciliationTransactionDetailResponse | undefined;
      api.getResultDetail(runId, resultId).subscribe((r) => (received = r));

      httpMock.expectOne((r) => r.url === detailUrl).flush(wire);

      expect(received).toEqual(wire);
      expect(received!.payments.length).toBe(0);
      expect(received!.banks.length).toBe(1);
      expect(received!.settlements.length).toBe(2);
    });

    it('surfaces a 404 (result not found) unmodified', () => {
      let body: unknown;

      api.getResultDetail(runId, resultId).subscribe({
        next: () => fail('expected the 404 to error'),
        error: (err) => (body = err.error),
      });

      httpMock.expectOne((r) => r.url === detailUrl).flush(
        {
          title: 'Resource Not Found',
          status: 404,
          detail: `Reconciliation result '${resultId}' was not found for run '${runId}'.`,
        },
        { status: 404, statusText: 'Not Found' },
      );

      expect(isProblemDetails(body)).toBeTrue();
    });
  });

  function makeResultItem(
    overrides: Partial<ReconciliationResultResponse> = {},
  ): ReconciliationResultResponse {
    return {
      resultId: 'r1',
      runId,
      normalizedTransactionId: 'nt1',
      transactionReference: 'TXN-0001',
      status: 'Matched',
      strategyUsed: 'StrategyOne_ExactReferenceMatch',
      reasonCode: 'EXACT_MATCH',
      createdAt: '2026-08-29T09:00:00Z',
      ...overrides,
    };
  }

  function makeResultsPage(
    items: ReconciliationResultResponse[],
  ): PagedResponse<ReconciliationResultResponse> {
    return { items, pageNumber: 1, pageSize: 50, totalCount: items.length, totalPages: items.length === 0 ? 0 : 1 };
  }

  function makeSourceRecord(sourceRecordIdentifier: string) {
    return {
      id: sourceRecordIdentifier,
      sourceRecordIdentifier,
      transactionReference: 'TXN-0001',
      amount: 100.5,
      currency: 'INR',
      transactionDate: '2026-08-20',
      status: 'SUCCESS',
      createdAt: '2026-08-29T09:00:00Z',
    };
  }

  function makeDetailResponse(
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
      payments: [makeSourceRecord('PAY-000001')],
      banks: [makeSourceRecord('BANK-000001')],
      settlements: [makeSourceRecord('SET-000001')],
      ...overrides,
    };
  }

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

  const exceptionId = '44444444-4444-4444-4444-444444444444';

  function makeExceptionItem(
    overrides: Partial<ReconciliationExceptionResponse> = {},
  ): ReconciliationExceptionResponse {
    return {
      exceptionId,
      runId,
      reconciliationResultId: resultId,
      transactionReference: 'TXN-0001',
      category: 'AmountMismatch',
      involvedSources: 'Payment,Bank',
      discrepancyDetail: '{"transaction_reference":"TXN-0001"}',
      aiExplanation: null,
      aiSuggestedCategory: null,
      aiExplanationGeneratedAt: null,
      createdAt: '2026-08-29T09:00:00Z',
      updatedAt: null,
      ...overrides,
    };
  }

  describe('getExceptions', () => {
    const exceptionsUrl = `${runsUrl}/${runId}/exceptions`;

    it('GETs the configured exceptions URL', () => {
      api.getExceptions(runId, 1, 50).subscribe();

      const req = httpMock.expectOne((r) => r.url === exceptionsUrl && r.method === 'GET');
      expect(req.request.method).toBe('GET');
      req.flush({ items: [], pageNumber: 1, pageSize: 50, totalCount: 0, totalPages: 0 });
    });

    it('sends pageNumber and pageSize as query parameters, nothing else', () => {
      api.getExceptions(runId, 2, 50).subscribe();

      const req = httpMock.expectOne((r) => r.url === exceptionsUrl);
      expect(req.request.params.get('pageNumber')).toBe('2');
      expect(req.request.params.get('pageSize')).toBe('50');
      expect(req.request.params.keys().sort()).toEqual(['pageNumber', 'pageSize']);

      req.flush({ items: [], pageNumber: 2, pageSize: 50, totalCount: 0, totalPages: 0 });
    });

    it('maps a real 200 verbatim, including nullable AI fields staying null', () => {
      const wire: PagedResponse<ReconciliationExceptionResponse> = {
        items: [makeExceptionItem()],
        pageNumber: 1,
        pageSize: 50,
        totalCount: 1,
        totalPages: 1,
      };

      let received: PagedResponse<ReconciliationExceptionResponse> | undefined;
      api.getExceptions(runId, 1, 50).subscribe((r) => (received = r));

      httpMock.expectOne((r) => r.url === exceptionsUrl).flush(wire);

      expect(received).toEqual(wire);
      expect(received!.items[0].aiExplanation).toBeNull();
      expect(received!.items[0].aiSuggestedCategory).toBeNull();
      expect(received!.items[0].aiExplanationGeneratedAt).toBeNull();
    });

    it('surfaces a 404 (run not found) unmodified', () => {
      let body: unknown;

      api.getExceptions(runId, 1, 50).subscribe({
        next: () => fail('expected the 404 to error'),
        error: (err) => (body = err.error),
      });

      httpMock.expectOne((r) => r.url === exceptionsUrl).flush(
        { title: 'Resource Not Found', status: 404, detail: `Reconciliation run '${runId}' was not found.` },
        { status: 404, statusText: 'Not Found' },
      );

      expect(isProblemDetails(body)).toBeTrue();
    });

    it('surfaces a 400 unmodified', () => {
      let body: unknown;

      api.getExceptions(runId, 0, 50).subscribe({
        next: () => fail('expected the 400 to error'),
        error: (err) => (body = err.error),
      });

      httpMock.expectOne((r) => r.url === exceptionsUrl).flush(
        { title: 'Bad Request', status: 400, detail: 'pageNumber must be greater than or equal to 1.' },
        { status: 400, statusText: 'Bad Request' },
      );

      expect(isProblemDetails(body)).toBeTrue();
    });
  });

  describe('getException', () => {
    const exceptionUrl = `${environment.apiBaseUrl}/reconciliation/exceptions/${exceptionId}`;

    it('GETs the individual-exception URL, which does NOT include runId', () => {
      api.getException(exceptionId).subscribe();

      const req = httpMock.expectOne((r) => r.url === exceptionUrl && r.method === 'GET');
      expect(req.request.method).toBe('GET');
      expect(req.request.url).not.toContain(runId);
      req.flush(makeExceptionItem());
    });

    it('maps a real 200 verbatim', () => {
      const wire = makeExceptionItem({ category: 'MissingRecord', involvedSources: 'Payment' });

      let received: ReconciliationExceptionResponse | undefined;
      api.getException(exceptionId).subscribe((r) => (received = r));

      httpMock.expectOne((r) => r.url === exceptionUrl).flush(wire);

      expect(received).toEqual(wire);
    });

    it('surfaces a 404 unmodified', () => {
      let body: unknown;

      api.getException(exceptionId).subscribe({
        next: () => fail('expected the 404 to error'),
        error: (err) => (body = err.error),
      });

      httpMock.expectOne((r) => r.url === exceptionUrl).flush(
        {
          title: 'Resource Not Found',
          status: 404,
          detail: `Reconciliation exception '${exceptionId}' was not found.`,
        },
        { status: 404, statusText: 'Not Found' },
      );

      expect(isProblemDetails(body)).toBeTrue();
    });
  });

  describe('generateAiExplanation', () => {
    const aiExplanationUrl = `${environment.apiBaseUrl}/reconciliation/exceptions/${exceptionId}/ai-explanation`;

    function makeAiExplanationResponse(
      overrides: Partial<AiExplanationResponse> = {},
    ): AiExplanationResponse {
      return {
        provider: 'Gemini',
        explanation: 'The payment and bank amounts differ by INR 10.',
        suggestedCategory: 'AmountMismatch',
        generatedAtUtc: '2026-08-29T09:05:00Z',
        ...overrides,
      };
    }

    it('POSTs to the exception-scoped ai-explanation URL', () => {
      api.generateAiExplanation(exceptionId).subscribe();

      const req = httpMock.expectOne((r) => r.url === aiExplanationUrl && r.method === 'POST');
      expect(req.request.method).toBe('POST');
      req.flush(makeAiExplanationResponse());
    });

    it('maps a real 200 verbatim, including a null suggestedCategory', () => {
      const wire = makeAiExplanationResponse({ suggestedCategory: null });

      let received: AiExplanationResponse | undefined;
      api.generateAiExplanation(exceptionId).subscribe((r) => (received = r));

      httpMock.expectOne((r) => r.url === aiExplanationUrl).flush(wire);

      expect(received).toEqual(wire);
      expect(received!.suggestedCategory).toBeNull();
    });

    it('surfaces a 400 ProblemDetails unmodified', () => {
      let body: unknown;

      api.generateAiExplanation(exceptionId).subscribe({
        next: () => fail('expected the 400 to error'),
        error: (err) => (body = err.error),
      });

      httpMock.expectOne((r) => r.url === aiExplanationUrl).flush(
        { title: 'Bad Request', status: 400, detail: 'A valid exceptionId is required.' },
        { status: 400, statusText: 'Bad Request' },
      );

      expect(isProblemDetails(body)).toBeTrue();
      expect((body as { detail: string }).detail).toBe('A valid exceptionId is required.');
    });

    it('surfaces a 404 ProblemDetails unmodified', () => {
      let body: unknown;

      api.generateAiExplanation(exceptionId).subscribe({
        next: () => fail('expected the 404 to error'),
        error: (err) => (body = err.error),
      });

      httpMock.expectOne((r) => r.url === aiExplanationUrl).flush(
        {
          title: 'Resource Not Found',
          status: 404,
          detail: `Reconciliation exception '${exceptionId}' was not found.`,
        },
        { status: 404, statusText: 'Not Found' },
      );

      expect(isProblemDetails(body)).toBeTrue();
      expect((body as { detail: string }).detail).toBe(
        `Reconciliation exception '${exceptionId}' was not found.`,
      );
    });

    it('surfaces a 503 ProblemDetails unmodified', () => {
      let body: unknown;
      let status: number | undefined;

      api.generateAiExplanation(exceptionId).subscribe({
        next: () => fail('expected the 503 to error'),
        error: (err) => {
          body = err.error;
          status = err.status;
        },
      });

      httpMock.expectOne((r) => r.url === aiExplanationUrl).flush(
        { title: 'AI Provider Unavailable', status: 503, detail: 'Both AI providers failed.' },
        { status: 503, statusText: 'Service Unavailable' },
      );

      expect(status).toBe(503);
      expect(isProblemDetails(body)).toBeTrue();
    });
  });

  describe('askFinanceAssistant', () => {
    const askUrl = `${environment.apiBaseUrl}/finance-assistant/ask`;

    function makeAssistantResponse(
      overrides: Partial<FinanceAssistantResponse> = {},
    ): FinanceAssistantResponse {
      return {
        answer: 'The match rate for this run is 91.5%.',
        toolsUsed: ['getReconciliationSummary'],
        traceId: null,
        ...overrides,
      };
    }

    it('POSTs to the finance-assistant ask URL', () => {
      api.askFinanceAssistant(runId, 'What is the match rate?').subscribe();

      const req = httpMock.expectOne((r) => r.url === askUrl && r.method === 'POST');
      expect(req.request.method).toBe('POST');
      req.flush(makeAssistantResponse());
    });

    it('sends exactly { runId, question }, nothing else', () => {
      api.askFinanceAssistant(runId, 'What is the match rate?').subscribe();

      const req = httpMock.expectOne((r) => r.url === askUrl);
      expect(req.request.body).toEqual({
        runId,
        question: 'What is the match rate?',
      });

      req.flush(makeAssistantResponse());
    });

    it('maps a real 200 verbatim, including toolsUsed', () => {
      const wire = makeAssistantResponse({
        toolsUsed: ['getReconciliationSummary', 'getUnmatchedRecords'],
      });

      let received: FinanceAssistantResponse | undefined;
      api.askFinanceAssistant(runId, 'Summarize this run.').subscribe((r) => (received = r));

      httpMock.expectOne((r) => r.url === askUrl).flush(wire);

      expect(received).toEqual(wire);
      expect(received!.toolsUsed).toEqual(['getReconciliationSummary', 'getUnmatchedRecords']);
    });

    it('maps a null traceId verbatim', () => {
      const wire = makeAssistantResponse({ traceId: null });

      let received: FinanceAssistantResponse | undefined;
      api.askFinanceAssistant(runId, 'What is the match rate?').subscribe((r) => (received = r));

      httpMock.expectOne((r) => r.url === askUrl).flush(wire);

      expect(received!.traceId).toBeNull();
    });

    it('maps a non-null traceId verbatim', () => {
      const wire = makeAssistantResponse({ traceId: 'trace-abc-123' });

      let received: FinanceAssistantResponse | undefined;
      api.askFinanceAssistant(runId, 'What is the match rate?').subscribe((r) => (received = r));

      httpMock.expectOne((r) => r.url === askUrl).flush(wire);

      expect(received!.traceId).toBe('trace-abc-123');
    });

    it('surfaces a 400 ProblemDetails unmodified', () => {
      let body: unknown;

      api.askFinanceAssistant(runId, '').subscribe({
        next: () => fail('expected the 400 to error'),
        error: (err) => (body = err.error),
      });

      httpMock.expectOne((r) => r.url === askUrl).flush(
        { title: 'Bad Request', status: 400, detail: 'question is required.' },
        { status: 400, statusText: 'Bad Request' },
      );

      expect(isProblemDetails(body)).toBeTrue();
      expect((body as { detail: string }).detail).toBe('question is required.');
    });

    it('surfaces a 503 ProblemDetails unmodified', () => {
      let body: unknown;
      let status: number | undefined;

      api.askFinanceAssistant(runId, 'What is the match rate?').subscribe({
        next: () => fail('expected the 503 to error'),
        error: (err) => {
          body = err.error;
          status = err.status;
        },
      });

      httpMock.expectOne((r) => r.url === askUrl).flush(
        {
          title: 'AI Provider Unavailable',
          status: 503,
          detail:
            'Finance Assistant temporarily unavailable. Reconciliation results are unaffected.',
        },
        { status: 503, statusText: 'Service Unavailable' },
      );

      expect(status).toBe(503);
      expect(isProblemDetails(body)).toBeTrue();
      expect((body as { detail: string }).detail).toBe(
        'Finance Assistant temporarily unavailable. Reconciliation results are unaffected.',
      );
    });
  });
});
