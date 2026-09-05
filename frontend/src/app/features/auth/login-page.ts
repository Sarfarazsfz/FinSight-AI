import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import type { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthApi } from '../../core/api/auth-api.service';
import { AuthStore } from '../../core/state/auth-store';
import { isProblemDetails } from '../../core/models/problem-details.model';
import { DEFAULT_POST_LOGIN_ROUTE, safeReturnUrl } from '../../core/util/return-url';

/**
 * The application's sign-in entry point, and the only place a session is
 * created -- registration and password reset both end by sending the user
 * here rather than issuing a token themselves.
 *
 * There is still no refresh token and no social provider, so no such
 * affordance is offered.
 */
@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './login-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginPage implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly authApi = inject(AuthApi);
  private readonly authStore = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly isSubmitting = signal(false);
  protected readonly authError = signal<string | null>(null);

  /**
   * Purely a display toggle for the password field's `type` attribute --
   * never touches the FormControl's value, so the entered password is
   * unaffected by showing/hiding it.
   */
  protected readonly passwordVisible = signal(false);

  /**
   * Set from a query flag written by signup / reset completion. Neither
   * flag carries a credential -- they exist only so this page can
   * acknowledge what just happened.
   */
  protected readonly noticeMessage = signal<string | null>(null);

  protected readonly form = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  ngOnInit(): void {
    // A signed-in user has no business on the login page.
    if (this.authStore.isAuthenticated()) {
      void this.router.navigateByUrl(this.resolveReturnUrl());
      return;
    }

    const params = this.route.snapshot.queryParamMap;

    if (params.get('created') === '1') {
      this.noticeMessage.set('Account created. Sign in to continue.');
    } else if (params.get('reset') === '1') {
      this.noticeMessage.set('Password updated. Sign in with your new password.');
    }
  }

  protected get emailInvalid(): boolean {
    const control = this.form.controls.email;
    return control.invalid && control.touched;
  }

  protected get passwordInvalid(): boolean {
    const control = this.form.controls.password;
    return control.invalid && control.touched;
  }

  protected togglePasswordVisibility(): void {
    this.passwordVisible.update((visible) => !visible);
  }

  protected submit(): void {
    if (this.isSubmitting()) {
      return;
    }

    this.authError.set(null);
    this.form.markAllAsTouched();

    if (this.form.invalid) {
      return;
    }

    this.isSubmitting.set(true);

    this.authApi.login(this.form.getRawValue()).subscribe({
      next: (response) => {
        this.authStore.setSession(response);
        this.isSubmitting.set(false);
        void this.router.navigateByUrl(this.resolveReturnUrl());
      },
      error: (error: HttpErrorResponse) => {
        this.isSubmitting.set(false);
        this.authError.set(LoginPage.toMessage(error));
      },
    });
  }

  private resolveReturnUrl(): string {
    return safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'));
  }

  /**
   * Turns a failed login into something a person can act on.
   *
   * `ProblemDetails.detail` is rendered as-is when the backend supplies one
   * -- it is already a human-readable sentence ("Invalid email or
   * password.", "Password is required."). It is never parsed or picked
   * apart to reconstruct field-level errors.
   */
  private static toMessage(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'Cannot reach the server. Check that the FinSight API is running and try again.';
    }

    const detail = isProblemDetails(error.error) ? error.error.detail : undefined;

    if (detail) {
      return detail;
    }

    if (error.status >= 500) {
      return 'The server could not complete the request. Please try again.';
    }

    return 'Sign in failed. Please try again.';
  }
}

export { DEFAULT_POST_LOGIN_ROUTE };
