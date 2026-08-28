# Product Overview

**FinSight — Financial Reconciliation Intelligence**

> **Find what doesn't reconcile. Understand why. Prove the result.**

---

## What FinSight is

FinSight reconciles three independent financial record sources — **Payment**, **Bank**,
and **Settlement** — into a single measured outcome, queues everything it could not
resolve for human review with the evidence attached, and lets the result be checked
against an independently generated ground truth before anyone trusts it.

`[CODE]` Every number FinSight reports — match status, reason code, match rate,
exception classification — is computed by deterministic C#. No AI provider participates
in that computation, and no AI code path can write to it.

## The problem

`[ZIP]` `[RECOMMENDATION]` Reconciling payment records against bank clearing and
settlement records is still largely manual, spreadsheet-driven work. Discrepancies —
amount differences, timing gaps, duplicates, missing counterpart records — are hard to
find systematically and harder to explain to a non-technical stakeholder. Teams close
the books on judgement rather than proof.

## The central question

Everything in this product exists to answer one question:

> **"Can I trust this reconciliation result?"**

That question, not feature count, is the design north star. A screen earns its place by
making the answer more obvious.

## The workflow

```
UPLOAD → VALIDATE → RECONCILE → UNDERSTAND
       → INVESTIGATE → AI EXPLAIN → INDEPENDENTLY VERIFY
```

| Stage | What happens | Backend `[CODE]` |
|---|---|---|
| **Upload** | Three CSVs ingested against a labelled batch | `POST /api/batches` |
| **Validate** | Every row checked before reconciliation; failures returned per-row, per-field | `ProblemDetails.errors[]` |
| **Reconcile** | Union of all three sources' references, two matching strategies, reason-coded classifier | `POST /api/reconciliation/runs` |
| **Understand** | Match rate + five-way status breakdown | `GET …/summary` |
| **Investigate** | Exception queue with full three-source evidence | `GET …/exceptions`, `GET …/results/{id}` |
| **AI explain** | Evidence-backed explanation, generated on request | `POST …/ai-explanation` |
| **Independently verify** | Compare the run against an external ground truth | `POST …/ground-truth-verification` |

## Product principles

1. **Deterministic truth.** The reconciliation engine is the sole source of financial
   truth. See [ADR-001](../adr/README.md#adr-001-deterministic-reconciliation-is-the-financial-source-of-truth).
2. **AI assists, never decides.** AI explains verified evidence and answers questions
   about it. It has no write path into reconciliation state. See
   [ADR-002](../adr/README.md#adr-002-ai-operates-only-through-read-only-investigation-tools).
3. **Evidence before commentary.** In every UI surface, verified data outranks AI output
   visually and positionally. This is the product's single most important design rule.
4. **Provable, not claimed.** Ground truth is generated independently, before any run.
   See [ADR-003](../adr/README.md#adr-003-ground-truth-is-independent-from-runtime-reconciliation-output).
5. **Honest by construction.** The exception list is complete because the engine
   enumerates the union of all three sources — not because someone curated it.
6. **One loop, done deeply.** See [scope](02-scope-and-boundaries.md).

## Brand and tone

FinSight communicates **TRUST · PRECISION · CLARITY · CONTROL · PROOF**.

Design north star: **"LEDGER, NOT DASHBOARD."** The interface should feel like a
financial system of record — dense where it should be dense, quiet everywhere else — not
like an analytics template.

> **Buildathon context is internal prioritisation only.** "Track 04", "Buildathon",
> phase numbers, and internal roadmap language **must never appear in the product UI**.
> `[CODE]` The current frontend contains **zero** such leaks, and a unit test asserts
> that product copy never contains challenge-track or internal roadmap vocabulary.

---

## Personas

| # | Persona | Goal | Design consequence |
|---|---|---|---|
| **1** | **Finance operations analyst** (primary) | Run a batch, work the exception queue, gather evidence | Dense data, keyboard-fast, zero decoration, queue navigation |
| **2** | **Finance controller** (secondary) | Headline only: match rate, exception count, is it verified | Run Overview must answer in two seconds and be readable alone |
| **3** | **Evaluator / judge** (not a user) | Assess in 5–10 minutes, skeptically | Must reach "this is verifiable" without narration |

`[RECOMMENDATION]` Designing for (1) satisfies (3). Designing for (3) alone produces a
demo, not a product — and reads as one.

---

## User journeys

**Cold start** — landing → login → empty Batches → upload → validation passes → run →
overview.

**Daily operator** — login → Batches → open latest run → exceptions queue → work each
case (evidence → AI note → next) → verify.

**Validation failure** — upload → 400 with structured `errors[]` → errors grouped by
source, each showing row · field · message → fix CSV → re-upload.
`[CODE]` This is where structured validation errors become visible; the UI must never
parse the free-text `detail` string.

**Investigation** — exceptions → row → evidence drawer → Payment/Bank/Settlement
comparison with the differing field explicitly marked → "Explain" → AI note *below* the
evidence → next exception.

**Proof** — run overview → Verify → supply ground truth → **Independently Verified**, or
a complete deterministic failure list.

**Evaluation (5 minutes)** — see [delivery/02-demo-runbook.md](../delivery/02-demo-runbook.md).
