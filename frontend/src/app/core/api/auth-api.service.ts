import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import type {
  AuthMessageResponse,
  ForgotPasswordRequest,
  LoginRequest,
  LoginResponse,
  RegisterRequest,
  RegisterResponse,
  ResetPasswordRequest,
} from '../models/auth.model';

/**
 * Typed wrapper over the backend's AuthController.
 *
 * Every method here maps to a real action on `api/auth`; nothing is added
 * speculatively. There is deliberately no refresh-token, logout or
 * change-password call, because no such endpoint exists.
 *
 * Note that `register` returns no token: the backend does not authenticate
 * a caller as a side effect of signup, so the client signs in explicitly
 * afterwards through `login`. That keeps exactly one code path issuing a
 * session.
 */
@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);

  private readonly baseUrl = `${environment.apiBaseUrl}/auth`;

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.baseUrl}/login`, request);
  }

  register(request: RegisterRequest): Observable<RegisterResponse> {
    return this.http.post<RegisterResponse>(`${this.baseUrl}/register`, request);
  }

  /**
   * Always resolves with the same message whether or not the address is
   * registered -- the backend deliberately does not disclose that, so no
   * caller may branch on it.
   */
  forgotPassword(
    request: ForgotPasswordRequest,
  ): Observable<AuthMessageResponse> {
    return this.http.post<AuthMessageResponse>(
      `${this.baseUrl}/forgot-password`,
      request,
    );
  }

  resetPassword(
    request: ResetPasswordRequest,
  ): Observable<AuthMessageResponse> {
    return this.http.post<AuthMessageResponse>(
      `${this.baseUrl}/reset-password`,
      request,
    );
  }
}
