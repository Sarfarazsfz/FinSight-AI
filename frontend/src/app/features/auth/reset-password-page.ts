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
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthApi } from '../../core/api/auth-api.service';
import { isProblemDetails } from '../../core/models/problem-details.model';
import { AuthLayout } from './auth-layout';

/** Mirrors the backend's CredentialPolicy.MinimumPasswordLength. */
const MINIMUM_PASSWORD_LENGTH = 8;

function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const password = group.get('newPassword')?.value;
  const confirmPassword = group.get('confirmPassword')?.value;

  return password === confirmPassword ? null : { passwordMismatch: true };
}

/**
 * Completes a password reset.
 *
 * The token arrives as a query parameter, which is how it reaches the app
 * from an emailed link. It is held in memory only -- never written to
 * storage, never logged, and never re-emitted into a URL by this page.
 */
@Component({
  selector: 'app-reset-password-page',
  imports: [ReactiveFormsModule, RouterLink, AuthLayout],
  templateUrl: './reset-password-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResetPasswordPage implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly authApi = inject(AuthApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly minimumPasswordLength = MINIMUM_PASSWORD_LENGTH;

  protected readonly isSubmitting = signal(false);
  protected readonly authError = signal<string | null>(null);

  /** Absent when the page is opened without a link. */
  protected readonly hasToken = signal(true);

  private token = '';

  protected readonly form = this.formBuilder.nonNullable.group(
    {
      newPassword: [
        '',
        [Validators.required, Validators.minLength(MINIMUM_PASSWORD_LENGTH)],
      ],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordsMatch },
  );

  ngOnInit(): void {
    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';
    this.hasToken.set(this.token.length > 0);
  }

  protected get passwordInvalid(): boolean {
    const control = this.form.controls.newPassword;
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

    const { newPassword, confirmPassword } = this.form.getRawValue();

    this.authApi
      .resetPassword({ token: this.token, newPassword, confirmPassword })
      .subscribe({
        next: () => {
          this.isSubmitting.set(false);

          // `reset=1` carries no credential -- just enough for the login
          // page to acknowledge what happened.
          void this.router.navigate(['/login'], {
            queryParams: { reset: '1' },
          });
        },
        error: (error: HttpErrorResponse) => {
          this.isSubmitting.set(false);
          this.authError.set(ResetPasswordPage.toMessage(error));
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

    return 'Could not reset your password. Please try again.';
  }
}
