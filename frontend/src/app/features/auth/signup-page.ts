import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import type { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { AuthApi } from '../../core/api/auth-api.service';
import { AuthStore } from '../../core/state/auth-store';
import { isProblemDetails } from '../../core/models/problem-details.model';
import { AuthLayout } from './auth-layout';

/** Mirrors the backend's CredentialPolicy.MinimumPasswordLength. */
const MINIMUM_PASSWORD_LENGTH = 8;

/**
 * Group-level validator. Applied to the form rather than to the confirm
 * control so it re-evaluates when *either* password field changes -- a
 * control-level validator would go stale when the first field is edited
 * after the second.
 */
function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const password = group.get('password')?.value;
  const confirmPassword = group.get('confirmPassword')?.value;

  return password === confirmPassword ? null : { passwordMismatch: true };
}

/**
 * Public account creation.
 *
 * No role is collected or sent: the API assigns every signup the standard
 * user role, and Admin accounts come only from the offline `create-user`
 * provisioning command. Offering a role picker here would imply a
 * privilege escalation the server would reject anyway.
 *
 * On success this navigates to /login rather than signing the user in.
 * The register endpoint deliberately issues no token, so login remains the
 * single path that creates a session.
 */
@Component({
  selector: 'app-signup-page',
  imports: [ReactiveFormsModule, RouterLink, AuthLayout],
  templateUrl: './signup-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SignupPage implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly authApi = inject(AuthApi);
  private readonly authStore = inject(AuthStore);
  private readonly router = inject(Router);

  protected readonly minimumPasswordLength = MINIMUM_PASSWORD_LENGTH;

  protected readonly isSubmitting = signal(false);
  protected readonly authError = signal<string | null>(null);

  protected readonly form = this.formBuilder.nonNullable.group(
    {
      email: ['', [Validators.required, Validators.email]],
      password: [
        '',
        [Validators.required, Validators.minLength(MINIMUM_PASSWORD_LENGTH)],
      ],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordsMatch },
  );

  ngOnInit(): void {
    if (this.authStore.isAuthenticated()) {
      void this.router.navigateByUrl('/batches');
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

  protected get confirmInvalid(): boolean {
    const control = this.form.controls.confirmPassword;

    return (
      control.touched &&
      (control.invalid || this.form.hasError('passwordMismatch'))
    );
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

    this.authApi.register(this.form.getRawValue()).subscribe({
      next: () => {
        this.isSubmitting.set(false);

        // Explicit sign-in after registration. `created=1` lets the login
        // page acknowledge the new account without carrying any
        // credential in the URL.
        void this.router.navigate(['/login'], {
          queryParams: { created: '1' },
        });
      },
      error: (error: HttpErrorResponse) => {
        this.isSubmitting.set(false);
        this.authError.set(SignupPage.toMessage(error));
      },
    });
  }

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

    return 'Could not create your account. Please try again.';
  }
}
