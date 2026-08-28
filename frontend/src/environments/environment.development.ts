/**
 * Local development configuration.
 *
 * Points at the FinSight.Api "http" launch profile
 * (backend/FinSight.Api/Properties/launchSettings.json), which listens on
 * http://localhost:5180. The backend's CORS policy defaults to allowing
 * http://localhost:4200 in Development, which is the Angular dev server.
 */
export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:5180/api',
} as const;
