import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { BatchUploadPage } from './batch-upload-page';
import { AuthStore } from '../../../core/state/auth-store';
import { environment } from '../../../../environments/environment';
import type { BatchIngestionResult } from '../../../core/models/batch.model';

describe('BatchUploadPage', () => {
  let fixture: ComponentFixture<BatchUploadPage>;
  let httpMock: HttpTestingController;
  let authStore: AuthStore;

  const batchesUrl = `${environment.apiBaseUrl}/batches`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [BatchUploadPage],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    httpMock = TestBed.inject(HttpTestingController);
    authStore = TestBed.inject(AuthStore);
    authStore.setSession({
      accessToken: 'token',
      tokenType: 'Bearer',
      expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
      userId: '1',
      email: 'ops-analyst@finsight.test',
      role: 'User',
    });

    fixture = TestBed.createComponent(BatchUploadPage);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function submitButton(): HTMLButtonElement {
    return el().querySelector<HTMLButtonElement>('button[type="submit"]')!;
  }

  function fileInputs(): HTMLInputElement[] {
    return Array.from(el().querySelectorAll<HTMLInputElement>('[data-testid="file-slot-input"]'));
  }

  function csvFile(name: string): File {
    return new File(['a,b\n1,2\n'], name, { type: 'text/csv' });
  }

  function selectFile(index: number, name: string): void {
    const input = fileInputs()[index];
    const dataTransfer = new DataTransfer();
    dataTransfer.items.add(csvFile(name));
    input.files = dataTransfer.files;
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  }

  function setLabel(value: string): void {
    const input = el().querySelector<HTMLInputElement>('#batch-label')!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function fillCompleteForm(): void {
    setLabel('August Batch 1');
    selectFile(0, 'payments.csv');
    selectFile(1, 'bank.csv');
    selectFile(2, 'settlements.csv');
  }

  function successResult(overrides: Partial<BatchIngestionResult> = {}): BatchIngestionResult {
    return {
      batchId: '11111111-1111-1111-1111-111111111111',
      validationStatus: 'Valid',
      paymentRecordCount: 10,
      bankRecordCount: 10,
      settlementRecordCount: 10,
      totalRecordCount: 30,
      ...overrides,
    };
  }

  it('disables submit until a label and all three files are present', () => {
    expect(submitButton().disabled).toBeTrue();

    setLabel('August Batch 1');
    expect(submitButton().disabled).toBeTrue();

    selectFile(0, 'payments.csv');
    selectFile(1, 'bank.csv');
    expect(submitButton().disabled).toBeTrue();

    selectFile(2, 'settlements.csv');
    expect(submitButton().disabled).toBeFalse();
  });

  it('sends no HTTP request while the form is incomplete', () => {
    setLabel('August Batch 1');
    selectFile(0, 'payments.csv');
    // Bank and Settlement intentionally left empty.

    submitButton().click();

    const requests = httpMock.match(() => true);
    expect(requests.length).toBe(0);
  });

  it('submits with createdBy from AuthStore, never a typed value', () => {
    fillCompleteForm();
    submitButton().click();

    const req = httpMock.expectOne((r) => r.url === batchesUrl && r.method === 'POST');
    const body = req.request.body as FormData;
    expect(body.get('createdBy')).toBe('ops-analyst@finsight.test');
    expect(body.get('batchLabel')).toBe('August Batch 1');

    req.flush(successResult());
  });

  it('shows the submitting state and disables the form while in flight', () => {
    fillCompleteForm();
    submitButton().click();
    fixture.detectChanges();

    expect(submitButton().disabled).toBeTrue();
    expect(submitButton().textContent).toContain('Uploading');

    httpMock.expectOne((r) => r.url === batchesUrl).flush(successResult());
  });

  it('renders the real 201 result verbatim on success', () => {
    fillCompleteForm();
    submitButton().click();

    const result = successResult({
      batchId: '22222222-2222-2222-2222-222222222222',
      paymentRecordCount: 120,
      bankRecordCount: 118,
      settlementRecordCount: 115,
      totalRecordCount: 353,
    });
    httpMock.expectOne((r) => r.url === batchesUrl).flush(result, { status: 201, statusText: 'Created' });
    fixture.detectChanges();

    const success = el().querySelector('[data-testid="upload-success"]')!;
    expect(success).toBeTruthy();
    expect(success.textContent).toContain('22222222-2222-2222-2222-222222222222');
    expect(success.textContent).toContain('120');
    expect(success.textContent).toContain('118');
    expect(success.textContent).toContain('115');
    expect(success.textContent).toContain('353');
    expect(success.textContent).toContain('August Batch 1');
  });

  it('renders structured Shape-A errors grouped by source, using the fields verbatim', () => {
    fillCompleteForm();
    submitButton().click();

    httpMock.expectOne((r) => r.url === batchesUrl).flush(
      {
        title: 'Bad Request',
        status: 400,
        detail: 'Batch validation failed:...',
        errors: [
          { source: 'Payment', rowNumber: 3, field: 'amount', message: 'Amount must be greater than zero.' },
          { source: 'Bank', rowNumber: 5, field: 'currency', message: 'Currency must be INR.' },
        ],
      },
      { status: 400, statusText: 'Bad Request' },
    );
    fixture.detectChanges();

    const error = el().querySelector('[data-testid="upload-error"]')!;
    expect(error.textContent).toContain('Payment');
    expect(error.textContent).toContain('Row 3');
    expect(error.textContent).toContain('amount');
    expect(error.textContent).toContain('Amount must be greater than zero.');
    expect(error.textContent).toContain('Bank');
    expect(error.textContent).toContain('Row 5');
    expect(error.textContent).toContain('Currency must be INR.');
    expect(el().querySelector('[data-testid="upload-error-detail"]')).toBeFalsy();
  });

  it('renders a Shape-B detail-only error as one plain message, not fabricated rows', () => {
    fillCompleteForm();
    submitButton().click();

    httpMock.expectOne((r) => r.url === batchesUrl).flush(
      {
        title: 'Bad Request',
        status: 400,
        detail: 'Missing required CSV column(s): amount, currency',
      },
      { status: 400, statusText: 'Bad Request' },
    );
    fixture.detectChanges();

    const detail = el().querySelector('[data-testid="upload-error-detail"]')!;
    expect(detail.textContent).toContain('Missing required CSV column(s): amount, currency');
  });

  it('shows a generic honest message on 413 without inventing a size limit', () => {
    fillCompleteForm();
    submitButton().click();

    httpMock.expectOne((r) => r.url === batchesUrl).flush(null, { status: 413, statusText: 'Payload Too Large' });
    fixture.detectChanges();

    const detail = el().querySelector('[data-testid="upload-error-detail"]')!;
    expect(detail.textContent).toContain('too large');
    expect(detail.textContent).not.toMatch(/\d+\s*(MB|KB|GB)/);
  });

  it('shows a generic honest message on 500', () => {
    fillCompleteForm();
    submitButton().click();

    httpMock.expectOne((r) => r.url === batchesUrl).flush(
      { title: 'An unexpected error occurred.', status: 500 },
      { status: 500, statusText: 'Internal Server Error' },
    );
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="upload-error-detail"]')!.textContent).toContain(
      'server could not complete',
    );
  });

  it('shows a clear server-unreachable message on a network failure', () => {
    fillCompleteForm();
    submitButton().click();

    httpMock.expectOne((r) => r.url === batchesUrl).error(new ProgressEvent('error'), { status: 0 });
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="upload-error-detail"]')!.textContent).toContain(
      'Cannot reach the server',
    );
  });

  it('preserves selected files after an error and resubmits the same selection on retry', () => {
    fillCompleteForm();
    submitButton().click();

    httpMock.expectOne((r) => r.url === batchesUrl).flush(
      { title: 'Bad Request', status: 400, detail: 'Missing required CSV column(s): amount' },
      { status: 400, statusText: 'Bad Request' },
    );
    fixture.detectChanges();

    // Files remain selected -- the slots still show the selected-file view.
    expect(el().querySelectorAll('[data-testid="file-slot-selected"]').length).toBe(3);
    expect(submitButton().disabled).toBeFalse();

    submitButton().click();

    const retryReq = httpMock.expectOne((r) => r.url === batchesUrl && r.method === 'POST');
    const body = retryReq.request.body as FormData;
    expect((body.get('paymentsFile') as File).name).toBe('payments.csv');
    expect((body.get('bankFile') as File).name).toBe('bank.csv');
    expect((body.get('settlementsFile') as File).name).toBe('settlements.csv');

    retryReq.flush(successResult());
  });

  it('does not render bespoke session-expired copy on a 401 -- that is the global interceptor’s job', () => {
    fillCompleteForm();
    submitButton().click();

    httpMock.expectOne((r) => r.url === batchesUrl).flush(
      { title: 'Unauthorized', status: 401 },
      { status: 401, statusText: 'Unauthorized' },
    );
    fixture.detectChanges();

    const text = el().textContent!.toLowerCase();
    expect(text).not.toContain('session');
    expect(text).not.toContain('expired');
  });

  it('resets the form when "Upload another batch" is clicked after success', () => {
    fillCompleteForm();
    submitButton().click();
    httpMock.expectOne((r) => r.url === batchesUrl).flush(successResult());
    fixture.detectChanges();

    el().querySelector<HTMLButtonElement>('[data-testid="upload-success"] button:last-of-type')!.click();
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="upload-success"]')).toBeFalsy();
    expect(submitButton().disabled).toBeTrue();
    expect(el().querySelectorAll('[data-testid="file-slot-selected"]').length).toBe(0);
  });
});
