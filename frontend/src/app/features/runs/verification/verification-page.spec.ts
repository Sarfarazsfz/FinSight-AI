import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { VerificationPage } from './verification-page';
import { environment } from '../../../../environments/environment';
import type { GroundTruthComparisonResult } from '../../../core/models/reconciliation.model';

const runId = '55555555-5555-5555-5555-555555555555';
const VERIFY_URL =
  `${environment.apiBaseUrl}/reconciliation/runs/${runId}/ground-truth-verification`;

const HEADER =
  'transaction_reference,scenario_type,expected_status,expected_reason_code,' +
  'expected_exception_category,expected_payment_present,expected_bank_present,' +
  'expected_settlement_present,expected_amount_relationship,expected_date_relationship';

const CSV =
  `${HEADER}\n` +
  'TXN-0001,ExactMatch,Matched,EXACT_MATCH,,true,true,true,Exact,Exact\n' +
  'TXN-0002,MissingBank,Missing,SOURCE_ABSENT_BANK,MissingRecord,true,false,true,NotComparable,NotComparable';

function passResult(
  overrides: Partial<GroundTruthComparisonResult> = {},
): GroundTruthComparisonResult {
  return {
    isSuccess: true,
    expectedTotalUnits: 2,
    actualTotalUnits: 2,
    expectedMatched: 1,
    actualMatched: 1,
    expectedMismatched: 0,
    actualMismatched: 0,
    expectedMissing: 1,
    actualMissing: 1,
    expectedDuplicate: 0,
    actualDuplicate: 0,
    expectedUnresolved: 0,
    actualUnresolved: 0,
    expectedMatchRate: 50,
    actualMatchRate: 50,
    failures: [],
    ...overrides,
  };
}

describe('VerificationPage', () => {
  let fixture: ComponentFixture<VerificationPage>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [VerificationPage],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ runId }) } },
        },
      ],
    });

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(VerificationPage);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  /** Drives the real file input with a real File, as a user would. */
  async function selectFile(contents = CSV, name = 'ground-truth.csv'): Promise<void> {
    const input = el().querySelector<HTMLInputElement>(
      '[data-testid="ground-truth-file-input"]',
    )!;

    const file = new File([contents], name, { type: 'text/csv' });

    Object.defineProperty(input, 'files', { value: [file], configurable: true });
    input.dispatchEvent(new Event('change'));

    // file.text() is async.
    await file.text();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  function clickVerify(): void {
    el().querySelector<HTMLButtonElement>('[data-testid="verify-button"]')!.click();
    fixture.detectChanges();
  }

  it('renders the verification workspace without implying verification already happened', () => {
    expect(el().querySelector('#verification-heading')!.textContent)
      .toContain('Ground Truth Verification');

    expect(el().textContent).toContain(
      'Verification compares this ground-truth file against the deterministic reconciliation results for this run.',
    );

    expect(el().querySelector('[data-testid="verification-result"]')).toBeNull();
    expect(el().textContent).not.toContain('PASS');
  });

  it('disables verification until a valid file is chosen', () => {
    const button = el().querySelector<HTMLButtonElement>(
      '[data-testid="verify-button"]',
    )!;

    expect(button.disabled).toBeTrue();
  });

  it('parses the chosen file and reports the row count', async () => {
    await selectFile();

    expect(el().querySelector('[data-testid="ground-truth-file-selected"]')).toBeTruthy();
    expect(el().querySelector('[data-testid="ground-truth-row-count"]')!.textContent)
      .toContain('2');
    expect(
      el().querySelector<HTMLButtonElement>('[data-testid="verify-button"]')!.disabled,
    ).toBeFalse();
  });

  it('rejects a malformed file locally without calling the API', async () => {
    await selectFile('not,a,ground,truth,file');

    expect(el().querySelector('[data-testid="ground-truth-file-error"]')).toBeTruthy();
    expect(
      el().querySelector<HTMLButtonElement>('[data-testid="verify-button"]')!.disabled,
    ).toBeTrue();

    httpMock.expectNone(VERIFY_URL);
  });

  it('posts the parsed rows to the real verification endpoint', async () => {
    await selectFile();
    clickVerify();

    const request = httpMock.expectOne(VERIFY_URL);

    expect(request.request.method).toBe('POST');
    expect(Array.isArray(request.request.body)).toBeTrue();
    expect(request.request.body.length).toBe(2);
    expect(request.request.body[0].transactionReference).toBe('TXN-0001');
    expect(request.request.body[0].expectedPaymentPresent).toBeTrue();

    request.flush(passResult());
  });

  it('shows a processing state while the comparison runs', async () => {
    await selectFile();
    clickVerify();

    expect(el().querySelector('[data-testid="verification-loading"]')).toBeTruthy();
    expect(el().textContent).toContain(
      'Comparing ground-truth labels against deterministic results',
    );

    httpMock.expectOne(VERIFY_URL).flush(passResult());
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="verification-loading"]')).toBeNull();
  });

  it('renders PASS with zero failures when the backend reports success', async () => {
    await selectFile();
    clickVerify();
    httpMock.expectOne(VERIFY_URL).flush(passResult());
    fixture.detectChanges();

    const verdict = el().querySelector('[data-testid="verification-verdict"]')!;

    expect(verdict.textContent).toContain('PASS');
    expect(verdict.textContent).toContain('Ground truth verified');
    expect(el().textContent).toContain('0 verification failures');
    expect(el().querySelector('[data-testid="verification-failures"]')).toBeNull();
  });

  it('renders FAIL and lists every failure verbatim', async () => {
    const failures = [
      "TXN-0002: status mismatch. Expected 'Missing', actual 'Matched'.",
      'Missing count mismatch. Expected 1, actual 0.',
    ];

    await selectFile();
    clickVerify();
    httpMock.expectOne(VERIFY_URL).flush(
      passResult({
        isSuccess: false,
        actualMatched: 2,
        actualMissing: 0,
        actualMatchRate: 100,
        failures,
      }),
    );
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="verification-verdict"]')!.textContent)
      .toContain('FAIL');
    expect(el().querySelector('[data-testid="verification-failure-count"]')!.textContent)
      .toContain('2');

    const items = el().querySelectorAll('[data-testid="verification-failures"] li');

    expect(items.length).toBe(2);
    // Verbatim -- not reformatted, not parsed into pseudo-structure.
    expect(items[0].textContent!.trim()).toBe(failures[0]);
    expect(items[1].textContent!.trim()).toBe(failures[1]);
  });

  it('shows expected vs actual for all five statuses plus totals and match rate', async () => {
    await selectFile();
    clickVerify();
    httpMock.expectOne(VERIFY_URL).flush(
      passResult({
        isSuccess: false,
        expectedTotalUnits: 100,
        actualTotalUnits: 100,
        expectedMatched: 97,
        actualMatched: 70,
        expectedMismatched: 1,
        actualMismatched: 10,
        expectedMissing: 2,
        actualMissing: 12,
        expectedDuplicate: 0,
        actualDuplicate: 6,
        expectedUnresolved: 0,
        actualUnresolved: 2,
        expectedMatchRate: 97,
        actualMatchRate: 70,
        failures: ['Matched count mismatch. Expected 97, actual 70.'],
      }),
    );
    fixture.detectChanges();

    const table = el().querySelector('[data-testid="verification-comparison"]')!;
    const text = table.textContent!;

    for (const label of [
      'Total units',
      'Matched',
      'Mismatched',
      'Missing',
      'Duplicate',
      'Unresolved',
      'Match rate',
    ]) {
      expect(text).withContext(label).toContain(label);
    }

    expect(table.querySelectorAll('tbody tr').length).toBe(7);

    // Exact server values on both sides.
    expect(text).toContain('97');
    expect(text).toContain('70');
    expect(text).toContain('12');
    expect(text).toContain('6');
  });

  it('never invents a verification id or timestamp, and says the result is not stored', async () => {
    await selectFile();
    clickVerify();
    httpMock.expectOne(VERIFY_URL).flush(passResult());
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="verification-persistence-note"]')!.textContent)
      .toContain('The verification result is not stored.');

    const text = el().textContent!.toLowerCase();
    expect(text).not.toContain('verification id');
    expect(text).not.toContain('verified at');
    expect(text).not.toContain('self-verified');
    expect(text).not.toContain('ai verified');
  });

  it('makes clear the labels are operator-supplied, not self-generated proof', () => {
    expect(el().textContent).toContain('supplied by you');
  });

  it('surfaces a 400 from the server without losing the chosen file', async () => {
    await selectFile();
    clickVerify();
    httpMock.expectOne(VERIFY_URL).flush(
      {
        title: 'Bad Request',
        status: 400,
        detail: 'A non-empty ground-truth row array is required.',
      },
      { status: 400, statusText: 'Bad Request' },
    );
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="verification-request-error"]')!.textContent)
      .toContain('A non-empty ground-truth row array is required.');
    expect(el().querySelector('[data-testid="ground-truth-file-selected"]')).toBeTruthy();
  });

  it('surfaces a 404 for a run that no longer exists', async () => {
    await selectFile();
    clickVerify();
    httpMock.expectOne(VERIFY_URL).flush(
      {
        title: 'Resource Not Found',
        status: 404,
        detail: `Reconciliation run '${runId}' was not found.`,
      },
      { status: 404, statusText: 'Not Found' },
    );
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="verification-request-error"]')!.textContent)
      .toContain('was not found');
  });

  it('reports a server error without exposing internals, and offers a retry', async () => {
    await selectFile();
    clickVerify();
    httpMock.expectOne(VERIFY_URL).flush(
      { title: 'An unexpected error occurred.', status: 500 },
      { status: 500, statusText: 'Server Error' },
    );
    fixture.detectChanges();

    const error = el().querySelector('[data-testid="verification-request-error"]')!;

    expect(error.getAttribute('role')).toBe('alert');
    expect(error.textContent).not.toContain('traceId');
    expect(error.textContent).not.toContain('at FinSight');
    expect(el().querySelector('[data-testid="verification-retry"]')).toBeTruthy();
  });

  it('reports a network failure honestly', async () => {
    await selectFile();
    clickVerify();
    httpMock.expectOne(VERIFY_URL).error(new ProgressEvent('error'), { status: 0 });
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="verification-request-error"]')!.textContent)
      .toContain('Cannot reach the server');
  });

  it('retries with the same parsed rows', async () => {
    await selectFile();
    clickVerify();
    httpMock.expectOne(VERIFY_URL).error(new ProgressEvent('error'), { status: 0 });
    fixture.detectChanges();

    el().querySelector<HTMLButtonElement>('[data-testid="verification-retry"]')!.click();
    fixture.detectChanges();

    const retry = httpMock.expectOne(VERIFY_URL);
    expect(retry.request.body.length).toBe(2);
    retry.flush(passResult());
  });

  it('offers navigation back to the run', async () => {
    expect(
      el()
        .querySelector('[data-testid="verification-back-to-run"]')!
        .getAttribute('href'),
    ).toBe(`/runs/${runId}`);

    await selectFile();
    clickVerify();
    httpMock.expectOne(VERIFY_URL).flush(passResult());
    fixture.detectChanges();

    expect(
      el()
        .querySelector('[data-testid="verification-result-back"]')!
        .getAttribute('href'),
    ).toBe(`/runs/${runId}`);
  });

  it('clears the result when the file is removed, so a stale verdict never lingers', async () => {
    await selectFile();
    clickVerify();
    httpMock.expectOne(VERIFY_URL).flush(passResult());
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="verification-result"]')).toBeTruthy();

    el().querySelector<HTMLButtonElement>('[data-testid="verification-verify-another"]')!.click();
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="verification-result"]')).toBeNull();
    expect(el().querySelector('[data-testid="ground-truth-file-selected"]')).toBeNull();
  });
});
