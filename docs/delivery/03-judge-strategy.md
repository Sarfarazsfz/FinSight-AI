# Evaluation Strategy

`[RECOMMENDATION]` throughout unless tagged otherwise. **No official numeric scoring
rubric is published anywhere** — nothing here should be presented as official criteria.

---

## What is actually known `[OFFICIAL WEB]`

| Known | Implication |
|---|---|
| Bar: *"throughput plus measured accuracy plus an honest exception list"* | Three concrete, checkable properties |
| *"One cherry-picked match proves nothing"* | A curated example is a liability |
| *"Verification capacity, not generation speed, is the bottleneck"* | Verification is the theme, not a footnote |
| Judged: whether AI/agents were applied **appropriately** | Bounded, grounded, transparent tool use — not volume |
| Judged: **how failures were identified at runtime and graceful fallbacks engineered** | Degradation paths are directly assessed |
| Required: public repo · 5-minute pitch video showing architecture · what broke and how you recovered | Deliverables, not optional polish |

---

## Honest self-assessment

| Dimension | Assessment |
|---|---|
| Problem choice | **Strong** — real, well-scoped finance-ops pain |
| Track alignment | **Strong** — the loop maps to every stage of the brief |
| Correctness | **Strong** — the known completeness defect is fixed and locked down by an orchestrator-level test |
| Verification | **Strongest asset** — independent ground truth, comprehensive comparator, HTTP-exposed |
| Exception completeness | **Strong** — complete by construction, asserted as an invariant |
| AI quality | **Strong** — grounded, bounded, non-recursive by construction |
| Agent/tool use | **Strong for single-step**; deliberately does not do multi-step chains ([ADR-004](../adr/README.md#adr-004-bounded-two-call-ai-interaction-instead-of-recursive-agent-loops)) |
| Failure handling | **Strong** — provider fallback by instance identity; both-down → 503; designed UI state |
| Trust model | **Strong structurally**; needs the UI to make it visible |
| Architecture | **Strong** — verified Clean Architecture, no god objects, no speculative abstraction |
| Testing | **Strong** — 836 tests: backend 401 total (350 passed · 51 skipped¹ · 0 failed) + frontend 435 (435 passed · 0 failed). The invariants that matter — orchestrator completeness, provider fallback, ground-truth independence, ownership — are all covered |
| **Throughput evidence** | **Present** — measured server-side from each run's persisted start/completion timestamps and surfaced on the Run Workspace. A single wall-clock run measurement, explicitly not a benchmark |
| **UX** | **Complete** — full end-to-end loop: landing · login · signup · forgot/reset-password · batch upload with structured `errors[]` · run workspace · results · exception detail · AI explanation · Finance Assistant · ground-truth verification · audit evidence · Synthetic Data Lab |
| **Demo** | **Runnable** — every route in [02-demo-runbook.md](02-demo-runbook.md) is implemented, including `/runs/:runId/verify`. Dataset regenerates from a fixed seed |
| Repository | **Complete** — full stack committed; backend + frontend + migrations + tests. Use `git add -A` (not `git add -u`) before the final commit to include all 73 new files alongside the 84 modified tracked files |

> ¹ The 51 skipped backend tests are PostgreSQL integration tests gated on `FINSIGHT_TEST_CONNECTION`. They skip cleanly with a documented reason when that env var is not set; they pass when it is. See [quality/01-testing.md](../quality/01-testing.md#environment-requirement). They are not failures.

---

## What makes this memorable

`[RECOMMENDATION]` A live, unstaged batch reconciled and then **independently verified**
in front of the evaluator — not claimed, shown. Very few submissions can produce a
measured accuracy number against labels that existed before their system ran.

## What makes it read as real engineering

Reason-coded, evidence-backed classification · schema with genuine precision, index, and
uniqueness discipline · invariants asserted as tests, not assumed · an audit trail that
reconstructs decisions · a bounded AI design with a stated, defended tradeoff · and a
team that found and disclosed its own completeness bug rather than hiding it.

## What could go wrong

| Risk | Mitigation |
|---|---|
| A defect found live rather than disclosed | Disclose both recoveries deliberately |
| AI failure surfacing as a raw error | `[CODE]` Already returns 503; design and rehearse the UI state |
| A non-reproducible demo | Generator path fixed; rehearse twice |
| A fabricated or remembered number | Read every figure from the live screen |
| **Skipped integration tests misread as failures** | Documented clearly: 51 skipped = PostgreSQL gated, not broken; 0 tests fail |

## The strongest technical conversation-starter

`[RECOMMENDATION]` The ground-truth comparator. It is an unusual asset, and "how does
that actually work?" is the best possible follow-up question — it leads directly to
[ADR-003](../adr/README.md#adr-003-ground-truth-is-independent-from-runtime-reconciliation-output),
the independence property, and the accumulate-all-failures design.

---

## Positioning

**Lead with:** deterministic reconciliation that measures and proves itself.
**Follow with:** evidence-first investigation.
**Then:** AI that explains rather than decides.
**Close on:** independent verification.

**Never lead with AI.** In a track whose stated bottleneck is *verification capacity*,
leading with generation is a positioning error. The restraint is the differentiator.

## Overall recommendation: MODIFY, not rebuild

`[CODE]` The backend architecture, reconciliation design, AI bounding, schema, and audit
design are sound by direct inspection — and every defect the historical baseline recorded
is fixed. The remaining work is the **presentation layer** and the **deliverables**
(video, what-broke write-up, throughput decision). Rebuilding sound parts would spend the
timeline on work that is not needed.
