/**
 * Production configuration.
 *
 * The API is deployed on Railway at a separate origin from the Vercel-hosted
 * frontend. All API services construct their URLs from this single value;
 * nothing else in the application references a base URL directly.
 *
 * After the Vercel frontend URL is confirmed, add it to the backend's
 * `Cors:AllowedOrigins` Railway environment variable so the browser's
 * cross-origin preflight requests are accepted.
 */
export const environment = {
  production: true,
  apiBaseUrl: 'https://finsight-ai-production-4c52.up.railway.app/api',
} as const;
