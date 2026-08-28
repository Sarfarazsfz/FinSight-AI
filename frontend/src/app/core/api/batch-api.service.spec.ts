import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { BatchApi } from './batch-api.service';
import { environment } from '../../../environments/environment';
import { isProblemDetails } from '../models/problem-details.model';
import type { BatchIngestionResult, BatchResponse } from '../models/batch.model';
import type { PagedResponse } from '../models/paged-response.model';

function csvFile(name: string, content = 'a,b\n1,2\n'): File {
  return new File([content], name, { type: 'text/csv' });
}

describe('BatchApi', () => {
  let api: BatchApi;
  let httpMock: HttpTestingController;

  const batchesUrl = `${environment.apiBaseUrl}/batches`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    api = TestBed.inject(BatchApi);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('GETs the configured batches URL', () => {
    api.getBatches(1, 20).subscribe();

    const req = httpMock.expectOne(
      (r) => r.url === batchesUrl && r.method === 'GET',
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0 });
  });

  it('sends pageNumber and pageSize as query parameters', () => {
    api.getBatches(3, 20).subscribe();

    const req = httpMock.expectOne(
      (r) => r.url === batchesUrl,
    );
    expect(req.request.params.get('pageNumber')).toBe('3');
    expect(req.request.params.get('pageSize')).toBe('20');
    req.flush({ items: [], pageNumber: 3, pageSize: 20, totalCount: 0, totalPages: 0 });
  });

  it('maps a populated PagedResponse<BatchResponse> verbatim', () => {
    const wire: PagedResponse<BatchResponse> = {
      items: [
        {
          batchId: '11111111-1111-1111-1111-111111111111',
          batchLabel: 'August Batch 1',
          paymentRecordCount: 120,
          bankRecordCount: 118,
          settlementRecordCount: 115,
          totalRecordCount: 353,
          validationStatus: 'Valid',
          createdBy: 'ops-analyst@finsight.test',
          createdAt: '2026-08-27T09:15:00Z',
        },
      ],
      pageNumber: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
    };

    let received: PagedResponse<BatchResponse> | undefined;
    api.getBatches(1, 20).subscribe((r) => (received = r));

    httpMock.expectOne((r) => r.url === batchesUrl).flush(wire);

    expect(received).toEqual(wire);
  });

  it('maps a genuine zero-result page without alteration', () => {
    const wire: PagedResponse<BatchResponse> = {
      items: [],
      pageNumber: 1,
      pageSize: 20,
      totalCount: 0,
      totalPages: 0,
    };

    let received: PagedResponse<BatchResponse> | undefined;
    api.getBatches(1, 20).subscribe((r) => (received = r));

    httpMock.expectOne((r) => r.url === batchesUrl).flush(wire);

    expect(received).toEqual(wire);
  });

  it('surfaces a non-2xx ProblemDetails to the caller unmodified', () => {
    let status: number | undefined;
    let body: unknown;

    api.getBatches(0, 20).subscribe({
      next: () => fail('expected the 400 to error'),
      error: (err) => {
        status = err.status;
        body = err.error;
      },
    });

    httpMock.expectOne((r) => r.url === batchesUrl).flush(
      {
        type: 'https://httpstatuses.com/400',
        title: 'Bad Request',
        status: 400,
        detail: 'pageNumber must be greater than or equal to 1.',
      },
      { status: 400, statusText: 'Bad Request' },
    );

    expect(status).toBe(400);
    expect(isProblemDetails(body)).toBeTrue();
    expect((body as { detail: string }).detail).toBe(
      'pageNumber must be greater than or equal to 1.',
    );
  });

  describe('createBatch', () => {
    function submit() {
      return api.createBatch(
        'August Batch 1',
        'ops-analyst@finsight.test',
        csvFile('payments.csv'),
        csvFile('bank.csv'),
        csvFile('settlements.csv'),
      );
    }

    it('POSTs to the configured batches URL', () => {
      submit().subscribe();

      const req = httpMock.expectOne((r) => r.url === batchesUrl && r.method === 'POST');
      expect(req.request.method).toBe('POST');
      req.flush({
        batchId: '1',
        validationStatus: 'Valid',
        paymentRecordCount: 1,
        bankRecordCount: 1,
        settlementRecordCount: 1,
        totalRecordCount: 3,
      });
    });

    it('sends a FormData body with exactly the five backend field names', () => {
      submit().subscribe();

      const req = httpMock.expectOne((r) => r.url === batchesUrl);
      const body = req.request.body as FormData;

      expect(body instanceof FormData).toBeTrue();
      expect(Array.from(body.keys()).sort()).toEqual(
        ['bankFile', 'batchLabel', 'createdBy', 'paymentsFile', 'settlementsFile'].sort(),
      );
      expect(body.get('batchLabel')).toBe('August Batch 1');
      expect(body.get('createdBy')).toBe('ops-analyst@finsight.test');
      expect((body.get('paymentsFile') as File).name).toBe('payments.csv');
      expect((body.get('bankFile') as File).name).toBe('bank.csv');
      expect((body.get('settlementsFile') as File).name).toBe('settlements.csv');

      req.flush({
        batchId: '1',
        validationStatus: 'Valid',
        paymentRecordCount: 1,
        bankRecordCount: 1,
        settlementRecordCount: 1,
        totalRecordCount: 3,
      });
    });

    it('never sets a Content-Type header, leaving the browser to generate the multipart boundary', () => {
      submit().subscribe();

      const req = httpMock.expectOne((r) => r.url === batchesUrl);
      expect(req.request.headers.has('Content-Type')).toBeFalse();

      req.flush({
        batchId: '1',
        validationStatus: 'Valid',
        paymentRecordCount: 1,
        bankRecordCount: 1,
        settlementRecordCount: 1,
        totalRecordCount: 3,
      });
    });

    it('maps a real 201 BatchIngestionResult verbatim', () => {
      const wire: BatchIngestionResult = {
        batchId: '22222222-2222-2222-2222-222222222222',
        validationStatus: 'Valid',
        paymentRecordCount: 120,
        bankRecordCount: 118,
        settlementRecordCount: 115,
        totalRecordCount: 353,
      };

      let received: BatchIngestionResult | undefined;
      submit().subscribe((r) => (received = r));

      httpMock.expectOne((r) => r.url === batchesUrl).flush(wire, { status: 201, statusText: 'Created' });

      expect(received).toEqual(wire);
    });

    it('surfaces a Shape-A 400 (structured errors[]) unmodified', () => {
      let body: unknown;

      submit().subscribe({
        next: () => fail('expected the 400 to error'),
        error: (err) => (body = err.error),
      });

      httpMock.expectOne((r) => r.url === batchesUrl).flush(
        {
          title: 'Bad Request',
          status: 400,
          detail: 'Batch validation failed:...',
          errors: [
            { source: 'Payment', rowNumber: 3, field: 'amount', message: 'Amount must be greater than zero.' },
          ],
        },
        { status: 400, statusText: 'Bad Request' },
      );

      expect(isProblemDetails(body)).toBeTrue();
      const problem = body as { errors: unknown[] };
      expect(problem.errors.length).toBe(1);
      expect(problem.errors[0]).toEqual({
        source: 'Payment',
        rowNumber: 3,
        field: 'amount',
        message: 'Amount must be greater than zero.',
      });
    });

    it('surfaces a Shape-B 400 (detail only, no errors[]) unmodified', () => {
      let body: unknown;

      submit().subscribe({
        next: () => fail('expected the 400 to error'),
        error: (err) => (body = err.error),
      });

      httpMock.expectOne((r) => r.url === batchesUrl).flush(
        {
          title: 'Bad Request',
          status: 400,
          detail: 'Missing required CSV column(s): amount, currency',
        },
        { status: 400, statusText: 'Bad Request' },
      );

      expect(isProblemDetails(body)).toBeTrue();
      expect((body as { detail: string; errors?: unknown[] }).errors).toBeUndefined();
      expect((body as { detail: string }).detail).toBe(
        'Missing required CSV column(s): amount, currency',
      );
    });
  });
});
