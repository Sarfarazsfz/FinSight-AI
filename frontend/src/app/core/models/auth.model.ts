/** Mirrors FinSight.Application.DTOs.Auth.LoginRequest. */
export interface LoginRequest {
  email: string;
  password: string;
}

/**
 * Mirrors FinSight.Application.DTOs.Auth.LoginResponse.
 *
 * `expiresAtUtc` is an ISO-8601 timestamp string (System.Text.Json serializes
 * DateTime that way). It is used for UX only -- see AuthSession.
 */
export interface LoginResponse {
  accessToken: string;
  tokenType: string;
  expiresAtUtc: string;
  userId: string;
  email: string;
  role: string;
}

/**
 * Mirrors FinSight.Application.DTOs.Auth.RegisterRequest.
 *
 * Deliberately carries no role: the server assigns every public signup the
 * standard user role, and Admin accounts exist only via the offline
 * `create-user` provisioning command. A role field here would imply a
 * privilege the API does not accept.
 */
export interface RegisterRequest {
  email: string;
  password: string;
  confirmPassword: string;
}

/** Mirrors FinSight.Application.DTOs.Auth.RegisterResponse -- no token by design. */
export interface RegisterResponse {
  userId: string;
  email: string;
  role: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  token: string;
  newPassword: string;
  confirmPassword: string;
}

/** Both reset endpoints answer with a single human-readable message. */
export interface AuthMessageResponse {
  message: string;
}

/**
 * The persisted subset of a LoginResponse.
 *
 * This is the one place where a UI-state type legitimately differs from an
 * API model: it is what gets written to storage, not what the API returned.
 * `tokenType` is deliberately not persisted -- the backend always issues
 * "Bearer" and the interceptor hardcodes that scheme.
 */
export interface AuthSession {
  accessToken: string;
  expiresAtUtc: string;
  userId: string;
  email: string;
  role: string;
}
