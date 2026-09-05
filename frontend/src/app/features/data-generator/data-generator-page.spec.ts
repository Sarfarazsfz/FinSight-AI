import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { DataGeneratorPage } from './data-generator-page';
import {
  CorruptionIntensity,
  GenerationMode,
  GeneratedDatasetMetadata,
  GenerateDatasetResponse,
} from '../../core/api/data-generator-api.service';
import { environment } from '../../../environments/environment';

const GENERATE_URL = `${environment.apiBaseUrl}/test-data/generate`;

function fakeMetadata(overrides: Partial<GeneratedDatasetMetadata> = {}): GeneratedDatasetMetadata {
  return {
    generationId: 'abc123',
    seed: 81_729_431,
    mode: GenerationMode.Mixed,
    size: 100,
    intensity: CorruptionIntensity.Medium,
    createdAt: new Date().toISOString(),
    scenarioDistribution: {
      Matched: 74,
      Mismatched: 10,
      Missing: 8,
      Duplicate: 5,
      Unresolved: 3,
    },
    expectedMatched: 74,
    expectedMismatched: 10,
    expectedMissing: 8,
    expectedDuplicate: 5,
    expectedUnresolved: 3,
    ...overrides,
  };
}

function fakeResponse(meta?: Partial<GeneratedDatasetMetadata>): GenerateDatasetResponse {
  return { metadata: fakeMetadata(meta) };
}

describe('DataGeneratorPage', () => {
  let fixture: ComponentFixture<DataGeneratorPage>;
  let httpMock: HttpTestingController;

  function configure(): void {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [DataGeneratorPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });

    httpMock  = TestBed.inject(HttpTestingController);
    fixture   = TestBed.createComponent(DataGeneratorPage);
    fixture.detectChanges();
  }

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  afterEach(() => {
    httpMock.verify();
  });

  // -------------------------------------------------------------------------
  // 28 — Page loads
  // -------------------------------------------------------------------------
  it('renders the page header and generate button', () => {
    configure();

    expect(el().textContent).toContain('Synthetic Data Lab');
    expect(el().querySelector('[data-testid="generate-button"]')).toBeTruthy();
  });

  // -------------------------------------------------------------------------
  // 29 — Form validation: generate without API call when seed is invalid
  // -------------------------------------------------------------------------
  it('shows error and makes no API call when seed is a negative number', () => {
    configure();

    const seedInput = el().querySelector<HTMLInputElement>('[data-testid="seed-input"]')!;
    seedInput.value = '-5';
    seedInput.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    el().querySelector<HTMLButtonElement>('[data-testid="generate-button"]')!.click();
    fixture.detectChanges();

    httpMock.expectNone(GENERATE_URL);
    expect(el().querySelector('[data-testid="generation-error"]')).toBeTruthy();
  });

  // -------------------------------------------------------------------------
  // 30 — Generate triggers API call with correct payload
  // -------------------------------------------------------------------------
  it('clicking Generate sends the correct request body', () => {
    configure();

    el().querySelector<HTMLButtonElement>('[data-testid="generate-button"]')!.click();
    fixture.detectChanges();

    const req = httpMock.expectOne(GENERATE_URL);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.size).toBe(100);          // default size
    expect(req.request.body.mode).toBe(GenerationMode.Mixed);
    expect(req.request.body.intensity).toBe(CorruptionIntensity.Medium);
    expect(req.request.body.seed).toBeNull();          // no seed → null

    req.flush(fakeResponse());
  });

  // -------------------------------------------------------------------------
  // 31 — Seed displayed after generation
  // -------------------------------------------------------------------------
  it('displays the seed returned by the API after generation', () => {
    configure();

    el().querySelector<HTMLButtonElement>('[data-testid="generate-button"]')!.click();
    fixture.detectChanges();

    httpMock.expectOne(GENERATE_URL).flush(fakeResponse({ seed: 81_729_431 }));
    fixture.detectChanges();

    const seedEl = el().querySelector('[data-testid="result-seed"]');
    expect(seedEl).toBeTruthy();
    expect(seedEl!.textContent).toContain('81');
  });

  // -------------------------------------------------------------------------
  // 32 — Expected scenario mix displayed
  // -------------------------------------------------------------------------
  it('shows expected scenario distribution after generation', () => {
    configure();

    el().querySelector<HTMLButtonElement>('[data-testid="generate-button"]')!.click();
    fixture.detectChanges();

    httpMock.expectOne(GENERATE_URL).flush(fakeResponse());
    fixture.detectChanges();

    const resultSection = el().querySelector('[data-testid="result-section"]');
    expect(resultSection).toBeTruthy();
    expect(resultSection!.textContent).toContain('Matched');
    expect(resultSection!.textContent).toContain('74');
    expect(resultSection!.textContent).toContain('Mismatched');
  });

  // -------------------------------------------------------------------------
  // 33 — Download controls rendered
  // -------------------------------------------------------------------------
  it('shows download buttons after generation', () => {
    configure();

    el().querySelector<HTMLButtonElement>('[data-testid="generate-button"]')!.click();
    fixture.detectChanges();
    httpMock.expectOne(GENERATE_URL).flush(fakeResponse());
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="download-payments"]')).toBeTruthy();
    expect(el().querySelector('[data-testid="download-bank"]')).toBeTruthy();
    expect(el().querySelector('[data-testid="download-settlements"]')).toBeTruthy();
    expect(el().querySelector('[data-testid="download-ground-truth"]')).toBeTruthy();
  });

  // -------------------------------------------------------------------------
  // 34 — Upload navigation link present
  // -------------------------------------------------------------------------
  it('shows an upload link after generation', () => {
    configure();

    el().querySelector<HTMLButtonElement>('[data-testid="generate-button"]')!.click();
    fixture.detectChanges();
    httpMock.expectOne(GENERATE_URL).flush(fakeResponse());
    fixture.detectChanges();

    const uploadLink = el().querySelector('[data-testid="upload-link"]');
    expect(uploadLink).toBeTruthy();
    expect(uploadLink!.getAttribute('href')).toBe('/batches/upload');
  });

  // -------------------------------------------------------------------------
  // 35 — Loading state: button disabled while generating
  // -------------------------------------------------------------------------
  it('disables the generate button while request is in flight', () => {
    configure();

    const btn = el().querySelector<HTMLButtonElement>('[data-testid="generate-button"]')!;
    btn.click();
    fixture.detectChanges();

    expect(btn.disabled).toBeTrue();
    expect(btn.textContent).toContain('Generating');

    httpMock.expectOne(GENERATE_URL).flush(fakeResponse());
    fixture.detectChanges();

    expect(btn.disabled).toBeFalse();
  });

  // -------------------------------------------------------------------------
  // 36 — Error state: API error shows error message
  // -------------------------------------------------------------------------
  it('shows error message when API call fails', () => {
    configure();

    el().querySelector<HTMLButtonElement>('[data-testid="generate-button"]')!.click();
    fixture.detectChanges();

    httpMock.expectOne(GENERATE_URL).error(
      new ProgressEvent('error'),
      { status: 500, statusText: 'Server Error' },
    );
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="generation-error"]')).toBeTruthy();
    expect(el().querySelector('[data-testid="result-section"]')).toBeNull();
  });

  // -------------------------------------------------------------------------
  // 37 — No secrets rendered
  // -------------------------------------------------------------------------
  it('never renders any credential, key, or secret in the page', () => {
    configure();

    el().querySelector<HTMLButtonElement>('[data-testid="generate-button"]')!.click();
    fixture.detectChanges();
    httpMock.expectOne(GENERATE_URL).flush(fakeResponse());
    fixture.detectChanges();

    const text = el().textContent!.toLowerCase();
    expect(text).not.toContain('apikey');
    expect(text).not.toContain('secret');
    expect(text).not.toContain('password');
    expect(text).not.toContain('connectionstring');
    expect(text).not.toContain('jwt');
  });

  // -------------------------------------------------------------------------
  // 38 — No auto-reconciliation: generate does NOT call the reconciliation API
  // -------------------------------------------------------------------------
  it('does not call the reconciliation API on generate', () => {
    configure();

    el().querySelector<HTMLButtonElement>('[data-testid="generate-button"]')!.click();
    fixture.detectChanges();

    // Only the generate endpoint should be called.
    httpMock.expectOne(GENERATE_URL).flush(fakeResponse());
    fixture.detectChanges();

    // Verify: no call to the reconciliation endpoint was made.
    const reconciliationCalls = httpMock.match(
      (req) => req.url.includes('reconciliation'),
    );
    expect(reconciliationCalls.length)
      .withContext('generate must not auto-trigger reconciliation')
      .toBe(0);
  });

  // -------------------------------------------------------------------------
  // Additional: explicit seed is sent in request body
  // -------------------------------------------------------------------------
  it('sends explicit seed in request body when provided', () => {
    configure();

    const seedInput = el().querySelector<HTMLInputElement>('[data-testid="seed-input"]')!;
    seedInput.value = '42026';
    seedInput.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    el().querySelector<HTMLButtonElement>('[data-testid="generate-button"]')!.click();
    fixture.detectChanges();

    const req = httpMock.expectOne(GENERATE_URL);
    expect(req.request.body.seed).toBe(42026);

    req.flush(fakeResponse({ seed: 42026 }));
  });

  // -------------------------------------------------------------------------
  // Additional: result section hidden before first generation
  // -------------------------------------------------------------------------
  it('does not show result section before generation', () => {
    configure();
    expect(el().querySelector('[data-testid="result-section"]')).toBeNull();
  });

  // -------------------------------------------------------------------------
  // Additional: default mode (Mixed=8) is sent as numeric enum value
  // -------------------------------------------------------------------------
  it('sends mode as numeric enum value matching backend expectations (default = Mixed)', () => {
    configure();

    // Default selectedMode is Mixed (8) — verify it reaches the API as a number.
    el().querySelector<HTMLButtonElement>('[data-testid="generate-button"]')!.click();
    fixture.detectChanges();

    const req = httpMock.expectOne(GENERATE_URL);
    expect(typeof req.request.body.mode).toBe('number');
    expect(req.request.body.mode).toBe(GenerationMode.Mixed);  // 8

    req.flush(fakeResponse());
  });
});
