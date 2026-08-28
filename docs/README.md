# FinSight Documentation

**FinSight — Financial Reconciliation Intelligence**

> Find what doesn't reconcile. Understand why. Prove the result.

This directory is the **single authoritative documentation set** for FinSight. If a
statement here conflicts with any other document, file, chat log, or archived material,
this directory wins — except where a document explicitly defers to source code.

---

## Source hierarchy

Every non-obvious claim in these documents carries a source tag. When sources disagree,
resolve in this order and **record the conflict rather than silently merging it**.

| Tag | Meaning | Authority |
|---|---|---|
| `[CODE]` | Verified by reading current repository source | **Wins for current implementation behaviour** |
| `[OFFICIAL WEB]` | Official Razorpay buildathon sources | **Wins for current challenge requirements** |
| `[ZIP]` | Pre-frontend documentation baseline (see `archive/`) | Historical baseline; stale items must be marked |
| `[RECOMMENDATION]` | Considered professional judgement, not fact | Explicitly non-authoritative; argue with it freely |

**Never** document a capability that does not exist in `[CODE]`. Never fabricate a
metric, customer, benchmark, or accuracy percentage. Any number quoted in a demo must be
read live from a real run.

---

## Document map

| Area | Documents |
|---|---|
| **Product** | [Overview](product/01-overview.md) · [Scope & boundaries](product/02-scope-and-boundaries.md) · [Challenge requirements](product/03-challenge-requirements.md) · [Feature matrix](product/04-feature-matrix.md) |
| **Architecture** | [System](architecture/01-system-architecture.md) · [Backend](architecture/02-backend-architecture.md) · [Reconciliation engine](architecture/03-reconciliation-engine.md) · [Data model](architecture/04-data-model.md) · [Ground truth](architecture/05-ground-truth-evaluation.md) · [AI](architecture/06-ai-architecture.md) · [Auth & security](architecture/07-auth-and-security.md) |
| **API** | [Contract](api/01-contract.md) · [Error handling](api/02-error-handling.md) |
| **Frontend** | [Architecture](frontend/01-architecture.md) · [Information architecture](frontend/02-information-architecture.md) · [Routes & screens](frontend/03-routes-and-screens.md) · [API integration](frontend/04-api-integration.md) |
| **Design** | [Design system](design/01-design-system.md) · [Icon system](design/02-icon-system.md) · [Landing page](design/03-landing-page.md) · [Application UX](design/04-application-ux.md) · [AI UX](design/05-ai-ux.md) · [Accessibility & responsive](design/06-accessibility-and-responsive.md) |
| **Quality** | [Testing](quality/01-testing.md) · [Visual QA](quality/02-visual-qa.md) · [Performance](quality/03-performance.md) |
| **Delivery** | [Roadmap](delivery/01-roadmap.md) · [Demo runbook](delivery/02-demo-runbook.md) · [Judge strategy](delivery/03-judge-strategy.md) · [Risks](delivery/04-risks.md) |
| **Setup** | [Local development](setup/01-local-development.md) |
| **Decisions** | [ADR index](adr/README.md) — ADR-001…010 |
| **History** | [Archive](archive/README.md) — the pre-frontend `[ZIP]` baseline |

---

## Anti-duplication rules

These exist because the project previously had documentation living outside the
repository, which drifted several implementation phases out of date.

1. **One fact, one home.** If a fact belongs in two documents, one of them links instead
   of restating.
2. **`api/01-contract.md` is derived from controller source.** Never hand-maintain it as
   an independent truth — regenerate it from `[CODE]` when the API changes.
3. **The archive is read-only history.** Never edit, partially merge, or re-import
   `archive/`. It records what was believed before, not what is true now.
4. **No successor document may contradict another** without an ADR recording the
   decision and its consequences.
5. **Stale is worse than missing.** A document that describes a fixed defect as open is
   actively harmful — mark resolutions explicitly.

---

## Implementation governance (mandatory)

Frontend work proceeds in small, independently verifiable phases. **Never implement the
entire frontend at once.**

```
PLAN → REVIEW → APPROVAL → IMPLEMENT SMALL PHASE
     → TEST → VISUAL QA → GIT CHECKPOINT → NEXT PHASE
```

Before every implementation phase:

1. Inspect the current state of the files involved.
2. Produce a written pre-implementation change plan.
3. **Wait for explicit approval.**
4. Implement only the approved scope — nothing adjacent, nothing "while we're here".
5. Test.
6. Visually inspect.
7. Report what was done, what passed, and what did not.
8. **Stop.**

A phase is complete only when it meets the full acceptance model in
[delivery/01-roadmap.md](delivery/01-roadmap.md#phase-acceptance-model) — compiling is
not completion.

### Backend freeze

The backend is **frozen** at 153/153 passing tests `[CODE]`. Do not change backend code
for frontend convenience. If the frontend genuinely needs a backend capability that does
not exist (the audit-log endpoint and throughput instrumentation are the two known
cases), that is a **separate, separately-approved** piece of work — never smuggled into
a frontend phase.

### Claude Design

Claude Design is **visual exploration only**: concepts, design-system exploration,
mockups, interaction exploration, visual QA. Any React artifact it produces is a
throwaway sketch and **must never become production code**. The production frontend is
Angular 20 and stays Angular 20. See [ADR-008](adr/README.md#adr-008-tailwind-css-v4-with-css-custom-properties).
