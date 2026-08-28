# Documentation Archive

**HISTORICAL RECORD — NOT AUTHORITATIVE.**

---

## What this records

FinSight's documentation previously lived **outside the repository**, as a ZIP archive of
37 markdown files under `docs/`. That archive is referred to throughout the current
documentation set as `[ZIP]`.

| Property | Value |
|---|---|
| Archive | `FinSight-AI-docs.zip` |
| SHA-256 | `e4f03b199b372fc2eed6cc49234a4bcedb51d838db39419cc8d8e5e0652a6834` |
| Size | 77,313 bytes |
| Dated | 2026-08-27 |
| Contents | 37 files (+3 directory records), 1,710 lines |
| Structure | `docs/00-…` – `docs/27-…`, `docs/adr/`, `docs/architecture/` |

The archive files themselves are **not committed here** — they exist outside the
repository and were verified byte-for-byte during reconciliation. This document records
their status and disposition.

---

## Why it is not authoritative

The archive describes the repository as it was **before** several implementation phases.
Every defect it records as `BLOCKER` or `[CONFIRMED DEFECT]` has since been fixed, and
several capabilities now exist that it does not mention at all.

Treating it as current would actively cause harm — for example, it states that CORS is
absent (it is configured), that the exception list is incomplete (it is complete by
construction), and that ground-truth verification has no HTTP endpoint (it has one, with
a different method and shape than the archive proposed).

---

## Disposition of every archived document

| Archived document | Disposition | Superseded by |
|---|---|---|
| `README` | REPLACED | Repository root README (pending) |
| `00-EXECUTIVE-SUMMARY` | UPDATED | [product/01-overview.md](../product/01-overview.md) |
| `00-MASTER-PRODUCT-SPEC` | UPDATED | [docs/README.md](../README.md) + product set |
| `01-TRACK-4-REQUIREMENTS` | UPDATED | [product/03-challenge-requirements.md](../product/03-challenge-requirements.md) |
| `02-PRODUCT-SCOPE-AND-BOUNDARIES` | **KEPT** (still accurate) | [product/02-scope-and-boundaries.md](../product/02-scope-and-boundaries.md) |
| `03-SYSTEM-ARCHITECTURE` | UPDATED | [architecture/01](../architecture/01-system-architecture.md) |
| `04-BACKEND-ARCHITECTURE` | UPDATED | [architecture/02](../architecture/02-backend-architecture.md) |
| `05-RECONCILIATION-ENGINE-DESIGN` | UPDATED — defect marked RESOLVED, rules and lesson retained | [architecture/03](../architecture/03-reconciliation-engine.md) |
| `06-DATA-MODEL-AND-DATABASE` | **KEPT** (accurate) | [architecture/04](../architecture/04-data-model.md) |
| `07-CSV-AND-SYNTHETIC-DATA-SPEC` | UPDATED | [architecture/05](../architecture/05-ground-truth-evaluation.md) |
| `08-GROUND-TRUTH-AND-EVALUATION` | UPDATED — endpoint contract corrected | [architecture/05](../architecture/05-ground-truth-evaluation.md) |
| `09-AI-AGENT-AND-TOOLING` | **KEPT** (accurate and durable) | [architecture/06](../architecture/06-ai-architecture.md) |
| `10-AI-PROVIDER-RESILIENCE` | UPDATED — defects marked RESOLVED | [architecture/06](../architecture/06-ai-architecture.md) |
| `11-API-CONTRACT` | **REPLACED** — regenerated from source | [api/01-contract.md](../api/01-contract.md) |
| `12-ERROR-HANDLING-AND-PROBLEMDETAILS` | UPDATED — standardisation complete | [api/02-error-handling.md](../api/02-error-handling.md) |
| `13-AUTHENTICATION-AUTHORIZATION-SECURITY` | UPDATED — CORS resolved | [architecture/07](../architecture/07-auth-and-security.md) |
| `14-AUDITABILITY-AND-OBSERVABILITY` | KEPT + flagged — endpoint genuinely missing | [architecture/04](../architecture/04-data-model.md#audit-log) |
| `15-TESTING-AND-QUALITY-ENGINEERING` | UPDATED — counts corrected, required tests now exist | [quality/01-testing.md](../quality/01-testing.md) |
| `16-PERFORMANCE-AND-THROUGHPUT` | KEPT + flagged — requirement still unmet | [quality/03-performance.md](../quality/03-performance.md) |
| `17-FRONTEND-ANGULAR20-ARCHITECTURE` | **REPLACED** — stack changed | [frontend/01-architecture.md](../frontend/01-architecture.md) |
| `18-FRONTEND-UX-UI-DESIGN-SYSTEM` | **REPLACED** — dark→light inversion; **governing rule retained** | [design/01-design-system.md](../design/01-design-system.md) |
| `19-FRONTEND-ROUTES-AND-SCREENS` | **REPLACED** — route map changed materially | [frontend/03-routes-and-screens.md](../frontend/03-routes-and-screens.md) |
| `20-FRONTEND-API-INTEGRATION` | UPDATED | [frontend/04-api-integration.md](../frontend/04-api-integration.md) |
| `21-FEATURE-MATRIX-ADD-REMOVE` | UPDATED — most rows now done; fallback re-rated | [product/04-feature-matrix.md](../product/04-feature-matrix.md) |
| `22-BUILDATHON-DEMO-RUNBOOK` | UPDATED — real routes, video and what-broke added | [delivery/02-demo-runbook.md](../delivery/02-demo-runbook.md) |
| `23-JUDGE-EVALUATION-AND-WINNING-STRATEGY` | UPDATED | [delivery/03-judge-strategy.md](../delivery/03-judge-strategy.md) |
| `24-RISKS-AND-REJECTION-CRITERIA` | UPDATED — closed risks separated | [delivery/04-risks.md](../delivery/04-risks.md) |
| `25-IMPLEMENTATION-ROADMAP` | **REPLACED** | [delivery/01-roadmap.md](../delivery/01-roadmap.md) |
| `26-REPOSITORY-HYGIENE-AND-GITHUB` | UPDATED — mostly resolved | [setup/01-local-development.md](../setup/01-local-development.md#repository-hygiene) |
| `27-LOCAL-DEVELOPMENT-AND-SETUP` | UPDATED | [setup/01-local-development.md](../setup/01-local-development.md) |
| `adr/README` (ADR-001…006) | **KEPT + EXTENDED** — all six still hold; 007–010 added | [adr/README.md](../adr/README.md) |
| `architecture/*` (6 Mermaid views) | UPDATED — "not yet built" removed | [architecture/01](../architecture/01-system-architecture.md) |

---

## What the archive got right, and should be credited for

- **The `[CONFIRMED]` / `[NOT PROVEN]` / `[RECOMMENDED]` labelling discipline.** Adopted
  in the current set as `[CODE]` / `[ZIP]` / `[OFFICIAL WEB]` / `[RECOMMENDATION]`.
- **The honesty standard** — no fabricated metrics, no "production ready", no remembered
  accuracy percentages. Carried forward unchanged.
- **The orphan-record analysis.** It correctly identified a real BLOCKER, traced it to a
  single method, and predicted the fix would need no downstream changes. It was right.
- **The testing lesson** — unit-testing a decision function does not prove the
  orchestration layer can reach its inputs. Preserved permanently in
  [quality/01-testing.md](../quality/01-testing.md#the-lesson-worth-keeping), independent
  of the closed defect that prompted it.
- **ADR-001 – ADR-006.** All six still hold, unmodified.
- **The scope discipline** in doc 02 — kept nearly verbatim.
- **The governing UX rule** from doc 18 — *verified financial data must be visually
  stronger than AI commentary* — retained permanently even though its palette is superseded.

---

## Rules

1. **Never edit the archive.** It is a record of what was believed, not what is true.
2. **Never partially merge from it.** If content is worth keeping, it has already been
   carried into the current set with its status corrected.
3. **Never cite it as current.** `[ZIP]` tags in the current documentation mark
   historical origin, and any stale claim is explicitly marked stale or RESOLVED.
4. **If a conflict is found**, resolve by source hierarchy — see
   [docs/README.md](../README.md#source-hierarchy) — and record the resolution rather
   than silently merging.
