# Challenge Requirements

`[OFFICIAL WEB]` Verified against the official Razorpay AI Buildathon page and current
public sources. This document records **requirements**, not strategy — for strategy see
[delivery/03-judge-strategy.md](../delivery/03-judge-strategy.md).

> **This is internal prioritisation context. None of this language belongs in the
> product UI.** See [product/01-overview.md](01-overview.md#brand-and-tone).

---

## Track: AI Finance Controller

**Challenge** `[OFFICIAL WEB]`
> "Run the books and the cash position. Build an agent that closes one finance-ops loop
> across a 50+ record batch of synthetic data, reporting its match rate and the
> exceptions it could not resolve."

**Why now** `[OFFICIAL WEB]`
> "Verification capacity, not generation speed, is the bottleneck." — "Reconciliation,
> settlement and forecasting are still done by hand."

**The bar** `[OFFICIAL WEB]`
> "Throughput plus measured accuracy plus an honest exception list. One cherry-picked
> match proves nothing."

**Example directions** `[OFFICIAL WEB]` Multi-source reconciliation · Settlement Q&A
agent · Forward cash forecaster · Tax-line matcher.
These are **alternatives, not a checklist.** FinSight commits fully to the first.

---

## Requirement → implementation mapping

| Requirement | Source | Status `[CODE]` | Where documented |
|---|---|---|---|
| Agent closes one finance-ops loop | `[OFFICIAL WEB]` | ✅ | [architecture/03](../architecture/03-reconciliation-engine.md) |
| 50+ record synthetic batch | `[OFFICIAL WEB]` | ✅ ~100 planned units | [architecture/05](../architecture/05-ground-truth-evaluation.md) |
| Reports **match rate** | `[OFFICIAL WEB]` | ✅ | [api/01](../api/01-contract.md) |
| Reports **exceptions it could not resolve** | `[OFFICIAL WEB]` | ✅ complete by construction | [architecture/03](../architecture/03-reconciliation-engine.md) |
| Measured accuracy, not claimed | `[OFFICIAL WEB]` | ✅ ground-truth verification | [architecture/05](../architecture/05-ground-truth-evaluation.md) |
| **Throughput** | `[OFFICIAL WEB]` | ❌ **not instrumented** | [quality/03](../quality/03-performance.md) |
| Public GitHub repository | `[OFFICIAL WEB]` | ⚠️ frontend uncommitted | [setup/01](../setup/01-local-development.md) |
| **5-minute pitch video** showing architecture | `[OFFICIAL WEB]` | ❌ not produced | [delivery/02](../delivery/02-demo-runbook.md) |
| **Explain what broke and how you recovered** | `[OFFICIAL WEB]` | ❌ not written up | [delivery/02](../delivery/02-demo-runbook.md) |

---

## Judging emphases `[OFFICIAL WEB]`

1. Whether AI tools / LLMs / agents were applied **appropriately**.
2. **How system failures were identified at runtime, and how graceful fallbacks were
   engineered.**

### Documented conflict — resolved, not merged

> `[ZIP]` doc 21 rates AI-provider fallback as *"Judge Value: Low (invisible if
> working)"*.
>
> `[OFFICIAL WEB]` states judging explicitly reviews how failures were identified at
> runtime and how graceful fallbacks were engineered.
>
> **Resolution: `[OFFICIAL WEB]` wins.** The AI-unavailable state is a **showcase
> surface**, not merely a defensive branch. It must be deliberately designed,
> deliberately demonstrable, and rehearsed. See
> [design/05-ai-ux.md](../design/05-ai-ux.md).

---

## Two requirements absent from the historical baseline

`[ZIP]` never mentions either of these. Both are real deliverables:

1. **A 5-minute pitch video showing architecture.**
2. **A written account of what broke during development and how it was recovered.**

`[RECOMMENDATION]` FinSight has unusually strong material for (2), because two genuine
defects were found by self-audit, fixed, and locked down with tests — see
[delivery/02-demo-runbook.md](../delivery/02-demo-runbook.md). This is a strength to
present deliberately, not a weakness to minimise.

---

## Open items

| Item | Note |
|---|---|
| Numeric scoring rubric | `[RECOMMENDATION]` None is published anywhere. All weighting in this documentation set is reasoned judgement, labelled as such — never present it as official. |
| Throughput measurement | Deferred backend work. Must become an explicit decision, never a fabricated number. |
