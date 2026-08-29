import type { Routes } from '@angular/router';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { AuthStore } from './core/state/auth-store';

/**
 * Route table.
 *
 * `/` resolves by session state rather than rendering a landing page: the
 * public marketing surface is a later, separately-scoped phase, and a
 * placeholder in its place would be decorative work with no functional
 * value.
 *
 * No /signup, /forgot-password, /analytics, /admin or /audit route exists.
 * None has a backend capability behind it -- audit in particular has no
 * read endpoint at all -- and a route that cannot work is worse than an
 * absent one.
 */
export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    canActivate: [
      () => {
        const router = inject(Router);
        const authStore = inject(AuthStore);

        return router.createUrlTree([
          authStore.isAuthenticated() ? '/batches' : '/login',
        ]);
      },
    ],
    children: [],
  },
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/login-page').then((m) => m.LoginPage),
    title: 'Sign in — FinSight',
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./layout/app-shell/app-shell').then((m) => m.AppShell),
    children: [
      {
        path: 'batches',
        loadComponent: () =>
          import('./features/batches/batches-page').then((m) => m.BatchesPage),
        title: 'Batches — FinSight',
      },
      {
        path: 'batches/upload',
        loadComponent: () =>
          import('./features/batches/upload/batch-upload-page').then(
            (m) => m.BatchUploadPage,
          ),
        title: 'Upload batch — FinSight',
      },
      {
        path: 'runs/:runId',
        loadComponent: () =>
          import('./features/runs/run-workspace-page').then(
            (m) => m.RunWorkspacePage,
          ),
        title: 'Run — FinSight',
      },
      {
        path: 'runs/:runId/results',
        loadComponent: () =>
          import('./features/runs/results/results-page').then(
            (m) => m.ResultsPage,
          ),
        title: 'Results — FinSight',
      },
      {
        path: 'runs/:runId/results/:resultId',
        loadComponent: () =>
          import('./features/runs/results/result-detail-page').then(
            (m) => m.ResultDetailPage,
          ),
        title: 'Evidence — FinSight',
      },
      {
        path: 'runs/:runId/exceptions',
        loadComponent: () =>
          import('./features/runs/exceptions/exceptions-page').then(
            (m) => m.ExceptionsPage,
          ),
        title: 'Exceptions — FinSight',
      },
      {
        path: 'runs/:runId/exceptions/:exceptionId',
        loadComponent: () =>
          import('./features/runs/exceptions/exception-detail-page').then(
            (m) => m.ExceptionDetailPage,
          ),
        title: 'Exception — FinSight',
      },
    ],
  },
  {
    path: '**',
    redirectTo: '',
  },
];
