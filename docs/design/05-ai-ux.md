# AI UX

Two separate AI experiences, deliberately distinct. Both are governed by one rule.

---

## The governing rule

> **Verified financial data must be visually stronger than AI commentary — always.**

Retained verbatim from `[ZIP]` doc 18, which is superseded on palette but **permanently
authoritative on this point**. It is a product principle, not a style preference.

Concretely, everywhere AI output appears:

| Verified evidence | AI output |
|---|---|
| Full-strength text colour | Muted text colour |
| Positioned **above** | Positioned **below**, always |
| Its own bordered region, labelled as verified | A visually distinct, subordinate region labelled as AI |
| Loads fast, deterministic | Loads slowly, network-dependent — **never blocks the evidence** |
| Never absent | Frequently absent — and its absence changes nothing |

**Never merge the two into one paragraph, one card, or one visual weight.** The structural
separation exists in the database (`discrepancyDetail` vs the three `ai*` columns, written
at different times by different code paths) — the UI must express it, not flatten it.

---

## A. Exception AI Explanation

**Where** inline contextual panel on the exception detail screen, **below** the evidence.
Never a modal, never a drawer, never above.

**API** `POST /api/reconciliation/exceptions/{id}/ai-explanation` → `AiExplanationResponse`.

### Design

An **analyst's evidence-backed note**, not a chat message.

| Element | Treatment |
|---|---|
| Panel label | Explicitly identifies this as AI-generated |
| `explanation` | Body text, muted relative to evidence. Rendered as **text, never HTML** |
| `suggestedCategory` (nullable) | Clearly marked *suggested* — never styled like the verified `category` |
| `provider` | Small metadata — visible provenance |
| `generatedAtUtc` | Small metadata — an explanation has an age |

### States

| State | Treatment |
|---|---|
| Not requested | A quiet **"Explain this exception"** action. Evidence stands alone and is complete without it |
| Loading | Inline loading **within the panel only**. Evidence remains fully interactive |
| Loaded | Explanation + suggested category + provider + timestamp |
| **503 — both providers down** | See below |
| 404 / 400 | Inline message; evidence untouched |

### The 503 state — a showcase surface

`[OFFICIAL WEB]` Judging explicitly reviews *how failures were identified at runtime and
how graceful fallbacks were engineered.* `[ZIP]` doc 21 rated this "Judge Value: Low" —
**superseded**.

> **AI explanation unavailable. Reconciliation result is unaffected.**

Requirements: calm, not alarming — this is a degraded optional feature, not a system
failure · the evidence panel stays **fully intact and interactive** · a retry action ·
**never an error page, never a toast that implies the run is compromised**.

This single state expresses the entire trust model better than any amount of copy.
Design it deliberately and rehearse it — see
[delivery/02-demo-runbook.md](../delivery/02-demo-runbook.md).

---

## B. Finance Assistant

**Where** a tab within the run workspace, scoped to the current run.
**API** `POST /api/finance-assistant/ask` → `{ answer, toolsUsed[], traceId? }`.

### Framing

> **"Ask about this reconciliation."**

Not a chatbot. Not a general assistant. Scoped, bounded, and honest about its bounds.

### Design — explicitly not a chat UI

| Do | Do not |
|---|---|
| Structured answer panel | Rounded speech bubbles |
| Question shown as a quiet heading above its answer | Alternating left/right alignment |
| Provenance chips beneath every answer | Avatars, typing indicators, "…" animations |
| Plain, professional typography | Emoji, personality, conversational filler |

`[ZIP]` doc 18 is explicit and correct: consumer chat styling undermines the credibility
of an evidence tool.

### Provenance — the trust mechanism

`toolsUsed[]` must be rendered beneath every answer as compact chips:

```
Tools used:  getReconciliationSummary   getUnmatchedRecords
```

`traceId`, when present, appears as small metadata.

`[RECOMMENDATION]` This is the single most valuable element on the screen. Trust comes
from **visible receipts**, not from confident prose. `[OFFICIAL WEB]` judging assesses
whether agents were applied appropriately — the tool trail is the evidence.

### States

| State | Treatment |
|---|---|
| Empty | Framing sentence + two or three example questions grounded in this run |
| Pending | Input **disabled** — consistent with the bounded two-call design; no queued follow-ups |
| Answered | Answer + tool chips + trace metadata |
| **503** | "Finance Assistant temporarily unavailable. Reconciliation results are unaffected." |
| 400 | Inline validation (question required) |

### Honesty about limits

The assistant answers from four read-only tools scoped to **one run**. It cannot do
cross-batch analytics or multi-step investigative chains — see
[ADR-004](../adr/README.md#adr-004-bounded-two-call-ai-interaction-instead-of-recursive-agent-loops).
The empty state should suggest questions it can actually answer rather than inviting
open-ended ones it cannot.

---

## Prohibitions — both surfaces

| Never | Why |
|---|---|
| Present AI output as authoritative financial truth | It is advisory; the engine decides |
| Style `aiSuggestedCategory` like the verified `category` | Conflates a suggestion with a classification |
| Place AI output above or beside evidence at equal weight | Inverts the trust hierarchy |
| Block evidence rendering on an AI call | Evidence is deterministic and instant |
| Render AI text as HTML | Server-supplied text — always render as text |
| Imply AI computed the match rate, statuses, or amounts | Provably false and the most damaging possible claim |
| Use "AI sparkle" decoration, emoji, or marketing language | Undermines the restraint that makes it credible |
| Retry automatically on 503 | `[CODE]` No retry layer exists; a silent retry hides the failure the design is meant to surface |
