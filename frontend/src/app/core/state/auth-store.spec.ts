import { TestBed } from '@angular/core/testing';
import { AuthStore } from './auth-store';
import type { LoginResponse } from '../models/auth.model';

const STORAGE_KEY = 'finsight.session';

function futureIso(minutes = 60): string {
  return new Date(Date.now() + minutes * 60_000).toISOString();
}

function pastIso(minutes = 60): string {
  return new Date(Date.now() - minutes * 60_000).toISOString();
}

function loginResponse(expiresAtUtc: string): LoginResponse {
  return {
    accessToken: 'token-abc',
    tokenType: 'Bearer',
    expiresAtUtc,
    userId: '11111111-1111-1111-1111-111111111111',
    email: 'operator@finsight.test',
    role: 'User',
  };
}

describe('AuthStore', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
  });

  afterEach(() => localStorage.clear());

  it('starts signed out when nothing is stored', () => {
    const store = TestBed.inject(AuthStore);

    expect(store.isAuthenticated()).toBeFalse();
    expect(store.session()).toBeNull();
    expect(store.accessToken).toBeNull();
    expect(store.userEmail()).toBeNull();
  });

  it('persists a session on login and exposes it through signals', () => {
    const store = TestBed.inject(AuthStore);
    store.setSession(loginResponse(futureIso()));

    expect(store.isAuthenticated()).toBeTrue();
    expect(store.accessToken).toBe('token-abc');
    expect(store.userEmail()).toBe('operator@finsight.test');
    expect(store.session()?.role).toBe('User');

    const stored = JSON.parse(localStorage.getItem(STORAGE_KEY)!);
    expect(stored.accessToken).toBe('token-abc');
    expect(stored.email).toBe('operator@finsight.test');
    // tokenType is deliberately not persisted.
    expect(stored.tokenType).toBeUndefined();
  });

  it('restores a valid session from storage', () => {
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({
        accessToken: 'restored-token',
        expiresAtUtc: futureIso(),
        userId: '22222222-2222-2222-2222-222222222222',
        email: 'restored@finsight.test',
        role: 'User',
      }),
    );

    const store = TestBed.inject(AuthStore);

    expect(store.isAuthenticated()).toBeTrue();
    expect(store.accessToken).toBe('restored-token');
  });

  it('treats an expired stored session as signed out', () => {
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({
        accessToken: 'stale-token',
        expiresAtUtc: pastIso(),
        userId: '33333333-3333-3333-3333-333333333333',
        email: 'stale@finsight.test',
        role: 'User',
      }),
    );

    const store = TestBed.inject(AuthStore);

    expect(store.isAuthenticated()).toBeFalse();
    expect(store.accessToken).toBeNull();
  });

  it('survives corrupt stored JSON without throwing', () => {
    localStorage.setItem(STORAGE_KEY, '{ not valid json');

    expect(() => TestBed.inject(AuthStore)).not.toThrow();
    expect(TestBed.inject(AuthStore).isAuthenticated()).toBeFalse();
  });

  it('rejects a stored value of the wrong shape', () => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ accessToken: 123 }));

    expect(TestBed.inject(AuthStore).isAuthenticated()).toBeFalse();
  });

  it('treats an unparseable expiry as expired', () => {
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({
        accessToken: 'token',
        expiresAtUtc: 'not-a-date',
        userId: 'id',
        email: 'e@x.test',
        role: 'User',
      }),
    );

    expect(TestBed.inject(AuthStore).isAuthenticated()).toBeFalse();
  });

  it('clears the session and storage on logout', () => {
    const store = TestBed.inject(AuthStore);
    store.setSession(loginResponse(futureIso()));
    expect(store.isAuthenticated()).toBeTrue();

    store.clearSession();

    expect(store.isAuthenticated()).toBeFalse();
    expect(store.accessToken).toBeNull();
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
  });

  it('still works in-memory when storage writes fail', () => {
    const store = TestBed.inject(AuthStore);
    spyOn(localStorage, 'setItem').and.throwError('QuotaExceededError');

    expect(() => store.setSession(loginResponse(futureIso()))).not.toThrow();
    expect(store.isAuthenticated()).toBeTrue();
    expect(store.accessToken).toBe('token-abc');
  });

  it('starts signed out when storage reads fail', () => {
    spyOn(localStorage, 'getItem').and.throwError('SecurityError');

    expect(() => TestBed.inject(AuthStore)).not.toThrow();
    expect(TestBed.inject(AuthStore).isAuthenticated()).toBeFalse();
  });
});
