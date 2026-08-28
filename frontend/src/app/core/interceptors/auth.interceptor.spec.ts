import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { authInterceptor } from './auth.interceptor';
import { AuthStore } from '../state/auth-store';
import { environment } from '../../../environments/environment';
import type { LoginResponse } from '../models/auth.model';

function validLogin(): LoginResponse {
  return {
    accessToken: 'token-abc',
    tokenType: 'Bearer',
    expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString(),
    userId: '55555555-5555-5555-5555-555555555555',
    email: 'operator@finsight.test',
    role: 'User',
  };
}

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let store: AuthStore;

  beforeEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    store = TestBed.inject(AuthStore);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('attaches the bearer token when a session exists', () => {
    store.setSession(validLogin());

    http.get(`${environment.apiBaseUrl}/batches`).subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/batches`);
    expect(req.request.headers.get('Authorization')).toBe('Bearer token-abc');
    req.flush({});
  });

  it('does not attach an Authorization header to the anonymous login request', () => {
    // No session yet -- this is what a real login call looks like.
    http.post(`${environment.apiBaseUrl}/auth/login`, {}).subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/auth/login`);
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({});
  });

  it('stops attaching the header once the session is cleared', () => {
    store.setSession(validLogin());
    store.clearSession();

    http.get(`${environment.apiBaseUrl}/batches`).subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/batches`);
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({});
  });
});
