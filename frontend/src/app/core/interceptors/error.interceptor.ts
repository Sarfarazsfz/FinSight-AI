import type { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthStore } from '../state/auth-store';

/**
 * Global handling for 401 ONLY, and only when a session actually existed.
 *
 * A 401 means "your session has ended" -- which is only meaningful if there
 * was a session to end. A failed sign-in also returns 401, but the user is
 * already on the login page with no session; treating that as an expiry
 * would redirect them to the page they are on and stamp a self-referential
 * `?returnUrl=/login` onto the URL. So the redirect is gated on there being
 * a session to clear.
 *
 * Every other status -- 400, 403, 404, 500, 503 -- is deliberately passed
 * through untouched. Those are surface-specific: a 400 with structured
 * validation errors belongs to the upload screen, a 503 belongs to the AI
 * panel. Flattening them into a global handler would lose that meaning.
 *
 * No retries. The backend performs a single fast AI provider failover
 * internally; a client-side retry layer would hide failures the UI is
 * supposed to surface.
 */
export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const authStore = inject(AuthStore);
  const router = inject(Router);

  return next(request).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && authStore.isAuthenticated()) {
        authStore.clearSession();

        void router.navigate(['/login'], {
          queryParams: { returnUrl: router.url },
        });
      }

      return throwError(() => error);
    }),
  );
};
