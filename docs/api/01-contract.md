# API Contract

`[CODE]` **Derived from controller source. This document is authoritative for the current
implementation and supersedes `[ZIP]` doc 11 entirely.**

> **Maintenance rule:** regenerate this from source when the API changes. Never
> hand-maintain it as an independent truth. No endpoint may be documented here that does
> not exist in `FinSight.Api/Controllers/`.

**Base URL** — dev `http://localhost:5180/api` (or `https://localhost:7148/api`).
**Auth** — `Authorization: Bearer <jwt>` on everything except login.
**Errors** — uniform `ProblemDetails`; see [02-error-handling.md](02-error-handling.md).

---

## Auth — `api/auth`

| Method | Path | Auth | Request | Success | Errors |
|---|---|---|---|---|---|
| POST | `/login` | Anonymous | `{ email, password }` | `200 LoginResponse` | 400 missing field · 401 invalid credentials |
| POST | `/register` | Anonymous | `{ email, password, confirmPassword }` | `201 RegisterResponse` | 400 invalid email/password/mismatch · 409 duplicate email |
| POST | `/forgot-password` | Anonymous | `{ email }` | `200 { message }` | 400 malformed email · **429 rate-limited (see below)** |
| POST | `/reset-password` | Anonymous | `{ token, newPassword, confirmPassword }` | `200 { message }` | 400 invalid/expired/already-used token, weak password, or mismatch |

**`LoginResponse`** — `accessToken` · `tokenType` (`"Bearer"`) · `expiresAtUtc` ·
`userId` · `email` · `role`

**`RegisterResponse`** — `userId` · `email` · `role`. Every public signup receives the
standard `"User"` role; there is no field through which a caller can request `"Admin"`.

**Forgot-password / reset-password anti-enumeration** — `/forgot-password` always answers
`200` with the identical message for a known and an unknown address
(`"If an account exists for that email, we sent password reset instructions."`); no field
in the response distinguishes the two. `/reset-password` reports an unknown, expired, and
already-used token identically (`"This password reset link is invalid or has expired."`).
Reset tokens are 256 bits of CSPRNG output, stored only as a SHA-256 hash, single-use, and
expire after 60 minutes by default (`Auth:PasswordReset` configuration).

**Forgot-password rate limiting** — `/forgot-password` is rate-limited in-process, checked
before any account lookup so it cannot itself become a signal that distinguishes a known
address from an unknown one:

| Key | Limit | Window |
|---|---|---|
| Normalized email (trim + lowercase, the same normalization used everywhere else) | 5 requests | 15 minutes |
| Client IP (`HttpContext.Connection.RemoteIpAddress`; no `X-Forwarded-For` trust — this project has no reverse-proxy/trusted-header configuration) | 20 requests | 15 minutes |

Exceeding either returns **`429 Too Many Requests`** (`ProblemDetails`, `detail: "Too many
password reset requests. Please try again later."` — never mentions account existence),
with a `Retry-After` header (seconds until that key's window resets). Both limits are
configuration-driven (`Auth:PasswordResetRateLimit`), overridable per environment.

This is a **single-process, in-memory** limiter — it protects one running API instance,
not a distributed/multi-instance deployment, and makes no DDoS-protection claim. FinSight
currently runs as a single instance, so this is the deployment's actual, complete
protection against forgot-password abuse today, not a partial mitigation.

---

## Batches — `api/batches` · all `[Authorize]`

| Method | Path | Request | Success | Errors |
|---|---|---|---|---|
| POST | `/` | `multipart/form-data`: `batchLabel`, `createdBy`, `paymentsFile`, `bankFile`, `settlementsFile` | `201 BatchIngestionResult` | **400 + `errors[]`** on validation failure · 400 missing field/file |
| GET | `/` | `pageNumber` (≥1, default 1), `pageSize` (1–100, default 50) | `200 PagedResponse<BatchResponse>` | 400 invalid paging |
| GET | `/{batchId:guid}` | — | `200 BatchResponse` | 404 |

> `[ZIP]` doc 11 marks the two GET endpoints `[NOT PROVEN]`. `[CODE]` **Both exist.**

**`BatchResponse`** — `batchId` · `batchLabel` · `paymentRecordCount` ·
`bankRecordCount` · `settlementRecordCount` · `totalRecordCount` · `validationStatus` ·
`createdBy` · `createdAt`

**`BatchIngestionResult`** — `batchId` · `validationStatus` · `paymentRecordCount` ·
`bankRecordCount` · `settlementRecordCount` · `totalRecordCount`

**Ordering** — batch history is `CreatedAt DESC`, then `Id DESC` as a stable tie-break.

**Ownership** — a batch belongs to the authenticated user who created it. `GET /` lists
only the caller's own batches; `GET /{batchId}` returns **404** for a batch that exists
but was created by someone else, identical to a genuinely unknown id — a caller cannot
distinguish the two. `POST /` always assigns ownership from the authenticated caller's
token; the `createdBy` form field is a display label only and has no bearing on
ownership. Batches created before ownership existed, whose `createdBy` label could not be
matched to a real account, belong to no one and are inaccessible to everyone.

---

## Reconciliation — `api/reconciliation` · all `[Authorize]`

| Method | Path | Request | Success | Errors |
|---|---|---|---|---|
| POST | `/runs` | `{ batchId }` | `201 ReconciliationRunResult` | 400 empty batchId · 404 batch not found |
| GET | `/runs/{runId:guid}` | — | `200 ReconciliationRunDetailsResponse` | 404 |
| GET | `/runs/{runId:guid}/summary` | — | `200 ReconciliationRunSummaryResponse` | 404 |
| GET | `/runs/{runId:guid}/results` | `pageNumber`, `pageSize` (1–100) | `200 PagedResponse<ReconciliationResultResponse>` | 400 · 404 |
| GET | `/runs/{runId:guid}/results/{resultId:guid}` | — | `200 ReconciliationTransactionDetailResponse` | 404 |
| GET | `/runs/{runId:guid}/exceptions` | `pageNumber`, `pageSize` (1–100) | `200 PagedResponse<ReconciliationExceptionResponse>` | 400 · 404 |
| GET | `/runs/{runId:guid}/audit` | `pageNumber`, `pageSize` (1–100) | `200 PagedResponse<AuditLogEntryResponse>` | 400 · 404 |
| GET | `/exceptions/{exceptionId:guid}` | — | `200 ReconciliationExceptionResponse` | 404 |
| POST | `/exceptions/{exceptionId:guid}/ai-explanation` | — | `200 AiExplanationResponse` | 400 · 404 · **503 both AI providers down** |
| POST | `/runs/{runId:guid}/ground-truth-verification` | `GroundTruthRow[]` | `200 GroundTruthComparisonResult` | 400 empty array · 404 run not found |

> **Correction to `[ZIP]` doc 08.** Ground-truth verification is **POST** with a
> `GroundTruthRow[]` body returning the full comparison result — *not* the documented
> `GET` with a five-field response. See
> [architecture/05](../architecture/05-ground-truth-evaluation.md#http-contract--corrected).

**Ownership** — every endpoint above resolves ownership transitively through the run's
batch (a run has no owner of its own): a run whose batch belongs to another user returns
**404**, indistinguishable from an unknown `runId`, and the check runs *before* the
reconciliation engine, the AI explanation call, or the ground-truth comparison ever
executes. `GET /exceptions/{exceptionId}` and
`POST /exceptions/{exceptionId}/ai-explanation` carry no `runId` in their route, so
ownership is resolved by first looking up the exception's parent run, then checking that
run's batch — the exception itself is never returned or acted on before that check
passes.

**Audit evidence** — `GET /runs/{runId}/audit` is **strictly read-only**: there is no
create/update/delete audit endpoint anywhere in this API, and none of this project's
controllers expose one. It reads FinSight's existing `audit_logs` table — the same store
`BatchIngestionService`, `ReconciliationOrchestrator`, `AiExplanationService` and
`FinanceAssistantService` already write to; this is not a second audit system. Results are
newest-first (`OccurredAt DESC`, `Id DESC` as a tie-break). This is evidence **about** a
run's execution — never a second source of financial truth. Match status, match rate,
exception counts and classification remain whatever `GET .../summary` and Ground Truth
Verification report; nothing here recomputes or overrides them. The underlying `AuditLog`
entity carries no actor/user-identity column, so no such field is ever returned — it is
not fabricated to look complete.

### Response shapes

**`ReconciliationRunResult`** — `runId` · `batchId` · `status` · `totalReconciliationUnits`
· `matchedCount` · `mismatchedCount` · `missingCount` · `duplicateCount` ·
`unresolvedCount` · `matchRate`

**`ReconciliationRunDetailsResponse`** — `runId` · `batchId` · `status` ·
`totalReconciliationUnits` · **`matchRate` (nullable)** · `startedAt` ·
`completedAt` (nullable) · `createdAt`

**`ReconciliationRunSummaryResponse`** — `runId` · `batchId` · `status` · `totalUnits` ·
`matched` · `mismatched` · `missing` · `duplicate` · `unresolved` · `matchRate` ·
`exceptionCount` · **`durationMs` (nullable)** · **`recordsPerSecond` (nullable)**
→ *This is the Run Workspace's (`/runs/:runId`) primary source: it backs both the
five-count reconciliation breakdown and the Run performance panel. The two timing fields
are `null` for a run that has not completed.*

**`ReconciliationResultResponse`** — `resultId` · `runId` · `normalizedTransactionId` ·
`transactionReference` · `status` · **`strategyUsed` (nullable)** · `reasonCode` ·
`createdAt`

**`ReconciliationTransactionDetailResponse`** — `resultId` · `runId` ·
`normalizedTransactionId` · `transactionReference` · `status` · `strategyUsed` (nullable)
· `reasonCode` · `payments[]` · `banks[]` · `settlements[]`

**`AuditLogEntryResponse`** — `id` · `occurredAt` · `eventType` (one of `BatchCreated`,
`BatchValidated`, `ReconciliationStarted`, `ReconciliationCompleted`,
`ReconciliationFailed`, `ReconciliationDecisionRecorded`, `ExceptionCreated`,
`AiQuestionAsked`, `AiToolInvoked`, `AiExplanationRequested`, `AiExplanationFailed`,
`AiAssistantFailed`) · **`runId` (nullable)** · **`relatedEntityType` (nullable)** ·
**`relatedEntityId` (nullable)** · `detail` (raw JSON string, passed through unparsed —
the same convention as `ReconciliationExceptionResponse.discrepancyDetail`; its shape
varies by `eventType` and is never validated against a fixed schema server-side). A
`ReconciliationCompleted` event's `detail` carries the same `duration_ms` /
`records_per_second` figures `ReconciliationRunSummaryResponse` exposes as `durationMs` /
`recordsPerSecond` — both are real, independently-timed wall-clock measurements of
overlapping but not identical windows (the audit figure includes the initial batch
lookup; the summary figure is the persisted run's `StartedAt`→`CompletedAt` span), so
minor differences between them are expected and neither is recomputed from the other.
where each element is **`SourceTransactionRecordResponse`** — `id` ·
`sourceRecordIdentifier` · `transactionReference` · `amount` · `currency` ·
`transactionDate` · `status` · `createdAt`
→ *This is the three-source evidence comparison.*

**`ReconciliationExceptionResponse`** — `exceptionId` · `runId` ·
`reconciliationResultId` · `transactionReference` · `category` · `involvedSources` ·
`discrepancyDetail` · **`aiExplanation` (nullable)** ·
**`aiSuggestedCategory` (nullable)** · **`aiExplanationGeneratedAt` (nullable)** ·
`createdAt` · **`updatedAt` (nullable)**
→ *`discrepancyDetail` is verified evidence. The three `ai*` fields are advisory. They
must never be rendered as peers.*

**`AiExplanationResponse`** — `provider` · `explanation` · **`suggestedCategory`
(nullable)** · `generatedAtUtc`

**`GroundTruthRow`** (request element) — `transactionReference` · `scenarioType` ·
`expectedStatus` · `expectedReasonCode` · `expectedExceptionCategory` ·
`expectedPaymentPresent` · `expectedBankPresent` · `expectedSettlementPresent` ·
`expectedAmountRelationship` · `expectedDateRelationship`

**`GroundTruthComparisonResult`** — `isSuccess` · `expectedTotalUnits` ·
`actualTotalUnits` · `expectedMatched` / `actualMatched` · `expectedMismatched` /
`actualMismatched` · `expectedMissing` / `actualMissing` · `expectedDuplicate` /
`actualDuplicate` · `expectedUnresolved` / `actualUnresolved` · `expectedMatchRate` ·
`actualMatchRate` · **`failures[]`**

---

## Finance Assistant — `api/finance-assistant` · `[Authorize]`

| Method | Path | Request | Success | Errors |
|---|---|---|---|---|
| POST | `/ask` | `{ runId, question }` | `200 FinanceAssistantResponse` | 400 missing body/runId/question · 401 · **404 `runId` not owned by the caller** · **503 both AI providers down** |

**`FinanceAssistantResponse`** — `answer` · `toolsUsed[]` · **`traceId` (nullable)**

**Ownership** — the same batch-rooted check as Reconciliation above: a `runId` the caller
does not own returns 404 before the assistant, its AI provider, or any of its read-only
tools are ever invoked.

---

## Synthetic Data — `api/test-data`

All endpoints require `Authorization: Bearer <jwt>`. Datasets are user-scoped; a
`generationId` issued to user A returns 404 for user B.  No production data is accessed —
generation is pure, deterministic computation.

| Method | Path | Auth | Request | Success | Errors |
|---|---|---|---|---|---|
| POST | `/generate` | Required | `{ size, mode, intensity, seed? }` | `200 GenerateDatasetResponse` | 400 invalid size/mode/intensity · 401 |
| GET | `/download/{generationId}/payments` | Required | — | `200 text/csv` | 401 · 404 not found or expired |
| GET | `/download/{generationId}/bank` | Required | — | `200 text/csv` | 401 · 404 |
| GET | `/download/{generationId}/settlements` | Required | — | `200 text/csv` | 401 · 404 |
| GET | `/download/{generationId}/ground-truth` | Required | — | `200 text/csv` | 401 · 404 |

**`GenerateDatasetResponse`** — `{ metadata: GeneratedDatasetMetadata }`

**`GeneratedDatasetMetadata`** — `generationId` · `seed` · `mode` · `size` · `intensity`
· `createdAt` · `scenarioDistribution { Matched, Mismatched, Missing, Duplicate, Unresolved }`
· `expectedMatched` · `expectedMismatched` · `expectedMissing` · `expectedDuplicate` · `expectedUnresolved`

**Allowed sizes** — `50 | 100 | 250 | 500`

**Allowed modes** (integer) — `0 Clean | 1 AmountMismatch | 2 DateMismatch | 3 MissingBank |
4 MissingSettlement | 5 MissingPayment | 6 Duplicate | 7 Unresolved | 8 Mixed | 9 RandomChaos`

**Allowed intensities** (integer) — `0 Low | 1 Medium | 2 High`

**Determinism** — same `seed + size + mode + intensity` always produces identical CSVs.
Omit `seed` (or set `null`) for a new cryptographically-random seed.

**Session TTL** — datasets expire 1 hour after generation; download endpoints return 404 after expiry.

**Ground truth** — derived from the generation scenario assignment only, never from
reconciliation output.  Upload the three CSV files through Batches → Upload Batch, then
run reconciliation and compare against the downloaded `ground-truth.csv`.

---

## Shared

**`PagedResponse<T>`** — `items[]` · `pageNumber` · `pageSize` · `totalCount` ·
`totalPages`

Pagination is **1-based**; `pageSize` max **100**; `totalPages` is `0` when
`totalCount` is `0`.

---

## Enumerations

Bind UI vocabularies to these exactly `[CODE]`:

| Enum | Values |
|---|---|
| `MatchStatus` | `Matched` · `Mismatched` · `Missing` · `Duplicate` · `Unresolved` |
| `ExceptionCategory` | `AmountMismatch` · `DateMismatch` · `MissingRecord` · `DuplicateRecord` · `Unresolved` |
| `ReconciliationRunStatus` | `Pending` · `Running` · `Completed` · `Failed` |
| `ReconciliationReasonCode` | `EXACT_MATCH` · `TOLERANCE_MATCH` · `AMOUNT_MISMATCH` · `DATE_OUT_OF_TOLERANCE` · `SOURCE_ABSENT_PAYMENT` · `SOURCE_ABSENT_BANK` · `SOURCE_ABSENT_SETTLEMENT` · `DUPLICATE_PAYMENT` · `DUPLICATE_BANK` · `DUPLICATE_SETTLEMENT` · `UNRESOLVED` |

Enums serialise as **strings**, not integers. `MatchStatus` has five values and the
design system defines a sixth token, `Pending`, for run status — see
[design/01-design-system.md](../design/01-design-system.md#semantic-status-colours).

---

## Endpoints that DO NOT exist

Do not design against, mock, or reference any of these:

`GET …/audit-log` · exception-resolve · `GET /api/reconciliation/runs` (run list) ·
a standalone metrics endpoint · refresh token · logout · batch delete.

**Throughput is not a separate endpoint** — `durationMs` and `recordsPerSecond` are fields
on the existing `GET /api/reconciliation/runs/{runId}/summary` response, computed from the
run's persisted `StartedAt`/`CompletedAt`. Both are **null** for a run that has not
completed.

**Also not endpoints:** `getReconciliationSummary`, `getUnmatchedRecords`,
`getTransactionDetails`, `getExceptionDetails` — these are **internal AI tools**
`[CODE]`. Derive "unmatched records" in the UI by filtering results on
`Status ∈ {Missing, Unresolved}`.

---

## Nullable fields — handle explicitly

`matchRate` (on run details) · `strategyUsed` · `aiExplanation` · `aiSuggestedCategory` ·
`aiExplanationGeneratedAt` · `updatedAt` · `completedAt` · `traceId` ·
`suggestedCategory` · `errors[]` · `rowNumber` (within a validation error)

Every one of these has a real "not yet / not applicable" meaning. Rendering `null` as
an empty string hides information the operator needs.
