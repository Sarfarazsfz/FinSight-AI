# Risks

Current, live risks only. Risks the historical baseline recorded that are now **closed**
are listed separately at the end so nobody re-opens a solved problem.

---

## Active risks

| # | Risk | Severity | Cause | Impact | Mitigation | Phase | Done when |
|---|---|---|---|---|---|---|---|
| 1 | **Frontend is uncommitted** | **HIGH** | `[CODE]` `frontend/` untracked — 50 clean files, 0 in git | Four delivered phases (F0–F3) exist only on disk; any mistake is unrecoverable | Commit and push the verified foundation | **F3 checkpoint** | `git status` clean, `frontend/` tracked, pushed |
| 2 | ~~SCSS → Tailwind migration~~ **CLOSED** | — | Superseded: the frontend was rebuilt from scratch rather than migrated | — | No migration was performed; F0 started clean on Tailwind v4 | F0 ✅ | Zero `.scss`; both builds clean |
| 3 | **Documentation drifts again** | HIGH | Docs previously lived outside the repository and fell ~4 phases stale | Decisions made against false assumptions | Single in-repo `docs/`; archive is read-only; anti-duplication rules; contract regenerated from source | P1 ✅ | Ongoing discipline |
| 4 | **Rebuild produces another generic result** | MED-HIGH | Visual quality is subjective and easy to defer | The stated reason for the rebuild goes unaddressed | "Ledger, not dashboard" north star; explicit DO-NOT list; visual QA as a hard gate with a template-or-ledger judgement | every F phase | Visual QA passes each phase |
| 5 | **Throughput unmeasured while named in the bar** | MED | `[CODE]` No instrumentation; backend frozen | An explicit evaluation criterion has no evidence | Decide explicitly before submission: measure via an approved backend change, or state plainly it was not measured. **Never estimate** | pre-submission | A real figure exists, or the absence is stated |
| 6 | **Scope creep into the frozen backend** | MED | Frontend work will surface tempting backend gaps | Destabilises a verified 153/153 backend late | Backend freeze; deferred items require separate approval | ongoing | No backend file changes in any frontend phase |
| 7 | **Tailwind v4 + Angular 20 integration friction** | MED | Tailwind v4's `@theme` model is newer | P2 stalls | Prove the integration in isolation at the start of P2 before authoring tokens | P2 | Build succeeds with tokens resolving |
| 8 | **Timeline** | MED | `[OFFICIAL WEB]` Applications close 5 September | Incomplete submission | P0–P11 is the credible minimum; P12–P13 are high value; P14–P17 are polish and deliverables | ongoing | Reassess at each checkpoint |
| 9 | **Video and "what broke" not produced** | MED | `[OFFICIAL WEB]` Both are required; neither exists | Incomplete submission regardless of code quality | Scheduled in P17; source material already documented | P17 | Video recorded, write-up complete |
| 10 | **Demo depends on live AI** | LOW-MED | Two external providers | A live outage during the demo | `[CODE]` 503 already returned; design and **rehearse** the unavailable state — it becomes a demonstration of the trust model | P11, P17 | 503 path rehearsed |
| 11 | **Two type families hurt coherence** | LOW-MED | `[ZIP]` proposed a serif pairing | Visual inconsistency | Deferred; single family until proven | — | Decision recorded |
| 12 | **Audit capability absent** | LOW | `[CODE]` No read endpoint | A real capability cannot be shown | State the absence honestly; do not fake a nav item | — | Documented as deferred |
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

**Frontend absence** is no longer a blocker either — a foundation exists; the risk has
changed shape from "nothing to show" to "what exists is not good enough yet", which is
risk #4.
