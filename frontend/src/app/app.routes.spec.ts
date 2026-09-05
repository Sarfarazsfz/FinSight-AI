import { routes } from './app.routes';
import { authGuard } from './core/guards/auth.guard';

describe('app.routes', () => {
  it('serves the public landing page at "/" with no guard', () => {
    const rootRoute = routes.find((r) => r.path === '' && r.pathMatch === 'full');

    expect(rootRoute).toBeTruthy();
    expect(rootRoute!.canActivate).toBeFalsy();
    expect(rootRoute!.loadComponent).toBeTruthy();
  });

  it('keeps /login unguarded and pointed at the existing LoginPage', () => {
    const loginRoute = routes.find((r) => r.path === 'login');

    expect(loginRoute).toBeTruthy();
    expect(loginRoute!.canActivate).toBeFalsy();
  });

  it('keeps every application route behind authGuard, unchanged', () => {
    const shellRoute = routes.find(
      (r) => r.path === '' && r.pathMatch !== 'full',
    );

    expect(shellRoute).toBeTruthy();
    expect(shellRoute!.canActivate).toEqual([authGuard]);

    const childPaths = (shellRoute!.children ?? []).map((c) => c.path);
    expect(childPaths).toEqual([
      'batches',
      'batches/upload',
      'runs/:runId',
      'runs/:runId/verify',
      'runs/:runId/results',
      'runs/:runId/results/:resultId',
      'runs/:runId/exceptions',
      'runs/:runId/exceptions/:exceptionId',
      'data-generator',
    ]);
  });

  it('falls back unknown paths to the landing route, not an authenticated one', () => {
    const wildcard = routes.find((r) => r.path === '**');
    expect(wildcard!.redirectTo).toBe('');
  });
});
