import type { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthStore } from '../state/auth-store';

/**
 * Attaches the bearer token to outgoing requests when a session exists.
 *
 * No URL-based exclusion is needed for the login request: at that point no
 * session exists, so no header is attached. Adding an explicit `/auth/login`
 * carve-out would be dead logic.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const token = inject(AuthStore).accessToken;

  if (!token) {
    return next(request);
  }

  return next(
    request.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    }),
  );
};
