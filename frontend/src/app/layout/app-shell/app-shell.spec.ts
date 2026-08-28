import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AppShell } from './app-shell';
import { AuthStore } from '../../core/state/auth-store';
import type { LoginResponse } from '../../core/models/auth.model';

function session(): LoginResponse {
  return {
    accessToken: 'jwt-token',
    tokenType: 'Bearer',
    expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString(),
    userId: '99999999-9999-9999-9999-999999999999',
    email: 'operator@finsight.test',
    role: 'User',
  };
}

describe('AppShell', () => {
  let fixture: ComponentFixture<AppShell>;
  let store: AuthStore;
  let navigateByUrl: jasmine.Spy;

  beforeEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [AppShell],
      // A real Router is required: routerLink / routerLinkActive resolve
      // against the router's URL tree. Spy on navigateByUrl rather than
      // replacing the whole service.
      providers: [provideRouter([])],
    });

    store = TestBed.inject(AuthStore);
    store.setSession(session());

    navigateByUrl = spyOn(TestBed.inject(Router), 'navigateByUrl').and.resolveTo(
      true,
    );

    fixture = TestBed.createComponent(AppShell);
    fixture.detectChanges();
  });

  afterEach(() => localStorage.clear());

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the real session email, never a placeholder', () => {
    expect(el().textContent).toContain('operator@finsight.test');
  });

  it('renders only navigation destinations that exist', () => {
    const text = el().textContent!.toLowerCase();

    expect(text).toContain('batches');

    // No dead items for unbuilt or unsupported capabilities.
    expect(text).not.toContain('analytics');
    expect(text).not.toContain('admin');
    expect(text).not.toContain('audit');
    expect(text).not.toContain('settings');
    expect(text).not.toContain('reports');
  });

  it('exposes logout as a real button and clears the session', () => {
    const logout = Array.from(
      el().querySelectorAll<HTMLButtonElement>('button'),
    ).find((b) => b.textContent?.includes('Log out'));

    expect(logout).toBeTruthy();
    expect(store.isAuthenticated()).toBeTrue();

    logout!.click();
    fixture.detectChanges();

    expect(store.isAuthenticated()).toBeFalse();
    expect(navigateByUrl).toHaveBeenCalledWith('/login');
  });

  it('provides a skip-to-content link and a main landmark', () => {
    expect(el().querySelector('a[href="#main-content"]')).toBeTruthy();
    expect(el().querySelector('main#main-content')).toBeTruthy();
  });

  it('contains no challenge-track or phase language', () => {
    const text = el().textContent!.toLowerCase();

    expect(text).not.toContain('track 04');
    expect(text).not.toContain('buildathon');
    expect(text).not.toContain('phase');
  });
});
