# Feature Matrix

Consolidated capability status. `[CODE]` verified against current source; supersedes
`[ZIP]` doc 21, whose statuses are several implementation phases out of date.

Legend — **KEEP** (exists, correct, unchanged) · **REBUILD** (exists, presentation being
rewritten) · **BUILD** (does not exist yet, planned) · **DEFER** (real, needs separate
approval) · **DO NOT BUILD**.

---

## Backend capabilities — frozen

| Capability | `[CODE]` | Action | Notes |
|---|---|---|---|
| JWT authentication | ✅ | KEEP | Fail-fast config validation, 30s clock skew |
| CORS for the Angular origin | ✅ | KEEP | `[ZIP]` recorded this as absent — **resolved** |
| Batch ingestion (multipart, 3 CSVs) | ✅ | KEEP | |
| Row-level validation | ✅ | KEEP | |
| **Structured validation errors** (`errors[]`) | ✅ | KEEP | Undocumented in `[ZIP]` — new |
| Batch history (paged) | ✅ | KEEP | Undocumented in `[ZIP]` |
| Batch detail | ✅ | KEEP | Undocumented in `[ZIP]` |
| Reconciliation run creation | ✅ | KEEP | |
| **Union-of-three-sources reconciliation** | ✅ | KEEP | `[ZIP]` recorded a BLOCKER defect here — **resolved** |
| Reason-coded classification | ✅ | KEEP | 5 statuses, 11 reason codes |
| Run details / summary | ✅ | KEEP | |
| Results (paged) | ✅ | KEEP | |
| Transaction evidence (3-source) | ✅ | KEEP | |
| Exceptions (paged) + single exception | ✅ | KEEP | |
| AI explanation | ✅ | KEEP | |
| Finance Assistant (bounded, 4 tools) | ✅ | KEEP | |
| AI provider routing + fallback | ✅ | KEEP | `[ZIP]` recorded a same-provider-retry defect — **resolved** |
| Graceful both-providers-down → 503 | ✅ | KEEP | `[ZIP]` recorded this as a gap — **resolved** |
| Uniform `ProblemDetails` errors | ✅ | KEEP | `[ZIP]` recorded two competing shapes — **resolved** |
| **Ground-truth verification over HTTP** | ✅ | KEEP | `[ZIP]` specified `GET`; actual is **`POST`** — see [api/01](../api/01-contract.md) |
| Audit trail write path | ✅ | KEEP | Rich, correlated |
| **Audit-log read endpoint** | ❌ | DEFER | Blocks any audit UI |
| **Throughput instrumentation** | ❌ | DEFER | `[OFFICIAL WEB]` names throughput in the bar |
| Exception resolution write path | ❌ | DEFER | Would change data-model semantics |

---

## Frontend capabilities

| Capability | `[CODE]` today | Action | Phase |
|---|---|---|---|
| Typed API models incl. `errors[]` | ✅ | **KEEP** | — |
| `AuthStore` (signals + localStorage) | ✅ | **KEEP** | — |
| Auth guard, HTTP interceptors | ✅ | **KEEP** | — |
| `AuthApi` | ✅ | **KEEP** | delivered (F2) |
| Environments + `fileReplacements` | ✅ | **KEEP** | — |
| Lazy route shape | ✅ | **KEEP** | delivered (F3) |
| Design token system | ✅ Tailwind v4 `@theme` + CSS vars | **KEEP** | delivered (F1) |
| Icon system | ❌ none yet — zero SVG, zero glyphs | **BUILD** → Lucide | deferred until a real consumer |
| Application shell | ✅ guarded shell, nav, logout | **KEEP** | delivered (F3) |
| Landing page | ❌ not built — `/` redirects by session state | **BUILD** | later phase |
| Login | ✅ real backend auth, ProblemDetails errors | **KEEP** | delivered (F3) |
| Batch history | ❌ entry page only; **fetches nothing** | **BUILD** | later phase |
| Batch upload + `errors[]` UX | ❌ not built | **BUILD** | later phase |
| Run overview (match rate) | ❌ not built | **BUILD** | later phase |
| Results table | ❌ not built | **BUILD** | later phase |
| Evidence comparison (drawer) | ❌ not built | **BUILD** | later phase |
| Exception queue + detail | ❌ not built | **BUILD** | later phase |
| AI explanation panel + 503 state | ❌ not built | **BUILD** | later phase |
| Finance Assistant + tool trail | ❌ not built | **BUILD** | later phase |
| Independent verification | ❌ not built | **BUILD** | later phase |
| Audit timeline | ❌ | **DEFER** | blocked on backend |

---

## Resolved-since-baseline summary

`[ZIP]` listed six items as BLOCKER or CONFIRMED DEFECT. **All six are fixed** `[CODE]`:

| `[ZIP]` defect | Resolution |
|---|---|
| Orphan Bank/Settlement records never classified (BLOCKER) | Union of all three key sets in the orchestrator |
| …and never proven by a test | Dedicated orchestrator-level integration test asserting the completeness invariant |
| `MissingPayment` generator scenario absent | Added, with an edge fixture |
| Hardcoded generator output path | CLI override with a relative fallback |
| CORS absent (frontend blocker) | Policy registered |
| Inconsistent error shapes | Uniform `ProblemDetails` |
| `AiProviderRouter` retried the same failed provider | Fallback resolves by instance identity |
| Both-AI-down surfaced as a generic 500 | Mapped to 503 |

**Two genuine gaps remain**: the audit-log read endpoint and throughput instrumentation.
Neither is a frontend problem; both are deferred backend work.
