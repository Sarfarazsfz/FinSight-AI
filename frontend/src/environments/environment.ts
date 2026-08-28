/**
 * Production configuration.
 *
 * Assumes the API is served from the same origin behind a reverse proxy.
 * If FinSight is ever deployed with the API on a different origin, change
 * this one value -- nothing else in the application references a base URL
 * directly -- and add the frontend origin to the backend's
 * `Cors:AllowedOrigins` configuration.
 */
export const environment = {
  production: true,
  apiBaseUrl: '/api',
} as const;
