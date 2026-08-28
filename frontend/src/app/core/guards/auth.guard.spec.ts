import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { provideRouter } from '@angular/router';
import type {
  ActivatedRouteSnapshot,
  RouterStateSnapshot,
} from '@angular/router';
import { authGuard } from './auth.guard';
import { AuthStore } from '../state/auth-store';
import type { LoginResponse } from '../models/auth.model';

function validLogin(): LoginResponse {
  return {
    accessToken: 'token-abc',
    tokenType: 'Bearer',
    expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString(),
    userId: '77777777-7777-7777-7777-777777777777',
    email: 'operator@finsight.test',
    role: 'User',
  };
}

describe('authGuard', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
  });

  afterEach(() => localStorage.clear());

  function run(url: string) {
    const state = { url } as RouterStateSnapshot;
    const route = {} as ActivatedRouteSnapshot;

    return TestBed.runInInjectionContext(() => authGuard(route, state));
  }

  it('allows navigation when authenticated', () => {
    TestBed.inject(AuthStore).setSession(validLogin());

    expect(run('/batches')).toBeTrue();
  });

  it('blocks navigation and redirects to /login when unauthenticated', () => {
    const result = run('/batches');

    expect(result instanceof UrlTree).toBeTrue();

    const tree = result as UrlTree;
    expect(TestBed.inject(Router).serializeUrl(tree)).toBe(
      '/login?returnUrl=%2Fbatches',
    );
  });

  it('preserves a nested returnUrl', () => {
    const tree = run('/runs/abc/exceptions') as UrlTree;

    expect(TestBed.inject(Router).serializeUrl(tree)).toContain(
      'returnUrl=%2Fruns%2Fabc%2Fexceptions',
    );
  });

  it('blocks navigation when the stored session is expired', () => {
    localStorage.setItem(
      'finsight.session',
      JSON.stringify({
        accessToken: 'stale',
        expiresAtUtc: new Date(Date.now() - 3_600_000).toISOString(),
        userId: 'id',
        email: 'e@x.test',
        role: 'User',
      }),
    );

    expect(run('/batches') instanceof UrlTree).toBeTrue();
  });
});
