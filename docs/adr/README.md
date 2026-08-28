# Architecture Decision Records

Only load-bearing decisions are recorded. No ADR exists to increase document count.

ADR-001 – ADR-006 carry forward from `[ZIP]`, re-verified against current source.
ADR-007 – ADR-010 are new, covering frontend decisions the baseline never addressed.

| # | Decision | Status |
|---|---|---|
| [001](#adr-001-deterministic-reconciliation-is-the-financial-source-of-truth) | Deterministic reconciliation is the financial source of truth | Accepted |
| [002](#adr-002-ai-operates-only-through-read-only-investigation-tools) | AI operates only through read-only investigation tools | Accepted |
| [003](#adr-003-ground-truth-is-independent-from-runtime-reconciliation-output) | Ground truth is independent from runtime reconciliation output | Accepted |
| [004](#adr-004-bounded-two-call-ai-interaction-instead-of-recursive-agent-loops) | Bounded two-call AI interaction instead of recursive agent loops | Accepted |
| [005](#adr-005-provider-fallback-is-isolated-behind-an-abstraction) | Provider fallback is isolated behind an abstraction | Accepted |
| [006](#adr-006-one-finance-ops-loop-instead-of-multiple-shallow-features) | One finance-ops loop instead of multiple shallow features | Accepted |
| [007](#adr-007-the-frontend-is-not-financial-truth-either) | The frontend is not financial truth either | Accepted |
| [008](#adr-008-tailwind-css-v4-with-css-custom-properties) | Tailwind CSS v4 with CSS custom properties | Accepted |
| [009](#adr-009-signals-and-services-instead-of-a-state-management-library) | Signals and services instead of a state-management library | Accepted |
| [010](#adr-010-runshell-as-shared-run-context) | RunShell as shared run context | Accepted |

---

## ADR-001: Deterministic reconciliation is the financial source of truth

**Context** `[OFFICIAL WEB]` The track warns against AI deciding financial truth. A tool
that let an LLM determine match status or amounts would be unauditable.

**Decision** All amounts, match status, match rate, and exception classification are
computed exclusively by deterministic C# — `ReconciliationOrchestrator`, the two matching
strategies, and `MatchClassifier` — with zero AI involvement in the computation path.
`[CODE]` Already true; not a proposed change.

**Alternatives** An AI-assisted matching layer that could override or supplement
deterministic decisions in ambiguous cases. **Rejected** — reintroduces exactly the risk
the brief warns against, for a case the classifier already handles honestly via
`Unresolved`.

**Consequences** Genuinely ambiguous cases remain `Unresolved` rather than auto-resolved.
That is the correct outcome: an operator or the AI layer may add colour, but neither
silently resolves the record.

---

## ADR-002: AI operates only through read-only investigation tools

**Context** An AI assistant with write access to reconciliation state, or with
unrestricted query ability, is a far larger correctness and security surface.

**Decision** Exactly four read-only tools registered in `FinanceToolRegistry`;
unknown tool names are explicitly rejected; malformed arguments become structured failed
results rather than exceptions. `[CODE]` Existing.

**Alternatives** A general "run arbitrary read query" tool. **Rejected** — much larger
data-exposure surface for no demonstrated benefit at this scale.

**Consequences** The assistant cannot answer questions outside what those four tools
expose — no cross-batch analytics. A deliberate limitation, stated in the UI's empty
state rather than hidden.

---

## ADR-003: Ground truth is independent from runtime reconciliation output

**Context** `[OFFICIAL WEB]` *"One cherry-picked match proves nothing."* An accuracy
number computed from the system under test proves nothing.

**Decision** `GroundTruthGenerator` derives expected labels from the same `GeneratorPlan`
that produces the source CSVs, **before any reconciliation run** — never by recording the
engine's output as truth. `[CODE]` Existing.

**Alternatives** Generating ground truth by spot-checking reconciliation output.
**Rejected** — reintroduces self-grading.

**Consequences** Any new generator scenario must update both `SourceRowGenerator` and
`GroundTruthGenerator`. They share a plan but not a guarantee — this is a discipline
requirement.

---

## ADR-004: Bounded two-call AI interaction instead of recursive agent loops

**Context** Recursive tool-calling agents are hard to bound for cost, latency, and
auditability.

**Decision** Exactly two model calls per question: tool selection (four tool definitions),
then synthesis with an **empty** tool array and an explicit throw if a tool call is
attempted. `[CODE]` Existing; bounded structurally, not by convention.

**Alternatives** An open-ended ReAct-style loop. **Rejected** for this scope — recursion
risk, unbounded cost, harder to audit, and each of the four tools already returns
complete, self-contained evidence.

**Consequences** No multi-step investigative chains within one question. A documented,
deliberate limitation — and the reason the assistant's empty state suggests questions it
can actually answer.

---

## ADR-005: Provider fallback is isolated behind an abstraction

**Context** A single AI provider is a single point of failure for explanation and
assistant features — though never for reconciliation itself, per ADR-001.

**Decision** `IAiProvider` / `IFinanceAssistantProvider` abstractions with Gemini-primary,
OpenAI-fallback routing configured via `AiProviderOptions`. Fallback resolves by
**instance identity**, guaranteeing the genuinely different provider. `[CODE]` Existing,
and the historical same-provider-retry defect is fixed.

**Alternatives** A single hardcoded provider. **Rejected** — one outage would disable all
AI features with no recourse.

**Consequences** Two integrations to maintain in behavioural parity. No retry or backoff
exists, deliberately: a single fast failover is the right strategy at this scale, and
adding circuit breakers would be overengineering.

---

## ADR-006: One finance-ops loop instead of multiple shallow features

**Context** `[OFFICIAL WEB]` The example directions are alternatives; the brief asks for
**one** coherent loop.

**Decision** Commit fully to multi-source Payment/Bank/Settlement reconciliation.
Explicitly exclude forecasting, tax-line matching, and any unscoped chatbot.

**Alternatives** Adding a lightweight forecasting or Q&A feature "for differentiation".
**Rejected** — see [product/02](../product/02-scope-and-boundaries.md).

**Consequences** Differentiation must come from **depth** — ground-truth verification,
reason-coded exceptions, evidence-first investigation, tool-trail transparency — not
from feature breadth.

---

## ADR-007: The frontend is not financial truth either

**Context** ADR-001 keeps AI out of the truth path. The same reasoning applies one layer
outward: a frontend that recomputes a financial value creates a second source of truth
that can silently disagree with the first.

**Decision** No Angular service or component may compute or recompute match rate, status,
reason code, classification, or any aggregate. Every such value is rendered exactly as the
API returned it.

**Alternatives** Client-side aggregation for responsiveness. **Rejected** — the values are
cheap to fetch and expensive to get wrong, and a discrepancy between UI and API in a
financial tool destroys trust instantly.

**Consequences** Slightly more fetching. Presentation-only derivation remains permitted:
filtering a fetched page, sorting a fetched page, grouping `errors[]`, formatting for
display. If a displayed number disagrees with the API, the API is right.

---

## ADR-008: Tailwind CSS v4 with CSS custom properties

**Context** `[CODE]` The current frontend carries 1,583 lines of SCSS across 17 files —
a bespoke design system that would compete with any utility framework, and which produced
a result judged too generic.

**Decision** Tailwind CSS v4 is the styling engine; CSS custom properties (via `@theme`)
are the token layer. **SCSS is removed entirely.** Angular CDK is used only for
focus-trapped overlays and accessibility primitives. Lucide is the only icon library.
No Bootstrap, no Material as a visual system, no PrimeNG, no second UI kit, no second
icon set, no animation library, no chart library.

**Alternatives considered**
- *Keep SCSS, redesign within it* — rejected: the token layer is sound but the
  hand-rolled component styles are the cost, and they would be rewritten anyway.
- *Adopt a component library (Material/PrimeNG)* — rejected: would visually contradict
  the bespoke fintech direction and impose an opinionated look that is the opposite of
  the goal.
- *Tailwind alongside SCSS* — rejected: two competing systems is the worst outcome.

**Consequences** A large one-time migration (P2), the highest-risk phase in the roadmap.
In exchange: one styling model, tokens enforced by construction, less bespoke CSS to
maintain, and a smaller component-style footprint.

**Related** Claude Design may be used for visual exploration only. Any React artifact it
produces is a sketch. **The production frontend is Angular 20 and never migrates to React.**

---

## ADR-009: Signals and services instead of a state-management library

**Context** The product is a linear read/fetch pipeline over server-owned data, with
exactly one piece of genuinely global state (the session).

**Decision** Angular signals plus injectable services. `AuthStore` for the session,
`RunContextStore` scoped to the run shell, component signals for page state with an
explicit `'loading' | 'loaded' | 'empty' | 'error'` union. **No NgRx or equivalent.**

**Alternatives** NgRx. **Rejected** — actions, reducers, effects, and selectors for data
the server already owns adds ceremony and a second source of truth, contradicting ADR-007.

**Consequences** No time-travel debugging or centralised store inspection. Acceptable at
this screen count. Revisit only if genuine cross-feature shared mutable state appears —
it has not.

---

## ADR-010: RunShell as shared run context

**Context** Five surfaces — overview, results, exceptions, assistant, verification — all
operate on the same reconciliation run and all need the same summary context.

**Decision** A `RunShell` layout component at `/runs/:runId` owning a sticky context
header and a tab bar, providing `RunContextStore` so tabs share one summary fetch. Each
tab is a lazy child route and independently deep-linkable.

**Alternatives**
- *Five independent top-level routes* (as the `[ZIP]` baseline proposed) — rejected: each
  would re-fetch the summary, and the operator loses orientation moving between them.
- *A single page with client-side sections* — rejected: breaks deep-linking and produces
  one oversized component.

**Consequences** Slightly more routing structure. In exchange: one fetch, persistent
orientation while investigating, coherent tab navigation, and shareable URLs. Evidence is
a **drawer** over results rather than a route, because it is consulted while scanning;
exception detail is a **route** because it is worked one at a time with queue navigation.
