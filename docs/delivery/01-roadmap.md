# Implementation Roadmap

Small, independently verifiable phases in dependency order. **There is no single "build
the frontend" phase**, by design.

---

## Phase naming

Two numbering schemes appear in this documentation set. They are **not** two names for
the same work:

| Series | Meaning |
|---|---|
| **P0–P17** | The **product/planning** sequence in this document. Written before the frontend was rebuilt; it describes intended scope and ordering. |
| **F0–Fn** | The **actual frontend implementation** phases executed after the rebuild. Each is separately planned, approved, verified and reported. |

The frontend was rebuilt from scratch rather than migrated, so the executed F-series does
not map one-to-one onto the planned P-series:

| Executed | Delivered | Planned equivalent |
|---|---|---|
| **F0** | Angular 20 scaffold + verified Tailwind v4 toolchain | part of P2 |
| **F1** | FinSight design tokens, typography, WCAG AA contrast | rest of P2 |
| **F2** | Typed HTTP/auth infrastructure, interceptors, guard, `ProblemDetails` | *no P equivalent* |
| **F3** | Login, authenticated application shell, protected batches entry | P3 + P5 |

**Use F-numbers when referring to implemented work.** P-numbers are retained for the
planning narrative and are **not** renumbered — the historical references throughout
these documents remain valid as planning history. ADR numbering is unaffected.

---

## Phase acceptance model

A phase is **not** complete because code compiles, a route exists, or an API returns 200.

A phase is complete only when **all** of the following hold:

1. The intended functionality works
2. Integration with the real API works
3. Tests pass
4. **Visual QA passes** — see [quality/02-visual-qa.md](../quality/02-visual-qa.md)
5. Responsive behaviour is acceptable at 1280 · 768 · 375
6. Accessibility requirements are met
7. **Loading, empty, and error states exist and have been viewed**
8. The phase's stated acceptance criteria are satisfied
9. A report has been produced, including anything that failed or could not be verified

Then, and only then: **git checkpoint → stop → await approval for the next phase.**

---

## Ordering rationale

The order below is **not** the priority order from
[frontend/02-information-architecture.md](../frontend/02-information-architecture.md#ux-priority--validated-not-assumed).
Priority ranks value; this ranks **dependencies**.

- **Checkpoint first, always.** The frontend is still uncommitted after F0–F3. Working
  against uncommitted code makes any mistake unrecoverable.
- **Design system before screens.** Building screens first means building them twice.
- **Shell and landing before feature screens.** They exercise the design system across
  both the public and dense surfaces and surface token gaps early, cheaply.
- **Evidence (P9) before exceptions (P10).** The evidence drawer is the reusable
  primitive the exception screen consumes.
- **AI (P11) after exceptions.** It must render *below* real evidence; building it first
  would mean designing it against a placeholder.
- **Accessibility as a sweep (P14).** Per-phase a11y is still mandatory; the sweep
  catches cross-screen issues. Auditing screens that still exist is cheaper than
  auditing screens about to be replaced.

---

## Phases

Legend — **Scope**: S (≤½ day) · M (~1 day) · L (>1 day).

### P0 — Local safety checkpoint · Scope S · **do this first**
**Objective** Commit the existing frontend as-is.
**Value** Rollback safety; satisfies the public-repo requirement for work already done.
**Depends** nothing.
**Affects** `frontend/**`, `.claude/`.
**Work** `git add frontend/ .claude/` → commit → push. No source edits.
**Accept** `git status` clean · `frontend/` tracked · pushed · build still succeeds.
**Risk** None. **Rollback** N/A — this *is* the rollback point.

### P1 — Documentation freeze · Scope M · ✅ **complete**
**Objective** Authoritative `docs/` in-repo.
**Value** One source of truth; no more drift.
**Depends** P0.
**Affects** `docs/**` only.
**Accept** All 25 required topics covered · every conflict recorded and resolved by
source hierarchy · resolved defects marked RESOLVED · no duplicate sources of truth ·
archive marked historical.
**Risk** Low. **Rollback** revert the docs commit.

### P2 — Design system · Scope L · **highest-risk phase**
**Objective** Install Tailwind v4 + Lucide; author the `@theme` token layer; build shared
primitives; **delete all SCSS**.
**Value** Every later phase inherits a coherent visual language.
**Depends** P1.
**Affects** `package.json`, `angular.json`, `styles/**`, `shared/ui/**`, all 17 `.scss`
files (deleted).
**UX** Token vocabulary; primitive specs; icon vocabulary fixed.
**Impl** Tailwind v4 + `lucide-angular` + CDK · `@theme` tokens · `<app-icon>` wrapper ·
button, badge, card, input, table, drawer, skeleton, empty-state, error-state, stat,
pagination.
**Test** Unit tests for status→token mapping and formatters.
**Visual QA** Every primitive in every state; **zero `.scss` files**; **zero banned
glyphs** (grep).
**A11y** Focus treatment; icon `aria-hidden` defaults; contrast for all six status pairs.
**Accept** Tailwind builds · no `.scss` remains · all tokens are CSS custom properties ·
Lucide renders · zero emoji/entity/ad-hoc SVG · primitives render in all states · build
0 warnings · bundle not regressed.
**Risk** **High** — single largest change. **Rollback** revert to P0/P1 checkpoint.

### P3 — Application shell · Scope M
**Objective** Rebuild the authenticated shell; **strip every Track-04/Phase string**.
**Depends** P2. **Affects** `layout/app-shell/**`, `layout/marketing-shell/**`.
**Accept** Five-item rail, no more · responsive rail→top-bar · skip link · keyboard nav ·
**grep for "Track 04"/"Buildathon"/"Phase" returns nothing in `frontend/src`**.
**Risk** Low.

### P4 — Landing · Scope L
**Objective** Long-form public page per [design/03](../design/03-landing-page.md).
**Depends** P3. **Affects** `features/landing/**`.
**Accept** All 11 sections · **no fabricated metrics, customers, or claims** · no
buildathon language · `@defer` below fold · three viewports · one `<h1>`, correct heading
order · reduced-motion respected.
**Risk** Medium — most subjective phase; the "template or ledger?" judgement applies hardest.

### P5 — Authentication · Scope S
**Objective** Rebuild login presentation. Auth logic already correct — **do not rewrite it**.
**Depends** P3. **Affects** `features/auth/**`.
**Accept** Valid login → `/batches` · invalid → inline 401 `role="alert"` · loading state ·
labels/focus/Enter-submit · no sign-up or forgot-password affordance.
**Risk** Low.

### P6 — Batch history · Scope M
**Depends** P5. **Affects** `features/batches/batch-history-page/**`.
**Accept** All four states rendered and viewed · server-side pagination · newest-first ·
tabular numerals right-aligned · 401 redirects · launcher, not a dashboard.
**Risk** Low.

### P7 — Batch upload + validation errors · Scope L
**Depends** P6. **Affects** `features/batches/batch-upload-page/**`, `shared/ui/dropzone`.
**Accept** Three intake slots with drag-drop/browse/remove · stage progression visible ·
**400 renders grouped `errors[]` with row · field · message** · **`detail` never parsed** ·
success summary · keyboard-operable dropzones.
**Risk** Medium.

### P8 — RunShell + Run Overview · Scope L
**Objective** Shared run workspace; match-rate hero.
**Depends** P7. **Affects** `layout/run-shell/**`, `features/runs/overview/**`,
`ReconciliationApi`.
**Accept** Shell holds context across tabs (one summary fetch) · hero shows real
`matchRate` · **nullable `matchRate` handled** · five counts + status bar · three actions ·
sticky header · deep-linkable tabs.
**Risk** Medium-High — first genuinely new architecture since P2.

### P9 — Results + evidence drawer · Scope L
**Depends** P8. **Affects** `features/runs/results/**`, `shared/ui/data-table`, `drawer`.
**Accept** Paged results · status badges with labels · drawer opens with three-source
evidence · **CDK focus trap, Escape closes, focus restored** · differing field marked by
**label + icon**, not colour alone · missing source shows explicit "No record" · mobile
full-screen sheet.
**Risk** High — the reusable primitive everything downstream depends on.

### P10 — Exception investigation · Scope L · **hero experience**
**Depends** P9. **Affects** `features/exceptions/**`.
**Accept** Queue paginates and filters · detail shows **evidence first** · prev/next queue
navigation with position · honest empty state · complete by construction (no client-side
filtering that could hide a case) · `discrepancyDetail` available as a system record.
**Risk** High — receives the most interaction-design attention.

### P11 — AI explanation + 503 state · Scope M
**Depends** P10. **Affects** `features/exceptions/ai-panel/**`.
**Accept** Panel renders **below** evidence, visually subordinate · provider + timestamp
shown · `suggestedCategory` marked as suggested · **503 renders the designed unavailable
state with evidence fully intact** · AI never blocks evidence · text never rendered as HTML.
**Risk** Medium. `[OFFICIAL WEB]` The 503 state is directly judged — rehearse it.

### P12 — Finance Assistant · Scope M
**Depends** P11. **Affects** `features/assistant/**`, `FinanceAssistantApi`.
**Accept** Answer + **`toolsUsed[]` provenance chips** + `traceId` · input disabled while
pending · 503 handled · **not chat-bubble styled** · empty state suggests answerable
questions.
**Risk** Medium.

### P13 — Independent verification · Scope M · **the differentiator**
**Depends** P12. **Affects** `features/verify/**`.
**Accept** Upload or paste ground truth · **PASS** = confident, restrained "Independently
Verified" · **FAIL** lists **every** failure · expected-vs-actual shown either way ·
nothing fabricated, nothing hardcoded.
**Risk** Medium.

### P14 — Responsive + accessibility sweep · Scope M
**Depends** P13. **Affects** all.
**Accept** Full keyboard-only pass · AA contrast verified · reduced motion · table
semantics · three viewports · no horizontal body scroll anywhere.
**Risk** Low.

### P15 — Performance · Scope S
**Depends** P14.
**Accept** Production build 0 errors/0 warnings · bundle not regressed vs the 315.82 kB /
87.20 kB baseline · budgets reviewed **downward** post-Tailwind · no console errors.
**Risk** Low.

### P16 — Visual polish · Scope M
**Depends** P15.
**Accept** Spacing/type audit against tokens · consistent icon usage · motion purposeful
and reduced-motion-safe · **the "ledger, not dashboard" judgement passes**.
**Risk** Low.

### P17 — Demo hardening · Scope M
**Depends** P16. **Affects** `docs/delivery/**`.
**Accept** Demo runs twice with consistent results · video script covers architecture ·
**"what broke and how we recovered" documented with test evidence** · throughput decision
made explicitly (measure or state plainly that it was not measured).
**Risk** Medium — `[OFFICIAL WEB]` deliverables live here.

---

## Deferred — separate approval, backend unfreeze required

| Item | Note |
|---|---|
| Audit-log endpoint + timeline UI | `[CODE]` No read endpoint exists |
| Throughput instrumentation | `[OFFICIAL WEB]` Named in the bar — decide in P17 |

Neither may be smuggled into a frontend phase.

---

## Checkpoint strategy

One commit per phase, on `main`, with a descriptive message. Every phase is independently
revertable. If a phase fails acceptance, revert to the previous checkpoint rather than
patching forward under time pressure.
