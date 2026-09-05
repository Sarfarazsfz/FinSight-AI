import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import {
  ActivatedRoute,
  Router,
  convertToParamMap,
  provideRouter,
} from '@angular/router';
import { SignupPage } from './signup-page';
import { ForgotPasswordPage } from './forgot-password-page';
import { ResetPasswordPage } from './reset-password-page';
import { environment } from '../../../environments/environment';

const REGISTER_URL = `${environment.apiBaseUrl}/auth/register`;
const FORGOT_URL = `${environment.apiBaseUrl}/auth/forgot-password`;
const RESET_URL = `${environment.apiBaseUrl}/auth/reset-password`;

const VALID_PASSWORD = 'test-only-password-value';

function setInput(
  fixture: ComponentFixture<unknown>,
  testId: string,
  value: string,
): void {
  const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>(
    `[data-testid="${testId}"]`,
  )!;

  input.value = value;
  input.dispatchEvent(new Event('input'));
  input.dispatchEvent(new Event('blur'));
  fixture.detectChanges();
}

function submitForm(fixture: ComponentFixture<unknown>): void {
  (fixture.nativeElement as HTMLElement)
    .querySelector('form')!
    .dispatchEvent(new Event('submit'));
  fixture.detectChanges();
}

// ============================================================== SIGN UP

describe('SignupPage', () => {
  let fixture: ComponentFixture<SignupPage>;
  let httpMock: HttpTestingController;
  let navigate: jasmine.Spy;

  beforeEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [SignupPage],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    httpMock = TestBed.inject(HttpTestingController);
    navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    fixture = TestBed.createComponent(SignupPage);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  it('posts email and both password fields, and never a role', () => {
    setInput(fixture, 'signup-email', 'person@example.com');
    setInput(fixture, 'signup-password', VALID_PASSWORD);
    setInput(fixture, 'signup-confirm', VALID_PASSWORD);
    submitForm(fixture);

    const request = httpMock.expectOne(REGISTER_URL);

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      email: 'person@example.com',
      password: VALID_PASSWORD,
      confirmPassword: VALID_PASSWORD,
    });

    // A public caller must not be able to ask for elevated privileges.
    expect(Object.keys(request.request.body)).not.toContain('role');

    request.flush({
      userId: 'id',
      email: 'person@example.com',
      role: 'User',
    });
  });

  it('sends the user to login after signup rather than creating a session', () => {
    setInput(fixture, 'signup-email', 'person@example.com');
    setInput(fixture, 'signup-password', VALID_PASSWORD);
    setInput(fixture, 'signup-confirm', VALID_PASSWORD);
    submitForm(fixture);

    httpMock.expectOne(REGISTER_URL).flush({
      userId: 'id',
      email: 'person@example.com',
      role: 'User',
    });
    fixture.detectChanges();

    expect(navigate).toHaveBeenCalledWith(['/login'], {
      queryParams: { created: '1' },
    });

    // No token was stored -- signup does not authenticate.
    expect(localStorage.getItem('finsight.session')).toBeNull();
  });

  it('blocks submission when the passwords do not match', () => {
    setInput(fixture, 'signup-email', 'person@example.com');
    setInput(fixture, 'signup-password', VALID_PASSWORD);
    setInput(fixture, 'signup-confirm', 'test-only-different-value');
    submitForm(fixture);

    httpMock.expectNone(REGISTER_URL);
    expect(el().querySelector('#signup-confirm-error')).toBeTruthy();
  });

  it('blocks submission when the password is below the minimum length', () => {
    setInput(fixture, 'signup-email', 'person@example.com');
    setInput(fixture, 'signup-password', 'short');
    setInput(fixture, 'signup-confirm', 'short');
    submitForm(fixture);

    httpMock.expectNone(REGISTER_URL);
    expect(el().querySelector('#signup-password-error')).toBeTruthy();
  });

  it('blocks submission when the email is malformed', () => {
    setInput(fixture, 'signup-email', 'not-an-email');
    setInput(fixture, 'signup-password', VALID_PASSWORD);
    setInput(fixture, 'signup-confirm', VALID_PASSWORD);
    submitForm(fixture);

    httpMock.expectNone(REGISTER_URL);
    expect(el().querySelector('#signup-email-error')).toBeTruthy();
  });

  it('surfaces a duplicate-email conflict from the server', () => {
    setInput(fixture, 'signup-email', 'taken@example.com');
    setInput(fixture, 'signup-password', VALID_PASSWORD);
    setInput(fixture, 'signup-confirm', VALID_PASSWORD);
    submitForm(fixture);

    httpMock.expectOne(REGISTER_URL).flush(
      {
        title: 'Conflict',
        status: 409,
        detail: 'An account with that email already exists.',
      },
      { status: 409, statusText: 'Conflict' },
    );
    fixture.detectChanges();

    const error = el().querySelector('[data-testid="signup-error"]');

    expect(error).toBeTruthy();
    expect(error!.textContent).toContain('already exists');
    expect(navigate).not.toHaveBeenCalled();
  });

  it('uses new-password autocomplete on both password fields', () => {
    expect(
      el()
        .querySelector('[data-testid="signup-password"]')!
        .getAttribute('autocomplete'),
    ).toBe('new-password');

    expect(
      el()
        .querySelector('[data-testid="signup-confirm"]')!
        .getAttribute('autocomplete'),
    ).toBe('new-password');
  });
});

// ====================================================== FORGOT PASSWORD

describe('ForgotPasswordPage', () => {
  let fixture: ComponentFixture<ForgotPasswordPage>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [ForgotPasswordPage],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ForgotPasswordPage);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  it('posts the email address to the forgot-password endpoint', () => {
    setInput(fixture, 'forgot-email', 'person@example.com');
    submitForm(fixture);

    const request = httpMock.expectOne(FORGOT_URL);

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ email: 'person@example.com' });

    request.flush({ message: 'If an account exists for that email, we sent password reset instructions.' });
  });

  it('renders the neutral server message verbatim, without confirming the account exists', () => {
    setInput(fixture, 'forgot-email', 'nobody@example.com');
    submitForm(fixture);

    const neutralMessage =
      'If an account exists for that email, we sent password reset instructions.';

    httpMock.expectOne(FORGOT_URL).flush({ message: neutralMessage });
    fixture.detectChanges();

    const success = el().querySelector('[data-testid="forgot-success"]');

    expect(success).toBeTruthy();
    expect(success!.textContent).toContain(neutralMessage);

    // Nothing on the page may assert that the address was or was not found.
    const text = el().textContent!.toLowerCase();
    expect(text).not.toContain('not found');
    expect(text).not.toContain('no account');
  });

  it('blocks submission for a malformed email', () => {
    setInput(fixture, 'forgot-email', 'not-an-email');
    submitForm(fixture);

    httpMock.expectNone(FORGOT_URL);
    expect(el().querySelector('#forgot-email-error')).toBeTruthy();
  });
});

// ======================================================= RESET PASSWORD

describe('ResetPasswordPage', () => {
  let fixture: ComponentFixture<ResetPasswordPage>;
  let httpMock: HttpTestingController;
  let navigate: jasmine.Spy;

  function configure(token: string | null): void {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [ResetPasswordPage],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: convertToParamMap(
                token === null ? {} : { token },
              ),
            },
          },
        },
      ],
    });

    httpMock = TestBed.inject(HttpTestingController);
    navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    fixture = TestBed.createComponent(ResetPasswordPage);
    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  it('sends the token from the link together with the new password', () => {
    configure('token-from-link');

    setInput(fixture, 'reset-password', VALID_PASSWORD);
    setInput(fixture, 'reset-confirm', VALID_PASSWORD);
    submitForm(fixture);

    const request = httpMock.expectOne(RESET_URL);

    expect(request.request.body).toEqual({
      token: 'token-from-link',
      newPassword: VALID_PASSWORD,
      confirmPassword: VALID_PASSWORD,
    });

    request.flush({ message: 'Your password has been updated. You can now sign in.' });
  });

  it('returns to login after a successful reset', () => {
    configure('token-from-link');

    setInput(fixture, 'reset-password', VALID_PASSWORD);
    setInput(fixture, 'reset-confirm', VALID_PASSWORD);
    submitForm(fixture);

    httpMock.expectOne(RESET_URL).flush({ message: 'ok' });
    fixture.detectChanges();

    expect(navigate).toHaveBeenCalledWith(['/login'], {
      queryParams: { reset: '1' },
    });
  });

  it('shows a recovery path instead of a form when the link carries no token', () => {
    configure(null);

    expect(el().querySelector('[data-testid="reset-missing-token"]')).toBeTruthy();
    expect(el().querySelector('form')).toBeNull();

    const requestNew = el().querySelector<HTMLAnchorElement>(
      '[data-testid="reset-request-new"]',
    );

    expect(requestNew).toBeTruthy();
    expect(requestNew!.getAttribute('href')).toBe('/forgot-password');
  });

  it('blocks submission when the confirmation does not match', () => {
    configure('token-from-link');

    setInput(fixture, 'reset-password', VALID_PASSWORD);
    setInput(fixture, 'reset-confirm', 'test-only-different-value');
    submitForm(fixture);

    httpMock.expectNone(RESET_URL);
    expect(el().querySelector('#reset-confirm-error')).toBeTruthy();
  });

  it('surfaces an expired or already-used link with a way to request a new one', () => {
    configure('stale-token');

    setInput(fixture, 'reset-password', VALID_PASSWORD);
    setInput(fixture, 'reset-confirm', VALID_PASSWORD);
    submitForm(fixture);

    httpMock.expectOne(RESET_URL).flush(
      {
        title: 'Bad Request',
        status: 400,
        detail: 'This password reset link is invalid or has expired.',
      },
      { status: 400, statusText: 'Bad Request' },
    );
    fixture.detectChanges();

    const error = el().querySelector('[data-testid="reset-error"]');

    expect(error).toBeTruthy();
    expect(error!.textContent).toContain('invalid or has expired');
    expect(
      el().querySelector('[data-testid="reset-error-request-new"]'),
    ).toBeTruthy();
    expect(navigate).not.toHaveBeenCalled();
  });
});
