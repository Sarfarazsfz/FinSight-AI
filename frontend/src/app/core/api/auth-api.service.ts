import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import type { LoginRequest, LoginResponse } from '../models/auth.model';

/**
 * Thin typed wrapper over the backend's only authentication endpoint.
 *
 * The backend exposes exactly one action on AuthController -- there is no
 * registration, refresh-token, logout or password-reset endpoint -- so this
 * service has exactly one method. Nothing here may be added speculatively.
 */
@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);

  private readonly baseUrl = `${environment.apiBaseUrl}/auth`;

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.baseUrl}/login`, request);
  }
}
