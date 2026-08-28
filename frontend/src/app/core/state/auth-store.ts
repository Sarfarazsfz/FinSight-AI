import { Injectable, computed, signal } from '@angular/core';
import type { AuthSession, LoginResponse } from '../models/auth.model';

const STORAGE_KEY = 'finsight.session';

/**
 * Session state for FinSight.
 *
 * Signals plus one injectable, deliberately not a state-management library:
 * the application has exactly one piece of genuinely global state, and the
 * rest is server-owned data fetched per screen.
 *
 * The backend issues an access token only -- there is no refresh endpoint,
 * no registration endpoint and no server-side logout -- so this store never
 * attempts silent renewal. `expiresAtUtc` is used for UX only; a token the
 * client believes is valid can still be rejected, and the resulting 401 is
 * authoritative.
 */
@Injectable({ providedIn: 'root' })
export class AuthStore {
  private readonly sessionSignal = signal<AuthSession | null>(
    AuthStore.readStoredSession(),
  );

  readonly session = this.sessionSignal.asReadonly();

  readonly isAuthenticated = computed(() => this.sessionSignal() !== null);

  readonly userEmail = computed(() => this.sessionSignal()?.email ?? null);

  get accessToken(): string | null {
    return this.sessionSignal()?.accessToken ?? null;
  }

  /** Persists the session from a successful login response. */
  setSession(response: LoginResponse): void {
    const session: AuthSession = {
      accessToken: response.accessToken,
      expiresAtUtc: response.expiresAtUtc,
      userId: response.userId,
      email: response.email,
      role: response.role,
    };

    this.sessionSignal.set(session);

    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    } catch {
      // Storage unavailable (private mode, blocked site data, quota).
      // Non-fatal: the session still works for this tab via the signal.
    }
  }

  /**
   * Clears the session locally. There is no server logout endpoint, so this
   * is the whole of logging out.
   */
  clearSession(): void {
    this.sessionSignal.set(null);

    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // Non-fatal -- the in-memory signal is already cleared.
    }
  }

  /**
   * Reads and validates a stored session.
   *
   * Returns null for anything that is not a usable session: absent, blocked
   * storage, corrupt JSON, wrong shape, or already expired. A restore must
   * never throw -- a broken stored value means signed out, not a crash.
   */
  private static readStoredSession(): AuthSession | null {
    let raw: string | null;

    try {
      raw = localStorage.getItem(STORAGE_KEY);
    } catch {
      return null;
    }

    if (!raw) {
      return null;
    }

    let parsed: unknown;

    try {
      parsed = JSON.parse(raw);
    } catch {
      return null;
    }

    if (!AuthStore.isAuthSession(parsed)) {
      return null;
    }

    if (AuthStore.isExpired(parsed.expiresAtUtc)) {
      return null;
    }

    return parsed;
  }

  private static isAuthSession(value: unknown): value is AuthSession {
    if (typeof value !== 'object' || value === null) {
      return false;
    }

    const candidate = value as Partial<AuthSession>;

    return (
      typeof candidate.accessToken === 'string' &&
      candidate.accessToken.length > 0 &&
      typeof candidate.expiresAtUtc === 'string' &&
      typeof candidate.userId === 'string' &&
      typeof candidate.email === 'string' &&
      typeof candidate.role === 'string'
    );
  }

  /**
   * UX-only expiry check. An unparseable timestamp is treated as expired,
   * because a session we cannot reason about should not be trusted.
   */
  private static isExpired(expiresAtUtc: string): boolean {
    const expiry = Date.parse(expiresAtUtc);

    if (Number.isNaN(expiry)) {
      return true;
    }

    return expiry <= Date.now();
  }
}
