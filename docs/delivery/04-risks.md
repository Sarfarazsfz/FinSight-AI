# Risks

Current, live risks only. Risks the historical baseline recorded that are now **closed**
are listed separately at the end so nobody re-opens a solved problem.

---

## Active risks

| # | Risk | Severity | Cause | Impact | Mitigation | Phase | Done when |
|---|---|---|---|---|---|---|---|
| 1 | **Full application not yet committed** | **HIGH** | `[CODE]` 73 new untracked files (frontend, backend, migrations, tests) + 84 modified tracked files; all exist on disk and build/test clean | The entire feature set exists only locally; a push without staging all files would break a fresh clone | Use `git add -A` (not `git add -u`) before the final commit to stage all 73 new files alongside the 84 tracked-but-modified ones | **final commit** | `git status` clean, all files tracked and pushed |
| 2 | ~~SCSS → Tailwind migration~~ **CLOSED** | — | Superseded: the frontend was rebuilt from scratch rather than migrated | — | No migration was performed; F0 started clean on Tailwind v4 | F0 ✅ | Zero `.scss`; both builds clean |
| 3 | **Documentation drifts again** | HIGH | Docs previously lived outside the repository and fell ~4 phases stale | Decisions made against false assumptions | Single in-repo `docs/`; archive is read-only; anti-duplication rules; contract regenerated from source | P1 ✅ | Ongoing discipline |
| 4 | **Rebuild produces another generic result** | MED-HIGH | Visual quality is subjective and easy to defer | The stated reason for the rebuild goes unaddressed | "Ledger, not dashboard" north star; explicit DO-NOT list; visual QA as a hard gate with a template-or-ledger judgement | every F phase | Visual QA passes each phase |
| 5 | **Throughput named in the bar** | LOW | `[CODE]` Resolved — `durationMs`/`recordsPerSecond` computed server-side from persisted run timestamps and shown on the Run Workspace | An explicit evaluation criterion now has real evidence | Quote only the on-screen figure. It is one wall-clock run measurement — **never** call it a benchmark, and never claim cold/warm | closed | A real figure is displayed and labelled honestly |
| 6 | **Scope creep into the frozen backend** | MED | Frontend work will surface tempting backend gaps | Destabilises the verified backend (401 tests: 350 passed · 51 skipped · 0 failed) | Backend freeze; deferred items require separate approval | ongoing | No unauthorized backend file changes in any frontend phase |
| 7 | **Tailwind v4 + Angular 20 integration friction** | MED | Tailwind v4's `@theme` model is newer | P2 stalls | Prove the integration in isolation at the start of P2 before authoring tokens | P2 | Build succeeds with tokens resolving |
| 8 | **Timeline** | MED | `[OFFICIAL WEB]` Applications close 5 September | Incomplete submission | P0–P11 is the credible minimum; P12–P13 are high value; P14–P17 are polish and deliverables | ongoing | Reassess at each checkpoint |
| 9 | **Video and "what broke" not produced** | MED | `[OFFICIAL WEB]` Both are required; neither exists | Incomplete submission regardless of code quality | Scheduled in P17; source material already documented | P17 | Video recorded, write-up complete |
| 10 | **Demo depends on live AI** | LOW-MED | Two external providers | A live outage during the demo | `[CODE]` 503 already returned; design and **rehearse** the unavailable state — it becomes a demonstration of the trust model | P11, P17 | 503 path rehearsed |
| 11 | **Two type families hurt coherence** | LOW-MED | `[ZIP]` proposed a serif pairing | Visual inconsistency | Deferred; single family until proven | — | Decision recorded |
| 12 | ~~**Audit capability absent**~~ **CLOSED** | — | `[CODE]` Resolved — `GET /api/reconciliation/runs/{runId}/audit` reads the existing `audit_logs` store (read-only, ownership-scoped, no create/update/delete endpoint exists) | A real capability now exists and is shown, in the Run Workspace's "Audit evidence" section | Evidence about a run's execution only — never a second source of financial truth; match status/rate/exceptions remain whatever the summary and Ground Truth report | closed | Endpoint and UI verified live, with cross-user access correctly rejected |
| 13 | **Nullable fields mishandled** | LOW | `matchRate`, `strategyUsed`, all `ai*` fields | Misleading UI — `0%` for "not yet run" is a lie | Enumerated in the API contract; per-phase acceptance criteria | P8+ | Verified per phase |
| 14 | **Secrets exposure** | LOW | Provider keys and JWT secret exist locally | Credential compromise | Never print secret values — including "redacted" output, since a failed redaction leaks the real value. If it happens: stop, disclose plainly, rotate | ongoing | No secret ever printed |

---

## Closed risks — do not re-open

`[ZIP]` recorded these as BLOCKER or HIGH. All are fixed `[CODE]` and verified.

| Closed risk | Resolution |
|---|---|
| Incomplete exception list (orphan Bank/Settlement never classified) — **BLOCKER** | Union-of-three-key-sets iteration; orchestrator-level integration test asserting the completeness invariant |
| No `MissingPayment` scenario or fixture | Both added |
| Hardcoded generator output path | CLI override with relative fallback |
| No `.gitignore`; build artifacts tracked | `.gitignore` present and correct |
| CORS absent — frontend blocker | Policy registered, configuration-driven |
| Inconsistent error shapes | Uniform `ProblemDetails` everywhere |
| AI fallback retried the same failed provider | Fallback resolves by instance identity |
| Both-AI-down surfaced as a generic 500 | Mapped to 503 |
| Ground-truth harness invisible outside a console workflow | HTTP endpoint exposed |
| Determinism / invariant tests missing | All present and asserting |
| No ownership boundary — any authenticated user could read or ground-truth-verify any batch/run, including another user's exceptions and AI explanations (IDOR) — **HIGH** | Batch-rooted ownership (`created_by_user_id`, nullable/backfilled, safe default-deny) enforced ahead of every batch, run, result, exception, ground-truth, and Finance Assistant endpoint; cross-user access returns 404 indistinguishable from not-found. Single-owner boundary, not enterprise multi-tenancy — no org/team/role-sharing model exists |
| `POST /api/auth/forgot-password` had no abuse/rate-limit protection — **MED** | In-process limiter checked before any account lookup: 5 requests/15 min per normalized email, 20/15 min per client IP; 429 with `Retry-After` beyond that, generic message, anti-enumeration behavior unchanged. Single-instance protection only — not distributed, not a DDoS solution |
| No provider call in the AI path (Finance Assistant, AI explanation; Gemini/OpenAI/NVIDIA) had any timeout — a slow/unresponsive provider could leave the request pending indefinitely, with no fallback and no audit event ever reached — **HIGH** | Every provider call now runs under a bounded 30s timeout (`ProviderFallbackChain`); a timed-out provider is treated as an ordinary failure and the chain falls through to the next configured provider, or reports a normal bounded failure. Live-verified: an AI explanation call that previously did not return within 30s now succeeds in ~9s once a working provider is tried; a single-effective-provider Finance Assistant failure, previously a generic 500 (`FinanceAssistantProviderRouter`'s single-failure path used a plain `InvalidOperationException`, unlike its already-correct `AiProviderRouter` sibling), now correctly returns the calm, tested 503 |

**Frontend absence** is no longer a blocker either — a foundation exists; the risk has
changed shape from "nothing to show" to "what exists is not good enough yet", which is
risk #4.
