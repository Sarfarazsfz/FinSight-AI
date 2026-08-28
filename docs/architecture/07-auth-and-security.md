# Authentication, Authorization, Security

`[CODE]` Verified against `Program.cs`, `JwtTokenService`, `JwtOptions`, `PasswordService`,
and every controller's attributes.

---

## JWT lifecycle

- Issuer, audience, secret key, and expiration are read from configuration (`JwtOptions`)
  and validated with **fail-fast startup exceptions** if any is missing or if
  `ExpirationMinutes <= 0`. The application refuses to start misconfigured.
- `TokenValidationParameters`: `ValidateIssuer`, `ValidateAudience`,
  `ValidateIssuerSigningKey`, `ValidateLifetime` all `true`;
  **`ClockSkew = 30 seconds`** — deliberately tighter than the .NET 5-minute default.
- Signing key: `SymmetricSecurityKey` from UTF-8 bytes of the configured secret.

**No refresh-token endpoint exists.** `[CODE]` The API issues an access token only. The
frontend must therefore treat a 401 as "session over, re-authenticate" and must **not**
implement silent-refresh machinery against a scheme that cannot support it. See
[frontend/01-architecture.md](../frontend/01-architecture.md).

## Password handling

`PasswordService` wraps ASP.NET Core Identity's `PasswordHasher<object>` — not a
homegrown scheme.

**There is no registration endpoint.** Users are provisioned directly. The frontend must
not present a sign-up flow.

## Route protection `[CODE]`

| Controller | Protection |
|---|---|
| `AuthController.Login` | `[AllowAnonymous]` |
| `BatchesController` | class-level `[Authorize]` |
| `ReconciliationController` | class-level `[Authorize]` |
| `FinanceAssistantController` | class-level `[Authorize]` |

Single role (`"User"`). **No role-based authorization exists** — do not build an RBAC UI.

## CORS

> **RESOLVED** `[ZIP]` doc 13 records CORS as *"CONFIRMED ABSENT — hard blocker"* for the
> frontend.
>
> **Status: FIXED** `[CODE]`. A named policy is registered and applied. Allowed origins
> are configuration-driven (`Cors:AllowedOrigins`), defaulting in Development to the
> Angular CLI origin `http://localhost:4200`. Never `AllowAnyOrigin()`. No
> `AllowCredentials()` — authentication is a Bearer header, not a cookie.

## HTTPS

`app.UseHttpsRedirection()` is applied conditionally by environment/configuration.
Dev profiles: `http://localhost:5180`, `https://localhost:7148`.

---

## Secrets policy

| Environment | Mechanism |
|---|---|
| Development | User Secrets (`dotnet user-secrets`) — `FinSight.Api.csproj` declares a `UserSecretsId` |
| Hosted / demo | Environment variables — `Jwt__SecretKey`, `AI__Gemini__ApiKey`, `AI__OpenAI__ApiKey`, `ConnectionStrings__FinSightDb` |

`[CODE]` No secret values appear in any tracked `appsettings*.json` — both contain only
logging configuration.

### Rules

1. **Never commit a key.** Never log one.
2. **Never print secret values to a terminal**, including "redacted" output — a redaction
   pattern that silently fails to match leaks the real value. If a leak occurs, stop
   immediately, disclose it plainly, and rotate.
3. **Never place a secret, token, or API key in frontend source.** The frontend holds
   only a user's own short-lived JWT.
4. The `UserSecretsId` GUID itself is not sensitive and is fine to commit.
5. Before making the repository public, re-scan **history**, not just the working tree —
   untracking a file does not remove it from history.

---

## Frontend security requirements

| Requirement | Rationale |
|---|---|
| Bearer token attached by a functional HTTP interceptor | One place, not scattered per-call |
| 401 → clear session, redirect to login | The only correct response with no refresh flow |
| Route guard on every authenticated route | Defence in depth alongside the interceptor |
| No secrets or keys in frontend source or environment files | The API base URL is the only environment-specific value |
| No PII or identifiers in URL query strings | |
| Never render server-supplied HTML unsanitised | AI explanation text is server-supplied — render as text, never as HTML |
| API base URL from environment configuration only | Never hardcoded in a component or service body |

---

## Known limitations — stated, not hidden

- No refresh tokens, no session extension.
- No RBAC beyond a single role.
- No rate limiting. `[ZIP]` doc 12 classifies this as DO NOT BUILD for this scope —
  concurred.
- No distinct handling of quota vs invalid-key AI failures.
- No explicit per-provider AI timeout configuration observed.
