import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { AuthApi } from './auth-api.service';
import { environment } from '../../../environments/environment';
import { isProblemDetails } from '../models/problem-details.model';
import type { LoginResponse } from '../models/auth.model';

describe('AuthApi', () => {
  let api: AuthApi;
  let httpMock: HttpTestingController;

  const loginUrl = `${environment.apiBaseUrl}/auth/login`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    api = TestBed.inject(AuthApi);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('POSTs to the configured login URL', () => {
    api.login({ email: 'a@b.test', password: 'pw' }).subscribe();

    const req = httpMock.expectOne(loginUrl);
    expect(req.request.method).toBe('POST');
    req.flush({});
  });

  it('sends exactly the backend LoginRequest shape', () => {
    api.login({ email: 'operator@finsight.test', password: 's3cret' }).subscribe();

    const req = httpMock.expectOne(loginUrl);
    expect(req.request.body).toEqual({
      email: 'operator@finsight.test',
      password: 's3cret',
    });
    req.flush({});
  });

  it('maps the backend LoginResponse verbatim', () => {
    const wire: LoginResponse = {
      accessToken: 'jwt-token',
      tokenType: 'Bearer',
      expiresAtUtc: '2026-08-28T12:00:00Z',
      userId: '44444444-4444-4444-4444-444444444444',
      email: 'operator@finsight.test',
      role: 'User',
    };

    let received: LoginResponse | undefined;
    api.login({ email: 'x', password: 'y' }).subscribe((r) => (received = r));

    httpMock.expectOne(loginUrl).flush(wire);

    expect(received).toEqual(wire);
  });

  it('surfaces a 401 ProblemDetails to the caller', () => {
    let status: number | undefined;
    let body: unknown;

    api.login({ email: 'wrong@finsight.test', password: 'nope' }).subscribe({
      next: () => fail('expected the 401 to error'),
      error: (err) => {
        status = err.status;
        body = err.error;
      },
    });

    httpMock.expectOne(loginUrl).flush(
      {
        type: 'https://tools.ietf.org/html/rfc7231#section-6.5.2',
        title: 'Unauthorized',
        status: 401,
        detail: 'Invalid email or password.',
      },
      { status: 401, statusText: 'Unauthorized' },
    );

    expect(status).toBe(401);
    expect(isProblemDetails(body)).toBeTrue();
    expect((body as { detail: string }).detail).toBe('Invalid email or password.');
  });
});
