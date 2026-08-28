# AI Architecture and Trust Boundaries

`[CODE]` Verified by direct source inspection, including verification *by absence* for
the write-path claims.

---

## The governing principle

> **AI explains and investigates verified evidence. It never decides financial truth.**

See [ADR-001](../adr/README.md#adr-001-deterministic-reconciliation-is-the-financial-source-of-truth)
and [ADR-002](../adr/README.md#adr-002-ai-operates-only-through-read-only-investigation-tools).

## Verified by absence `[CODE]`

Neither `AiExplanationService`, nor `FinanceAssistantService`, nor any of the four tools
contains a write path into:

- `ReconciliationResult.Status`
- `ReconciliationResult.ReasonCode`
- `ReconciliationRun.MatchRate`
- any `Amount` field

The only fields any AI code path writes are the exception's own `AiExplanation`,
`AiSuggestedCategory`, and `AiExplanationGeneratedAt` — advisory columns, structurally
separate from `DiscrepancyDetail`.

### Explicit prohibitions — true today, must remain true

- AI writes reconciliation state — **does not happen**
- AI invents amounts — **cannot happen**; the model only ever sees already-persisted values
- AI determines match rate — **does not happen**; computed once, deterministically
- AI changes classification — **cannot happen**; no tool exposes a classification write

---

## Bounded two-call design

```
Call 1  (Tools = 4 definitions)   → tool selection only
        backend executes selected tools, read-only, against persisted repositories
Call 2  (Tools = EMPTY array)     → final synthesis, no further tool calls possible
```

`[CODE]` This is bounded **structurally**, not by convention: the second call is
constructed with an empty tool array, and the code throws if the model attempts a tool
call anyway. Recursive tool loops are impossible by construction.

See [ADR-004](../adr/README.md#adr-004-bounded-two-call-ai-interaction-instead-of-recursive-agent-loops)
for the tradeoff — genuinely multi-step investigative chains are not supported, and that
is a deliberate, documented limitation.

## The four tools — all read-only `[CODE]`

| Tool | Purpose | Inputs |
|---|---|---|
| `getReconciliationSummary` | Authoritative run summary | `runId` |
| `getUnmatchedRecords` | Every non-matched result for a run | `runId` |
| `getTransactionDetails` | Payment/Bank/Settlement rows for one result | `runId`, `resultId` |
| `getExceptionDetails` | Full exception evidence | `exceptionId` |

> **These are internal tools, not HTTP endpoints.** Do not build frontend services
> against them. The UI derives "unmatched" by filtering results on
> `Status ∈ {Missing, Unresolved}`.

**Security boundary** — `FinanceToolRegistry.TryGet` rejects any name outside this fixed
set. Malformed arguments are caught by `FinanceToolRequestMapper.TryMap` and converted
into a structured failed tool result fed back to the model, rather than an unhandled
exception.

**Transparency** — `FinanceAssistantResponse.ToolsUsed` returns exactly which tools ran.
This is real provenance, exposed to the caller, and the UI must surface it. See
[design/05-ai-ux.md](../design/05-ai-ux.md).

---

## Provider routing and fallback

`[CODE]` Gemini primary, OpenAI fallback, configured via `AiProviderOptions`
(`DefaultProvider`, `FallbackEnabled`, per-provider `ApiKey`), bound from User Secrets in
development.

Two routers exist by design, for the two distinct AI surfaces:

| Router | Used by | Fallback resolution |
|---|---|---|
| `AiProviderRouter` | `AiExplanationService` | By **instance identity** — always the genuinely different provider |
| `FinanceAssistantProviderRouter` | `FinanceAssistantService` | Same pattern |

> ### RESOLVED — same-provider retry defect
>
> `[ZIP]` doc 10 records a CONFIRMED DEFECT: `AiProviderRouter.GetFallbackProvider`
> re-derived the fallback from the *configured default string* rather than the
> *instance actually used*, so a Gemini-configured-but-unavailable setup could retry
> OpenAI after OpenAI had already failed.
>
> **Status: FIXED** `[CODE]` — fallback now resolves by reference identity, matching the
> assistant router. The cosmetic missing-`$` interpolation bug in the same class is also
> no longer present.

### Failure semantics `[CODE]`

| Scenario | Behaviour |
|---|---|
| Primary available | Used directly |
| Primary unavailable, fallback available | Falls back to the genuinely different provider |
| Primary transient failure mid-call | Falls back |
| **Both unavailable** | `AiProviderUnavailableException` → **HTTP 503 + ProblemDetails** |
| Cancellation | `OperationCanceledException` re-thrown, never swallowed |

> **RESOLVED** `[ZIP]` docs 10/11/12 record that both-providers-down surfaced as an
> undifferentiated 500. `[CODE]` `GlobalExceptionHandler` now maps
> `AiProviderUnavailableException` to **503**.

**No retry/backoff logic exists, deliberately.** `[RECOMMENDATION]` A single fast
failover to a genuinely different provider is the right resilience strategy at this
scale. Do not add exponential backoff or circuit breakers — that is overengineering for
a bounded, low-volume assistant.

**Not distinguished** `[CODE]`: quota failure vs invalid-key failure; no explicit
per-provider timeout configuration was found. Recorded as known limitations, not claimed
as features.

---

## The 503 state is a feature

`[OFFICIAL WEB]` Judging explicitly reviews *how failures were identified at runtime and
how graceful fallbacks were engineered.*

`[ZIP]` doc 21 rates this "Judge Value: Low (invisible if working)". **That assessment is
superseded.** The AI-unavailable state must be:

- deliberately designed (see [design/05-ai-ux.md](../design/05-ai-ux.md)),
- deliberately demonstrable, and
- rehearsed as part of the demo.

When AI is unavailable, the correct product message is:

> **"AI explanation unavailable. Reconciliation result is unaffected."**

with the verified evidence panel fully intact beside it. That single state expresses the
entire trust model better than any amount of prose.
