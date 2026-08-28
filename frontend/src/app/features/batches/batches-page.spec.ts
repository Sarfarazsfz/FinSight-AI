import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { BatchesPage } from './batches-page';

describe('BatchesPage', () => {
  let fixture: ComponentFixture<BatchesPage>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [BatchesPage],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(BatchesPage);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  it('renders an honest empty state', () => {
    const empty = el().querySelector('[data-testid="batches-empty"]');

    expect(empty).toBeTruthy();
    expect(empty!.textContent).toContain('No batches yet');
  });

  it('makes no HTTP request — batch integration is a later phase', () => {
    // `match` returns every request the component issued, so asserting the
    // count proves the absence directly. `expectNone` would also fail the
    // test, but it registers no Jasmine expectation, which makes the spec
    // look empty to the runner.
    //
    // This matters beyond tidiness: a skeleton or spinner on this page would
    // imply a request that does not exist yet.
    const requests = httpMock.match(() => true);

    expect(requests.length).toBe(0);
  });

  it('fabricates no batches, counts or statistics', () => {
    const text = el().textContent!;

    // No digits at all: any number on this page today would be invented data
    // in a financial tool.
    expect(/\d/.test(text)).toBeFalse();
  });

  it('contains no challenge-track or internal roadmap language', () => {
    const text = el().textContent!.toLowerCase();

    expect(text).not.toContain('track 04');
    expect(text).not.toContain('buildathon');
    // Internal delivery vocabulary must never surface in product copy.
    expect(text).not.toContain('phase');
    expect(text).not.toContain('sprint');
    expect(text).not.toContain('roadmap');
  });
});
