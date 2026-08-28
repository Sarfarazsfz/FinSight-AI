import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { errorInterceptor } from './error.interceptor';
import { AuthStore } from '../state/auth-store';
import { environment } from '../../../environments/environment';
import type { LoginResponse } from '../models/auth.model';

function validLogin(): LoginResponse {
  return {
    accessToken: 'token-abc',
    tokenType: 'Bearer',
    expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString(),
    userId: '66666666-6666-6666-6666-666666666666',
    email: 'operator@finsight.test',
    role: 'User',
  };
}

describe('errorInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let store: AuthStore;
  let navigateSpy: jasmine.Spy;

  const url = `${environment.apiBaseUrl}/batches`;

  beforeEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        {
          provide: Router,
          useValue: {
            url: '/batches',
            navigate: jasmine.createSpy('navigate').and.resolveTo(true),
          },
        },
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    store = TestBed.inject(AuthStore);
    navigateSpy = TestBed.inject(Router).navigate as jasmine.Spy;
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('clears the session and redirects with returnUrl on 401', () => {
    store.setSession(validLogin());
    expect(store.isAuthenticated()).toBeTrue();

    let caught: number | undefined;
    http.get(url).subscribe({ error: (e) => (caught = e.status) });

    httpMock.expectOne(url).flush(
      { title: 'Unauthorized', status: 401, detail: 'Invalid email or password.' },
      { status: 401, statusText: 'Unauthorized' },
    );

    expect(store.isAuthenticated()).toBeFalse();
    expect(navigateSpy).toHaveBeenCalledWith(['/login'], {
      queryParams: { returnUrl: '/batches' },
    });
    // The error is still rethrown for the caller.
    expect(caught).toBe(401);
  });

  [
    { status: 400, statusText: 'Bad Request' },
    { status: 404, statusText: 'Not Found' },
    { status: 500, statusText: 'Internal Server Error' },
    { status: 503, statusText: 'Service Unavailable' },
  ].forEach(({ status, statusText }) => {
    it(`passes a ${status} through untouched`, () => {
      store.setSession(validLogin());

      let caught: number | undefined;
      let body: unknown;
      http.get(url).subscribe({
        error: (e) => {
          caught = e.status;
          body = e.error;
        },
      });

      httpMock
        .expectOne(url)
        .flush({ title: statusText, status, detail: 'context-specific' }, { status, statusText });

      expect(caught).toBe(status);
      // Body reaches the caller unmodified, and the session is untouched.
      expect((body as { detail: string }).detail).toBe('context-specific');
      expect(store.isAuthenticated()).toBeTrue();
      expect(navigateSpy).not.toHaveBeenCalled();
    });
  });

  it('does NOT redirect on a 401 when there was no session (failed sign-in)', () => {
    // A failed login also returns 401. The user is already on the login
    // page with no session, so treating it as an expiry would redirect them
    // to where they already are and stamp ?returnUrl=/login onto the URL.
    expect(store.isAuthenticated()).toBeFalse();

    let caught: number | undefined;
    http.post(`${environment.apiBaseUrl}/auth/login`, {}).subscribe({
      error: (e) => (caught = e.status),
    });

    httpMock.expectOne(`${environment.apiBaseUrl}/auth/login`).flush(
      { title: 'Unauthorized', status: 401, detail: 'Invalid email or password.' },
      { status: 401, statusText: 'Unauthorized' },
    );

    expect(caught).toBe(401);
    expect(navigateSpy).not.toHaveBeenCalled();
  });

  it('preserves structured validation errors on a 400', () => {
    let body: unknown;
    http.post(`${environment.apiBaseUrl}/batches`, {}).subscribe({
      error: (e) => (body = e.error),
    });

    httpMock.expectOne(`${environment.apiBaseUrl}/batches`).flush(
      {
        title: 'Bad Request',
        status: 400,
        detail: 'Batch validation failed:\nPayment row 2: ...',
        errors: [
          {
            source: 'Payment',
            rowNumber: 2,
            field: 'payment_record_id',
            message: 'Required value is missing.',
          },
        ],
      },
      { status: 400, statusText: 'Bad Request' },
    );

    const errors = (body as { errors: unknown[] }).errors;
    expect(errors.length).toBe(1);
  });
});
