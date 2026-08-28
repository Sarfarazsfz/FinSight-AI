# API Integration

How the Angular frontend talks to the backend. The contract itself lives in
[api/01-contract.md](../api/01-contract.md) — this document covers the client side only.

---

## Principle

Every service is a **thin typed HTTP wrapper**. No business logic, no recomputation, no
client-side classification. The backend is the single source of computed truth; the
frontend renders it.

---

## Services

| Service | Wraps | Methods | Status `[CODE]` |
|---|---|---|---|
| `AuthApi` | `POST /auth/login` | `login(request)` | ✅ exists |
| `BatchApi` | `POST /batches`, `GET /batches`, `GET /batches/{id}` | `upload(request)`, `getPage(pageNumber, pageSize)`, `getById(batchId)` | ✅ exists |
| `ReconciliationApi` | all `api/reconciliation` endpoints | `createRun`, `getRun`, `getSummary`, `getResults`, `getTransactionDetail`, `getExceptions`, `getException`, `requestAiExplanation`, `verifyGroundTruth` | 🚧 placeholder — fill per phase |
| `FinanceAssistantApi` | `POST /finance-assistant/ask` | `ask(runId, question)` | 🚧 placeholder — fill in P12 |

Fill each placeholder **in the phase that needs it**, not speculatively. A method with no
caller is untested code.

## Models

| File | Contents | Status |
|---|---|---|
| `problem-details.model.ts` | `ProblemDetails`, `IngestionValidationError`, `isProblemDetails()` guard | ✅ exists |
| `auth.model.ts` | `LoginRequest`, `LoginResponse`, `AuthSession` | ✅ exists |
| `batch.model.ts` | `BatchResponse`, `BatchIngestionResult`, `PagedResponse<T>` | ✅ exists |
| `reconciliation.model.ts` | run result/details/summary, result response, transaction detail, source record | ⬜ to add |
| `exception.model.ts` | exception response, AI explanation response | ⬜ to add |
| `assistant.model.ts` | assistant request/response | ⬜ to add |
| `ground-truth.model.ts` | `GroundTruthRow`, `GroundTruthComparisonResult` | ⬜ to add |

Models mirror backend DTOs **exactly**. Do not rename fields, flatten shapes, or invent
convenience properties — divergence here is how contracts rot.

## Interceptors and guard

| Item | Behaviour |
|---|---|
| `authInterceptor` | Attaches `Authorization: Bearer <token>` when a session exists |
| `errorInterceptor` | **401 only**: clear session, redirect to `/login?returnUrl=…`. Every other status passes through untouched for the caller to handle |
| `authGuard` | Redirects to `/login` when unauthenticated, preserving `returnUrl` |

`errorInterceptor` deliberately does **not** swallow, reshape, or toast 400/403/404/500/
503. Those are surface-specific and belong to the screen that made the call.

---

## Error handling contract

One `ProblemDetails` shape, one parse path — see
[api/02-error-handling.md](../api/02-error-handling.md).

```ts
const problem = isProblemDetails(error.error) ? error.error : null;

if (problem?.errors?.length) {
  // batch validation only — group by source, render row · field · message
} else {
  // single message; fall back to a written default, never to a raw stack or traceId
}
```

**Rules**
1. Render `errors[]`; **never parse `detail`**.
2. Treat `errors` as optional everywhere else.
3. Never surface `traceId` as a user-facing message — it is diagnostic detail, at most.
4. Never render server text as HTML.

---

## Pagination

Server-side only. 1-based `pageNumber`; `pageSize` 1–100.

```ts
getPage(pageNumber: number, pageSize: number): Observable<PagedResponse<T>>
```

Never fetch everything and paginate in the browser — the API caps `pageSize` at 100
precisely so clients cannot.

## Loading patterns

Route-param-keyed server state via `httpResource` / `rxResource`; explicit
`'loading' | 'loaded' | 'empty' | 'error'` union per surface. `RunContextStore` fetches
the run summary **once** at the `RunShell` level; tabs read it rather than re-fetching.

---

## Prohibited

| Never | Why |
|---|---|
| Recompute match rate, counts, reason codes, or classification | The frontend is not financial truth ([ADR-007](../adr/README.md#adr-007-the-frontend-is-not-financial-truth-either)) |
| Call `getUnmatchedRecords` / `getExceptionDetails` as HTTP | They are internal AI tools, not endpoints |
| Build against `/audit-log`, run-list, exception-resolve, refresh-token | None exist |
| Silent-refresh machinery | No refresh endpoint exists |
| Arithmetic on amounts through JS `number` for display | Backend uses `decimal(18,2)`; render, don't recompute |
| Put a secret, key, or hardcoded token in frontend source | |
| Hardcode a base URL in a component or service body | Use `environment.apiBaseUrl` |

**Permitted client-side derivation** — presentation only, never persisted or reported:
filtering a fetched page by status, sorting a fetched page, grouping `errors[]` by
source, formatting dates and amounts.

---

## Environments

| File | `apiBaseUrl` |
|---|---|
| `environment.development.ts` | `http://localhost:5180/api` |
| `environment.ts` (production) | `/api` (same-origin behind a reverse proxy) |

Wired via `angular.json` `fileReplacements`. `[CODE]` Already correct — one value to
change if the API ever moves.
