import { inject } from '@angular/core';
import type { CanActivateFn } from '@angular/router';
import { Router } from '@angular/router';
import { AuthStore } from '../state/auth-store';

/**
 * Blocks entry into an authenticated route without a live session.
 *
 * Defence in depth alongside errorInterceptor: the guard prevents entry,
 * the interceptor handles a session expiring mid-use.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const authStore = inject(AuthStore);
  const router = inject(Router);

  if (authStore.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/login'], {
    queryParams: { returnUrl: state.url },
  });
};
