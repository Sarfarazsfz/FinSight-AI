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
| **Audit-log read endpoint** | ✅ | KEEP | `GET /api/reconciliation/runs/{runId}/audit`; surfaced in `AuditEvidencePanel` |
| **Throughput instrumentation** | ✅ | KEEP | `durationMs` + `recordsPerSecond` on summary response, server-authoritative; displayed on Run Workspace |
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
| Landing page | ✅ editorial hero, live reconciliation mockup, KPI display, CTAs | **KEEP** | |
| Login | ✅ real backend auth, ProblemDetails errors | **KEEP** | |
| Signup | ✅ public — always `User` role, duplicate rejected | **KEEP** | |
| Forgot password | ✅ anti-enumeration response, in-memory rate limiting, `Retry-After` | **KEEP** | |
| Reset password | ✅ tokenized (256-bit RNG, SHA-256 stored), single-use, 60-minute window | **KEEP** | |
| Batch history | ✅ fetches real paged data from backend, pagination, run action | **KEEP** | |
| Batch upload + `errors[]` UX | ✅ three-slot intake; per-row, per-field structured errors displayed | **KEEP** | |
| Run workspace (match rate + 5-count breakdown) | ✅ `/runs/:runId` — match rate, Matched/Mismatched/Missing/Duplicate/Unresolved, run performance | **KEEP** | |
| Results table | ✅ paginated, status-badged | **KEEP** | |
| Result evidence (3-source, routed page) | ✅ `/runs/:runId/results/:resultId` — Payment · Bank · Settlement with differing field marked | **KEEP** | |
| Exception queue + detail | ✅ paginated exception queue + routed exception detail page | **KEEP** | |
| AI explanation panel + 503 state | ✅ below evidence; designed 503 ("AI explanation unavailable. Reconciliation result is unaffected.") | **KEEP** | |
| Finance Assistant + tool trail | ✅ right-side rail (≥1024px) / bottom drawer (mobile); tool-call chips; scope guard; 503 state | **KEEP** | |
| Ground-truth verification | ✅ `/runs/:runId/verify` — operator-supplied CSV → backend-authoritative PASS/FAIL | **KEEP** | |
| Audit evidence | ✅ `AuditEvidencePanel` embedded in Run Workspace; newest-first, paginated | **KEEP** | |
| Run performance / throughput | ✅ `durationMs` + `recordsPerSecond` from persisted timestamps; displayed on Run Workspace | **KEEP** | |
| Synthetic Data Lab | ✅ `/data-generator` — 10 modes · 3 intensities · 4 sizes · seeded; independent ground truth | **KEEP** | |
| Icon system | ❌ inline SVG only — no external icon library | — | deferred: no consumer requiring a library |

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

**All major backend and frontend capabilities are now implemented.** The one remaining
deferred item is the exception-resolution write path, which is intentionally excluded to
avoid changing data-model semantics before the submission freeze.
