import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import type { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { AuthApi } from '../../core/api/auth-api.service';
import { isProblemDetails } from '../../core/models/problem-details.model';
import { AuthLayout } from './auth-layout';

/**
 * Requests a password reset link.
 *
 * The success state deliberately does not confirm that an account exists.
 * The API returns the same response either way, and this page renders that
 * response verbatim -- branching on "was it found" here would reintroduce
 * the account-enumeration leak the backend is specifically avoiding.
 */
@Component({
  selector: 'app-forgot-password-page',
  imports: [ReactiveFormsModule, RouterLink, AuthLayout],
  templateUrl: './forgot-password-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ForgotPasswordPage {
  private readonly formBuilder = inject(FormBuilder);
  private readonly authApi = inject(AuthApi);

  protected readonly isSubmitting = signal(false);
  protected readonly authError = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly form = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
  });

  protected get emailInvalid(): boolean {
    const control = this.form.controls.email;
    return control.invalid && control.touched;
  }

  protected submit(): void {
    if (this.isSubmitting()) {
      return;
    }

    this.authError.set(null);
    this.successMessage.set(null);
    this.form.markAllAsTouched();

    if (this.form.invalid) {
      return;
    }

    this.isSubmitting.set(true);

    this.authApi.forgotPassword(this.form.getRawValue()).subscribe({
      next: (response) => {
        this.isSubmitting.set(false);
        this.successMessage.set(response.message);
      },
      error: (error: HttpErrorResponse) => {
        this.isSubmitting.set(false);
        this.authError.set(ForgotPasswordPage.toMessage(error));
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

    return 'Could not send reset instructions. Please try again.';
  }
}
