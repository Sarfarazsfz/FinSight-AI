import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { LoginPage } from './login-page';
import { AuthStore } from '../../core/state/auth-store';
import { environment } from '../../../environments/environment';
import type { LoginResponse } from '../../core/models/auth.model';

@Component({ template: '', selector: 'app-dummy' })
class DummyComponent {}

const LOGIN_URL = `${environment.apiBaseUrl}/auth/login`;

function successResponse(): LoginResponse {
  return {
    accessToken: 'jwt-token',
    tokenType: 'Bearer',
    expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString(),
    userId: '88888888-8888-8888-8888-888888888888',
    email: 'operator@finsight.test',
    role: 'User',
  };
}

describe('LoginPage', () => {
  let fixture: ComponentFixture<LoginPage>;
  let httpMock: HttpTestingController;
  let store: AuthStore;
  let navigateByUrl: jasmine.Spy;

  function configure(
    returnUrl: string | null = null,
    seedSession = false,
  ): void {
    localStorage.clear();

    if (seedSession) {
      localStorage.setItem(
        'finsight.session',
        JSON.stringify({
          accessToken: 'existing',
          expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString(),
          userId: 'id',
          email: 'operator@finsight.test',
          role: 'User',
        }),
      );
    }
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [LoginPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: '', component: DummyComponent },
          { path: 'login', component: DummyComponent },
          { path: 'batches', component: DummyComponent },
        ]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: convertToParamMap(
                returnUrl ? { returnUrl } : {},
              ),
            },
          },
        },
      ],
    });

    httpMock = TestBed.inject(HttpTestingController);
    store = TestBed.inject(AuthStore);
    const router = TestBed.inject(Router);
    navigateByUrl = spyOn(router, 'navigateByUrl').and.resolveTo(true);
    fixture = TestBed.createComponent(LoginPage);
    fixture.detectChanges();
  }

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function setCredentials(email: string, password: string): void {
    const emailInput = el().querySelector<HTMLInputElement>('#email')!;
    const passwordInput = el().querySelector<HTMLInputElement>('#password')!;

    emailInput.value = email;
    emailInput.dispatchEvent(new Event('input'));
    passwordInput.value = password;
    passwordInput.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function submit(): void {
    el().querySelector<HTMLFormElement>('form')!.dispatchEvent(
      new Event('submit', { bubbles: true, cancelable: true }),
    );
    fixture.detectChanges();
  }

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('renders email, password and submit, and nothing that does not exist', () => {
    configure();

    expect(el().querySelector('#email')).toBeTruthy();
    expect(el().querySelector('#password')).toBeTruthy();
    expect(el().querySelector('button[type="submit"]')).toBeTruthy();

    // Signup and password reset are now real AuthController actions, so
    // links to them are legitimate. What must still be absent is any
    // capability the backend genuinely lacks.
    const text = el().textContent!.toLowerCase();
    expect(text).not.toContain('google');
    expect(text).not.toContain('single sign-on');
    expect(text).not.toContain('track 04');
  });

  it('links to the real signup and password-reset routes', () => {
    configure();

    const signup = el().querySelector<HTMLAnchorElement>(
      '[data-testid="login-create-account"]',
    );
    const forgot = el().querySelector<HTMLAnchorElement>(
      '[data-testid="login-forgot-password"]',
    );

    expect(signup).toBeTruthy();
    expect(signup!.getAttribute('href')).toBe('/signup');

    expect(forgot).toBeTruthy();
    expect(forgot!.getAttribute('href')).toBe('/forgot-password');
  });

  it('blocks submission and shows field errors when empty', () => {
    configure();
    submit();

    httpMock.expectNone(LOGIN_URL);
    expect(el().querySelector('#email-error')).toBeTruthy();
    expect(el().querySelector('#password-error')).toBeTruthy();
  });

  it('blocks submission when the email format is invalid', () => {
    configure();
    setCredentials('not-an-email', 'pw');
    submit();

    httpMock.expectNone(LOGIN_URL);
    expect(el().querySelector('#email-error')).toBeTruthy();
  });

  it('disables the button while the request is in flight', () => {
    configure();
    setCredentials('operator@finsight.test', 'pw');
    submit();

    const button = el().querySelector<HTMLButtonElement>('button[type="submit"]')!;
    expect(button.disabled).toBeTrue();
    expect(button.textContent).toContain('Signing in');

    httpMock.expectOne(LOGIN_URL).flush(successResponse());
  });

  it('renders the backend ProblemDetails detail on a real 401', () => {
    configure();
    setCredentials('wrong@finsight.test', 'nope');
    submit();

    httpMock.expectOne(LOGIN_URL).flush(
      {
        type: 'https://tools.ietf.org/html/rfc9110#section-15.5.2',
        title: 'Unauthorized',
        status: 401,
        detail: 'Invalid email or password.',
      },
      { status: 401, statusText: 'Unauthorized' },
    );
    fixture.detectChanges();

    const error = el().querySelector('[data-testid="auth-error"]');
    expect(error?.textContent).toContain('Invalid email or password.');
    expect(store.isAuthenticated()).toBeFalse();
    expect(navigateByUrl).not.toHaveBeenCalled();
  });

  it('renders the detail on a 400', () => {
    configure();
    setCredentials('a@b.test', 'pw');
    submit();

    httpMock.expectOne(LOGIN_URL).flush(
      { title: 'Bad Request', status: 400, detail: 'Password is required.' },
      { status: 400, statusText: 'Bad Request' },
    );
    fixture.detectChanges();

    expect(
      el().querySelector('[data-testid="auth-error"]')?.textContent,
    ).toContain('Password is required.');
  });

  it('reports an unreachable server distinctly', () => {
    configure();
    setCredentials('a@b.test', 'pw');
    submit();

    httpMock
      .expectOne(LOGIN_URL)
      .error(new ProgressEvent('error'), { status: 0, statusText: 'Unknown Error' });
    fixture.detectChanges();

    expect(
      el().querySelector('[data-testid="auth-error"]')?.textContent,
    ).toContain('Cannot reach the server');
  });

  it('stores the session and navigates to /batches on success', () => {
    configure();
    setCredentials('operator@finsight.test', 'pw');
    submit();

    httpMock.expectOne(LOGIN_URL).flush(successResponse());
    fixture.detectChanges();

    expect(store.isAuthenticated()).toBeTrue();
    expect(store.accessToken).toBe('jwt-token');
    expect(navigateByUrl).toHaveBeenCalledWith('/batches');
  });

  it('honours a safe returnUrl', () => {
    configure('/runs/abc/exceptions');
    setCredentials('operator@finsight.test', 'pw');
    submit();

    httpMock.expectOne(LOGIN_URL).flush(successResponse());

    expect(navigateByUrl).toHaveBeenCalledWith('/runs/abc/exceptions');
  });

  it('rejects an external returnUrl and falls back to /batches', () => {
    configure('https://evil.test');
    setCredentials('operator@finsight.test', 'pw');
    submit();

    httpMock.expectOne(LOGIN_URL).flush(successResponse());

    expect(navigateByUrl).toHaveBeenCalledWith('/batches');
  });

  it('redirects an already-authenticated visitor away from the login page', () => {
    configure(null, true);

    expect(store.isAuthenticated()).toBeTrue();
    expect(navigateByUrl).toHaveBeenCalledWith('/batches');
  });

  describe('password visibility toggle', () => {
    function toggleButton(): HTMLButtonElement {
      return el().querySelector<HTMLButtonElement>(
        '[data-testid="toggle-password-visibility"]',
      )!;
    }

    function passwordInput(): HTMLInputElement {
      return el().querySelector<HTMLInputElement>('#password')!;
    }

    it('starts masked', () => {
      configure();
      expect(passwordInput().type).toBe('password');
    });

    it('renders an eye control with an accessible label', () => {
      configure();
      const button = toggleButton();

      expect(button).toBeTruthy();
      expect(button.getAttribute('type')).toBe('button');
      expect(button.getAttribute('aria-label')).toBe('Show password');
      expect(button.getAttribute('aria-pressed')).toBe('false');
    });

    it('reveals the entered password on click, without changing its value', () => {
      configure();
      setCredentials('operator@finsight.test', 'super-secret-pw');

      toggleButton().click();
      fixture.detectChanges();

      expect(passwordInput().type).toBe('text');
      expect(passwordInput().value).toBe('super-secret-pw');
    });

    it('hides the password again on a second click, value still unchanged', () => {
      configure();
      setCredentials('operator@finsight.test', 'super-secret-pw');

      toggleButton().click();
      fixture.detectChanges();
      toggleButton().click();
      fixture.detectChanges();

      expect(passwordInput().type).toBe('password');
      expect(passwordInput().value).toBe('super-secret-pw');
    });

    it('updates the accessible label and pressed state when toggled', () => {
      configure();

      toggleButton().click();
      fixture.detectChanges();

      const button = toggleButton();
      expect(button.getAttribute('aria-label')).toBe('Hide password');
      expect(button.getAttribute('aria-pressed')).toBe('true');
    });

    it('does not reset the form or clear other fields when toggled', () => {
      configure();
      setCredentials('operator@finsight.test', 'super-secret-pw');

      toggleButton().click();
      fixture.detectChanges();

      expect(el().querySelector<HTMLInputElement>('#email')!.value).toBe(
        'operator@finsight.test',
      );
    });

    it('is keyboard activatable (a real <button>, not a div/span)', () => {
      configure();
      expect(toggleButton().tagName).toBe('BUTTON');
    });

    it('does not affect password validation behavior', () => {
      configure();
      submit();

      toggleButton().click();
      fixture.detectChanges();

      expect(el().querySelector('#password-error')).toBeTruthy();
      httpMock.expectNone(LOGIN_URL);
    });
  });
});
