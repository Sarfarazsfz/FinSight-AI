# Testing and Quality Engineering

---

## Backend — frozen

`[CODE]` **153 `[Test]` methods across 34 test files.** Reported passing 153/153 in a
provisioned environment.

> `[ZIP]` doc 15 states "~100 tests across 22 files" — **stale**.

### Layers

| Layer | Meaning | Examples |
|---|---|---|
| **Unit** | One class, hand-built inputs, no I/O | `MatchClassifierTests`, strategy tests |
| **Integration** | Real collaborators via `PostgresIntegrationFixture` | `ReconciliationPipelineIntegrationTests`, `ReconciliationOrphanReferenceIntegrationTests` |
| **API / HTTP** | Real pipeline via `WebApplicationFactory<Program>` — the only way to prove `[Authorize]` and CORS | `BatchesAuthorizationTests`, `GlobalExceptionHandlerTests`, `GroundTruthVerificationEndpointTests` |
| **Provider** | AI routing and fallback | `AiProviderRouterTests`, `FinanceAssistantProviderRouterTests` |
| **Evaluation** | Ground-truth generation and comparison | `GroundTruthComparatorTests`, `GroundTruthGeneratorTests` |

### Environment requirement

Database-backed tests require **`FINSIGHT_TEST_CONNECTION`** pointing at a dedicated
test database. Without it, every such fixture fails at `OneTimeSetUp` with a clear,
identical environment error — **not** a logic failure.

`[RECOMMENDATION]` When triaging a failing run, first confirm whether every failure
message is that environment error. If so, the failure is environmental. Grep before
debugging.

### The invariants under test

1. `matched + mismatched + missing + duplicate + unresolved == totalUnits`
2. Exactly one exception per non-`Matched` result
3. `matchRate == round(matched / totalUnits * 100, 2)`
4. The same batch reconciled twice yields identical status and reason-code assignments
5. A validation failure persists **nothing** — no batch, no records, no audit rows
6. Orphan Bank/Settlement references produce `SOURCE_ABSENT_PAYMENT` end to end
7. Error responses never leak exception text or provider details

### The lesson worth keeping

> Unit-testing a decision function does **not** prove the orchestration layer can ever
> construct the inputs that function was designed for.

`[ZIP]` doc 15's central point, preserved. The orphan defect passed roughly a hundred
tests because `MatchClassifier` was unit-tested with a hand-built payment-absent evidence
object while nothing tested whether the orchestrator could *produce* one. It could not.

The defect is closed `[CODE]`, but the lesson is permanent: when a decision function
gains a branch, separately ask **what constructs the input that reaches it, and is that
path tested?**

---

## Frontend testing strategy

Proportionate. Test what breaks silently; skip what visual QA catches immediately.

### Unit — pure functions

| Target | Why |
|---|---|
| `errors[]` grouping by source | Real logic with ordering and edge cases |
| Status → design-token mapping | A wrong mapping is silently wrong |
| Formatters (date, amount, percentage) | Locale and null handling |
| Pagination arithmetic | Off-by-one bugs are invisible until page 3 |

### Component — `TestBed`

| Target | Why |
|---|---|
| Four-state machines (loading/empty/error/loaded) | The most commonly skipped path |
| `authGuard` | Security-relevant |
| `authInterceptor` / `errorInterceptor` | 401 handling is security-relevant |
| Dropzone interactions | Drag, select, remove, keyboard |
| Drawer focus management | Trap, Escape, focus restoration |

### Contract

Typed models asserted against **real response shapes**, not hand-written fixtures that
can drift. When a model and the API disagree, the test must fail.

### Manual integration — per phase, against the live API

| Path | Must verify |
|---|---|
| Login | Success, 401, guard redirect |
| Upload | Success, **400 with `errors[]`**, missing-field 400 |
| Run | Creation, summary, nullable `matchRate` |
| Results | Pagination, evidence drawer |
| Exceptions | Queue, detail, prev/next |
| AI | Success and **503** |
| Verification | PASS and FAIL |

### Not built

**No E2E framework.** `[RECOMMENDATION]` Setup and maintenance cost outweighs the benefit
at this timeline and team size; per-phase manual integration against the real API covers
the same ground with less ceremony. Revisit only if the product outlives this phase of
work.

---

## Definition of a passing phase

Compiling is not passing. See the full acceptance model in
[delivery/01-roadmap.md](../delivery/01-roadmap.md#phase-acceptance-model). In summary,
a phase passes only when intended functionality works, integration works, tests pass,
**visual QA passes**, responsive behaviour is acceptable, accessibility requirements are
met, loading/empty/error states exist, and the phase's stated acceptance criteria are
satisfied.

## Backend change policy

The backend is frozen. If frontend work appears to require a backend change, that is a
**separate, separately-approved** piece of work — never bundled into a frontend phase.
The two known cases are the audit-log read endpoint and throughput instrumentation.
