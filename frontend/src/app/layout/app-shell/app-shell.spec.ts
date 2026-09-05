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

  it('opens a confirmation dialog when logout is clicked, and cancels without logging out', () => {
    const logout = Array.from(
      el().querySelectorAll<HTMLButtonElement>('button'),
    ).find((b) => b.textContent?.includes('Log out'));

    expect(logout).toBeTruthy();
    expect(store.isAuthenticated()).toBeTrue();

    logout!.click();
    fixture.detectChanges();

    // Dialog is open
    const dialog = el().querySelector('[role="dialog"]');
    expect(dialog).toBeTruthy();
    expect(dialog!.textContent).toContain('Sign out of FinSight?');
    expect(store.isAuthenticated()).toBeTrue();

    // Click Cancel
    const cancelBtn = Array.from(
      dialog!.querySelectorAll<HTMLButtonElement>('button'),
    ).find((b) => b.textContent?.includes('Cancel'));
    expect(cancelBtn).toBeTruthy();

    cancelBtn!.click();
    fixture.detectChanges();

    expect(el().querySelector('[role="dialog"]')).toBeNull();
    expect(store.isAuthenticated()).toBeTrue();
    expect(navigateByUrl).not.toHaveBeenCalled();
  });

  it('confirms logout from the dialog and clears the session', () => {
    const logout = Array.from(
      el().querySelectorAll<HTMLButtonElement>('button'),
    ).find((b) => b.textContent?.includes('Log out'));

    logout!.click();
    fixture.detectChanges();

    const confirmBtn = el().querySelector<HTMLButtonElement>(
      '[data-testid="confirm-logout-button"]',
    );
    expect(confirmBtn).toBeTruthy();

    confirmBtn!.click();
    fixture.detectChanges();

    expect(store.isAuthenticated()).toBeFalse();
    expect(navigateByUrl).toHaveBeenCalledWith('/login');
  });

  it('closes the confirmation dialog when Escape is pressed', () => {
    const logout = Array.from(
      el().querySelectorAll<HTMLButtonElement>('button'),
    ).find((b) => b.textContent?.includes('Log out'));

    logout!.click();
    fixture.detectChanges();

    expect(el().querySelector('[role="dialog"]')).toBeTruthy();

    const dialog = el().querySelector<HTMLElement>('[role="dialog"]')!;
    dialog.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();

    expect(el().querySelector('[role="dialog"]')).toBeNull();
    expect(store.isAuthenticated()).toBeTrue();
  });

  it('provides a skip-to-content link and a main landmark', () => {
    expect(el().querySelector('a[href="#main-content"]')).toBeTruthy();
    expect(el().querySelector('main#main-content')).toBeTruthy();
  });

  // ---------------------------------------------------------------------
  // Desktop sidebar collapse/expand
  // ---------------------------------------------------------------------

  function sidebarToggle(): HTMLButtonElement {
    return el().querySelector<HTMLButtonElement>('[data-testid="sidebar-toggle"]')!;
  }

  it('defaults to expanded, with a discoverable "Collapse sidebar" control', () => {
    const toggle = sidebarToggle();

    expect(toggle).toBeTruthy();
    expect(toggle.getAttribute('aria-label')).toBe('Collapse sidebar');
    expect(toggle.getAttribute('aria-expanded')).toBe('true');
    expect(el().querySelector('[data-testid="app-sidebar"]')!.className).toContain('w-60');
  });

  it('collapses the sidebar on click, and the toggle becomes "Expand sidebar"', () => {
    sidebarToggle().click();
    fixture.detectChanges();

    const toggle = sidebarToggle();
    expect(toggle.getAttribute('aria-label')).toBe('Expand sidebar');
    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    expect(el().querySelector('[data-testid="app-sidebar"]')!.className).toContain('w-[68px]');
  });

  it('expands the sidebar again on a second click, returning to its original width', () => {
    sidebarToggle().click();
    fixture.detectChanges();
    sidebarToggle().click();
    fixture.detectChanges();

    const toggle = sidebarToggle();
    expect(toggle.getAttribute('aria-label')).toBe('Collapse sidebar');
    expect(toggle.getAttribute('aria-expanded')).toBe('true');
    expect(el().querySelector('[data-testid="app-sidebar"]')!.className).toContain('w-60');
  });

  it('keeps every nav item and the logout control with a real accessible name while collapsed', () => {
    sidebarToggle().click();
    fixture.detectChanges();

    const batchesLink = Array.from(
      el().querySelectorAll<HTMLAnchorElement>('[data-testid="app-sidebar"] nav a'),
    ).find((a) => a.getAttribute('aria-label') === 'Batches');
    expect(batchesLink).toBeTruthy();
    expect(batchesLink!.getAttribute('title')).toBe('Batches');

    const logoutButton = el().querySelector<HTMLButtonElement>(
      '[data-testid="sidebar-logout-button"]',
    )!;
    expect(logoutButton.getAttribute('aria-label')).toBe('Log out');
  });

  it('still opens the existing logout confirmation dialog when collapsed, and Confirm still logs out', () => {
    sidebarToggle().click();
    fixture.detectChanges();

    el().querySelector<HTMLButtonElement>('[data-testid="sidebar-logout-button"]')!.click();
    fixture.detectChanges();

    const dialog = el().querySelector('[role="dialog"]');
    expect(dialog).toBeTruthy();
    expect(dialog!.textContent).toContain('Sign out of FinSight?');

    el().querySelector<HTMLButtonElement>('[data-testid="confirm-logout-button"]')!.click();
    fixture.detectChanges();

    expect(store.isAuthenticated()).toBeFalse();
    expect(navigateByUrl).toHaveBeenCalledWith('/login');
  });

  it('shows a close (X) control while expanded, with the brand as branding only', () => {
    const toggle = sidebarToggle();

    expect(toggle.querySelector('[data-testid="sidebar-toggle-icon-close"]')).toBeTruthy();
    expect(toggle.querySelector('[data-testid="sidebar-toggle-icon-menu"]')).toBeNull();

    // The brand lockup is a sibling, never the toggle target itself.
    expect(toggle.textContent).not.toContain('FinSight');

    const brand = el().querySelector<HTMLAnchorElement>(
      '[data-testid="app-sidebar"] a[aria-label="FinSight AI Home"]',
    );
    expect(brand).toBeTruthy();
    expect(brand!.contains(toggle)).toBeFalse();
  });

  it('keeps a visible hamburger control in the collapsed rail so the sidebar can be reopened', () => {
    sidebarToggle().click();
    fixture.detectChanges();

    const toggle = sidebarToggle();

    // Still rendered, still reachable -- the rail never becomes a dead end.
    expect(toggle).toBeTruthy();
    expect(toggle.querySelector('[data-testid="sidebar-toggle-icon-menu"]')).toBeTruthy();
    expect(toggle.querySelector('[data-testid="sidebar-toggle-icon-close"]')).toBeNull();
    expect(toggle.hasAttribute('hidden')).toBeFalse();
    expect(toggle.className).not.toContain('hidden');

    // Clicking it restores the expanded sidebar.
    toggle.click();
    fixture.detectChanges();

    expect(sidebarToggle().getAttribute('aria-expanded')).toBe('true');
    expect(
      sidebarToggle().querySelector('[data-testid="sidebar-toggle-icon-close"]'),
    ).toBeTruthy();
  });

  it('hides the brand lockup in the collapsed rail', () => {
    sidebarToggle().click();
    fixture.detectChanges();

    expect(
      el().querySelector('[data-testid="app-sidebar"] a[aria-label="FinSight AI Home"]'),
    ).toBeNull();
  });

  it('exposes exactly one sidebar toggle control, with no separate chevron button', () => {
    const sidebar = el().querySelector('[data-testid="app-sidebar"]')!;

    expect(sidebar.querySelectorAll('[data-testid="sidebar-toggle"]').length).toBe(1);

    // Every remaining button in the sidebar is the logout control; no second
    // collapse/expand affordance survives anywhere in the rail.
    const collapseControls = Array.from(
      sidebar.querySelectorAll<HTMLButtonElement>('button'),
    ).filter((b) => /collapse|expand/i.test(b.getAttribute('aria-label') ?? ''));

    expect(collapseControls.length).toBe(1);
    expect(collapseControls[0]).toBe(sidebarToggle());
  });

  it('toggles without navigating -- the brand toggle is not a router link', () => {
    const toggle = sidebarToggle();

    expect(toggle.tagName).toBe('BUTTON');
    expect(toggle.getAttribute('href')).toBeNull();

    toggle.click();
    fixture.detectChanges();

    expect(navigateByUrl).not.toHaveBeenCalled();
  });

  it('contains no challenge-track or phase language', () => {
    const text = el().textContent!.toLowerCase();

    expect(text).not.toContain('track 04');
    expect(text).not.toContain('buildathon');
    expect(text).not.toContain('phase');
  });
});
