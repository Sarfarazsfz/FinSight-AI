# Demo Runbook

One deterministic end-to-end demonstration, using **only real backend capabilities**.

> **Status:** target. Requires P0–P13 complete. This is not a description of a demo that
> can run today.

---

## Pre-flight

| Step | Detail |
|---|---|
| 1 | PostgreSQL running; migrations applied |
| 2 | Backend running (`http://localhost:5180`) |
| 3 | At least one AI provider key configured — the demo works without one, but the AI beats degrade to the 503 state |
| 4 | Frontend running (`http://localhost:4200`) |
| 5 | A demo user exists — `[CODE]` there is **no registration endpoint**; provision directly |
| 6 | **Generate a fresh batch immediately before the demo** — never reuse a committed one |
| 7 | Corresponding `ground-truth.csv` on hand for the verification step |
| 8 | Rehearse **twice**; confirm consistent results |

**Anti-cherry-picking rule** `[OFFICIAL WEB]` *"One cherry-picked match proves nothing."*
Regenerate before the real run and be prepared to regenerate live if asked.

---

## Sequence — 5 minutes

| Time | Surface | Action | Visible result | Say | Do **not** say |
|---|---|---|---|---|---|
| 0:00–0:20 | `/` | Land | Hero + workflow | "FinSight reconciles payment, bank and settlement records into one measured result — and proves that result against an independent source." | Don't headline the AI |
| 0:20–0:40 | `/login` | Sign in | Workspace | — | — |
| 0:40–1:20 | `/batches/upload` | Upload the **fresh** batch | Three intake slots → validation passes → ready | "Freshly generated, labelled synthetic data with a known ground truth — nothing here is cherry-picked." | Don't say "random data" |
| 1:20–1:50 | `/batches/:id` | Run reconciliation | Run created | "All three sources reconciled in one pass." | — |
| 1:50–2:30 | `/runs/:id/overview` | Read the headline | Match rate + five counts + status bar | "Every unit is classified — matched, mismatched, missing, duplicate, unresolved. The counts sum to the total by construction, so the exception list is complete, not curated." | **Never quote a remembered percentage — read the screen** |
| 2:30–3:20 | `/runs/:id/exceptions` → detail | Open one exception | Evidence: Payment / Bank / Settlement, differing field marked | "Here is the actual evidence behind the classification — the three source rows, with the field they disagree on called out." | Don't skip to the AI |
| 3:20–3:50 | same | Request AI explanation | Note appears **below** the evidence | "The explanation is generated from that evidence. It's advisory — it never decided the classification, and it can't." | Don't imply AI computed anything |
| 3:50–4:20 | `/runs/:id/assistant` | Ask one question | Answer + tool-trail chips | "It investigated using our own read-only tools — you can see exactly which ones ran." | Don't call it a chatbot |
| 4:20–4:50 | `/runs/:id/verify` | Supply ground truth | **Independently Verified** (or the full failure list) | "This compares the run against labels generated before reconciliation ever ran — a measurement, not a self-report." | Don't claim a number the screen doesn't show |
| 4:50–5:00 | — | Close | — | "Find what doesn't reconcile. Understand why. Prove the result." | Don't end on a feature list |

---

## If AI is unavailable mid-demo

**This is a feature, not a failure.** `[CODE]` Both-providers-down returns a 503, and the
UI renders a designed state:

> "AI explanation unavailable. Reconciliation result is unaffected."

Presenter line:

> "This is exactly why reconciliation truth never depends on AI — the match rate, the
> exception list, and the evidence above are completely unaffected."

`[OFFICIAL WEB]` Judging explicitly reviews graceful runtime fallbacks. **Rehearse this
path deliberately.**

---

## If verification fails live

Show it. A visible "3 of 100 discrepancies found, listed below" is more credible than
silence and is exactly what the honesty standard asks for. Then explain what the
comparator checked. Do not retry until it passes.

---

## What broke and how we recovered

`[OFFICIAL WEB]` An explicit submission requirement. Two genuine, documented, test-backed
recoveries:

### 1. Incomplete reconciliation coverage (the significant one)

**Broke** The orchestrator iterated only Payment-keyed references. Any Bank or Settlement
record without a Payment counterpart was never classified, never counted, and never
appeared as an exception — so the exception list was silently incomplete and the
`SOURCE_ABSENT_PAYMENT` code was unreachable.

**How it was found** Self-audit, not a failing test. Roughly a hundred tests passed
because `MatchClassifier` was unit-tested with a hand-built payment-absent evidence
object — while nothing tested whether the orchestrator could *construct* one. It could
not.

**Recovered** Iterate the union of all three sources' key sets. No change was needed to
the evidence model, classifier, strategies, or entities — the downstream model already
tolerated an absent payment. Added a `MissingPayment` generator scenario, an edge
fixture, and — critically — an **orchestrator-level** integration test asserting the
completeness invariant.

**Lesson** Unit-testing a decision function does not prove the orchestration layer can
reach its inputs. See
[architecture/03](../architecture/03-reconciliation-engine.md#the-testing-lesson--preserve-this-even-though-the-defect-is-closed).

### 2. Sealed base type blocked the planned design (the instructive one)

**Broke** Structured validation errors were designed around a `BatchValidationException`
deriving from `InvalidDataException`, to preserve the existing service contract. It does
not compile — `System.IO.InvalidDataException` is **sealed** in .NET 10.

**How it was found** The compiler, immediately — an approved design meeting reality.

**Recovered** Kept the plain `InvalidDataException` and attached the structured payload
via `Exception.Data["Errors"]`. The service-layer contract was preserved **exactly**
(an existing integration test asserting `InvalidDataException` passes unchanged), while
the controller enriches the `ProblemDetails` response with the `errors[]` extension.

**Lesson** When a plan meets a language constraint, prefer the option that preserves the
existing contract over the one that requires changing every caller.

`[RECOMMENDATION]` Present both honestly. A team that finds, discloses, and locks down its
own correctness bug is more credible than one that reports no bugs.

---

## Video plan — 5 minutes `[OFFICIAL WEB]`

| Segment | Time | Content |
|---|---|---|
| Problem + thesis | 0:00–0:40 | Three-way reconciliation is manual and unprovable |
| Architecture | 0:40–1:40 | Clean Architecture; deterministic engine; AI trust boundary; ground truth generated independently |
| Live loop | 1:40–4:00 | Upload → validate → reconcile → investigate → explain → verify |
| What broke | 4:00–4:40 | Both recoveries above |
| Close | 4:40–5:00 | The promise |

**Must show architecture** — the trust boundary is the most distinctive thing to show.
**Must not** contain fabricated metrics, remembered percentages, or claims not visible on
screen.

---

## Throughput — decide before the demo

`[CODE]` Not instrumented. `[OFFICIAL WEB]` Named in the bar. Either measure it (a small
approved backend change) or state plainly that it was not measured. **Never estimate.**
See [quality/03-performance.md](../quality/03-performance.md).

---

## Honesty rules

1. Never quote an accuracy percentage from memory — read the screen.
2. Never claim "production ready", "enterprise grade", "real-time", "zero
   hallucinations", or "scales to millions".
3. Never hide a failure that occurs live.
4. Never present AI output as having decided anything.
5. Never demo a stale committed batch as if freshly generated.
