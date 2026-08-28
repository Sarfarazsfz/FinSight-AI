# Data Model and Database

`[CODE]` Verified against `AppDbContext`, the 9 EF configurations, and the 4 migrations.
**Schema is frozen. No migrations may be created during frontend work.**

---

## Entities

`Batch` · `PaymentRecord` · `BankRecord` · `SettlementRecord` · `NormalizedTransaction` ·
`ReconciliationRun` · `ReconciliationResult` · `ReconciliationException` · `AuditLog` ·
`User` — all registered as `DbSet<T>`.

## Monetary precision

| Column | Precision | Rationale |
|---|---|---|
| `PaymentRecord.Amount` | `decimal(18,2)` | Currency — fixed precision, no float drift |
| `BankRecord.Amount` | `decimal(18,2)` | |
| `SettlementRecord.Amount` | `decimal(18,2)` | |
| `ReconciliationRun.MatchRate` | `decimal(5,2)` | Percentage, two decimals |

CLR type is `decimal` throughout, mapped to fixed-precision numeric columns. This is a
deliberate correctness choice for a financial system, not an incidental default.

**Frontend consequence:** never coerce an amount through JavaScript `number` arithmetic
for display. Render the string the API returns, formatted — do not recompute.

## Indexes

Migration `AddReconciliationQueryIndexes` `[CODE]`:

- `IX_Payment_Batch_TransactionReference` on `payment_records(batch_id, transaction_reference)`
- `IX_Bank_Batch_TransactionReference` on `bank_records(batch_id, transaction_reference)`
- `IX_Settlement_Batch_TransactionReference` on `settlement_records(batch_id, transaction_reference)`
- `IX_Result_Run_CreatedAt` on `reconciliation_results(run_id, created_at)`
- `IX_Exception_Run_CreatedAt` on `reconciliation_exceptions(run_id, created_at)`
- Single-column FK indexes on `reconciliation_results.run_id`, `reconciliation_exceptions.run_id`

These match the API's actual query patterns exactly: batch-scoped lookup by reference,
and run-scoped paged listing ordered by creation time. Purposeful, not accidental.

## Unique constraints

- `PaymentRecordConfiguration`, `BankRecordConfiguration`, `SettlementRecordConfiguration`
  — batch-scoped unique composite index each
- `NormalizedTransactionConfiguration` — unique composite index
- `ReconciliationResult.NormalizedTransactionId` — unique (one result per normalized
  transaction)
- `ReconciliationException.ReconciliationResultId` — unique (one exception per result)
- `User.Email` — unique

The last two encode the invariants in
[03-reconciliation-engine.md](03-reconciliation-engine.md#invariants) at the schema level.

## Migrations

`InitialCreate` → `AddBankAndSettlementStatus` → `AddReconciliationQueryIndexes` →
`AddUsers`. Clean, incremental, no hand-edited or destructive migrations.

---

## Lifecycles

**Run** — `MarkRunning()` → `Complete(totalUnits, matchRate)` or `Fail()`, persisted via
`IUnitOfWork.SaveChangesAsync` in the orchestrator's try/catch.

**Exception** — created once at orchestration time with `DiscrepancyDetail` populated
immediately. `AiExplanation`, `AiSuggestedCategory`, and `AiExplanationGeneratedAt` are
populated later and only on demand.

> This split is what makes the UI's evidence-vs-AI separation structurally honest rather
> than cosmetic: verified evidence and AI commentary are **different columns, written at
> different times, by different code paths**. See
> [design/05-ai-ux.md](../design/05-ai-ux.md).

**Batch ingestion** — all-or-nothing. A validation failure persists nothing: no batch, no
records, no audit rows. `[CODE]` asserted by integration test.

---

## Audit log

`AuditEventType` `[CODE]`: `BatchCreated`, `BatchValidated`, `ReconciliationStarted`,
`ReconciliationCompleted`, `ReconciliationFailed`, `ReconciliationDecisionRecorded`,
`ExceptionCreated`, `AiQuestionAsked`, `AiToolInvoked`, `AiExplanationRequested`,
`AiExplanationFailed`, `AiAssistantFailed`.

Each entry carries a JSON payload plus `relatedEntityType` / `relatedEntityId`, enough to
reconstruct the exact decision sequence behind any exception. Payloads observed contain
IDs, statuses, and reason codes — **no raw amounts or PII**.

> **Gap — DEFERRED.** There is **no read endpoint** for audit logs `[CODE]`. Any audit
> timeline UI is blocked on backend work that is currently frozen. Do not design screens
> against imagined audit endpoints. See
> [product/02-scope-and-boundaries.md](../product/02-scope-and-boundaries.md#defer--real-work-but-not-now-needs-separate-approval).

---

## Not independently re-verified

`[ZIP]` flagged these and this phase did not re-trace them. Treat as open questions, not
as facts:

- Exact column ordering within the unique composite indexes
- Cascade-delete behaviour between `Batch` and children, and between `ReconciliationRun`
  and its results/exceptions
- Optimistic-concurrency configuration, if any
- Runtime query plans / N+1 behaviour under load
