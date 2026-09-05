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
 * /signup, /forgot-password and /reset-password each map to a real
 * AuthController action. No /analytics or /admin route exists -- neither
 * has a backend capability behind it, and a route that cannot work is
 * worse than an absent one.
 *
 * There is also no standalone /audit route: audit evidence has a real,
 * read-only backend endpoint (GET /api/reconciliation/runs/{runId}/audit,
 * added in P-1H), but its UI is embedded directly in the Run Workspace
 * (runs/:runId) rather than given its own route -- the same way Finance
 * Assistant is embedded rather than routed. See AuditEvidencePanel.
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
    path: 'signup',
    loadComponent: () =>
      import('./features/auth/signup-page').then((m) => m.SignupPage),
    title: 'Create account — FinSight',
  },
  {
    path: 'forgot-password',
    loadComponent: () =>
      import('./features/auth/forgot-password-page').then(
        (m) => m.ForgotPasswordPage,
      ),
    title: 'Reset password — FinSight',
  },
  {
    path: 'reset-password',
    loadComponent: () =>
      import('./features/auth/reset-password-page').then(
        (m) => m.ResetPasswordPage,
      ),
    title: 'Set a new password — FinSight',
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
        path: 'runs/:runId/verify',
        loadComponent: () =>
          import('./features/runs/verification/verification-page').then(
            (m) => m.VerificationPage,
          ),
        title: 'Ground truth verification — FinSight',
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
      {
        path: 'data-generator',
        loadComponent: () =>
          import('./features/data-generator/data-generator-page').then(
            (m) => m.DataGeneratorPage,
          ),
        title: 'Synthetic Data Lab — FinSight',
      },
    ],
  },
  {
    path: '**',
    redirectTo: '',
  },
];
