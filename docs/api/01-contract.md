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

**`LoginResponse`** — `accessToken` · `tokenType` (`"Bearer"`) · `expiresAtUtc` ·
`userId` · `email` · `role`

No registration, refresh, or logout endpoint exists.

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
| GET | `/exceptions/{exceptionId:guid}` | — | `200 ReconciliationExceptionResponse` | 404 |
| POST | `/exceptions/{exceptionId:guid}/ai-explanation` | — | `200 AiExplanationResponse` | 400 · 404 · **503 both AI providers down** |
| POST | `/runs/{runId:guid}/ground-truth-verification` | `GroundTruthRow[]` | `200 GroundTruthComparisonResult` | 400 empty array · 404 run not found |

> **Correction to `[ZIP]` doc 08.** Ground-truth verification is **POST** with a
> `GroundTruthRow[]` body returning the full comparison result — *not* the documented
> `GET` with a five-field response. See
> [architecture/05](../architecture/05-ground-truth-evaluation.md#http-contract--corrected).

### Response shapes

**`ReconciliationRunResult`** — `runId` · `batchId` · `status` · `totalReconciliationUnits`
· `matchedCount` · `mismatchedCount` · `missingCount` · `duplicateCount` ·
`unresolvedCount` · `matchRate`

**`ReconciliationRunDetailsResponse`** — `runId` · `batchId` · `status` ·
`totalReconciliationUnits` · **`matchRate` (nullable)** · `startedAt` ·
`completedAt` (nullable) · `createdAt`

**`ReconciliationRunSummaryResponse`** — `runId` · `batchId` · `status` · `totalUnits` ·
`matched` · `mismatched` · `missing` · `duplicate` · `unresolved` · `matchRate` ·
`exceptionCount`
→ *This is the Run Overview's primary source.*

**`ReconciliationResultResponse`** — `resultId` · `runId` · `normalizedTransactionId` ·
`transactionReference` · `status` · **`strategyUsed` (nullable)** · `reasonCode` ·
`createdAt`

**`ReconciliationTransactionDetailResponse`** — `resultId` · `runId` ·
`normalizedTransactionId` · `transactionReference` · `status` · `strategyUsed` (nullable)
· `reasonCode` · `payments[]` · `banks[]` · `settlements[]`
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
| POST | `/ask` | `{ runId, question }` | `200 FinanceAssistantResponse` | 400 missing body/runId/question · 401 · **503 both AI providers down** |

**`FinanceAssistantResponse`** — `answer` · `toolsUsed[]` · **`traceId` (nullable)**

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
throughput/metrics · user registration · refresh token · logout · batch delete.

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
