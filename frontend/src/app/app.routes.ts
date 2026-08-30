import type { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

/**
 * Route table.
 *
 * `/` is the public marketing landing page -- no auth, no redirect, no
 * backend call. It is reachable regardless of session state; an already
 * signed-in visitor who clicks through to `/login` is bounced straight
 * back into the app by LoginPage's own ngOnInit check, so nothing about
 * the authenticated flow changes.
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
    loadComponent: () =>
      import('./features/landing/landing-page').then((m) => m.LandingPage),
    title: 'FinSight AI — AI Finance Controller',
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
