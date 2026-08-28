# System Architecture

`[CODE]` Verified against current source. Clean Architecture, dependency direction
correct, **no rewrite required or recommended**.

---

## Project dependency graph

```
FinSight.Domain            (no project references)
   ▲
FinSight.Application       (→ Domain)
   ▲
FinSight.Infrastructure    (→ Application, Domain; owns EF Core, Npgsql, Gemini, OpenAI, JWT)
   ▲
FinSight.Api               (→ Application, Infrastructure)
   ▲
FinSight.Tests             (→ all)

FinSight.DataGenerator     (→ Domain, Application; standalone console)
```

### Dependency rules — enforced

- Domain must never reference Application / Infrastructure / Api. `[CODE]` currently true.
- Application must never reference Infrastructure, EF Core, Npgsql, or any AI SDK.
  `[CODE]` currently true.
- Controllers must not contain reconciliation or matching logic. `[CODE]` currently true.
- Infrastructure must never be referenced *by* Api's domain logic — only composed in DI.

---

## Container view

```mermaid
graph TD
    subgraph Client
        FE[Angular 20 SPA]
    end
    subgraph Backend
        API[FinSight.Api — controllers, auth, ProblemDetails]
        APP[FinSight.Application — use cases, AI tools, evaluation]
        INF[FinSight.Infrastructure — EF Core, orchestrator, AI providers]
        DOM[FinSight.Domain — entities, enums]
    end
    DB[(PostgreSQL)]
    GEM[Google Gemini]
    OAI[OpenAI]

    FE -->|REST/JSON + Bearer JWT| API
    API --> APP
    APP --> DOM
    INF --> APP
    INF --> DOM
    API --> INF
    INF --> DB
    INF --> GEM
    INF --> OAI
```

## System context

```mermaid
graph TD
    User[Finance operator] -->|HTTPS + JWT| WebApp[Angular 20 SPA]
    WebApp -->|REST/JSON| Api[FinSight.Api]
    Api --> DB[(PostgreSQL)]
    Api --> Gemini[Google Gemini API]
    Api --> OpenAI[OpenAI API]
    Generator[FinSight.DataGenerator] -->|writes CSVs + ground-truth.csv| FS[(Local filesystem)]
    Generator -->|verifies a run over HTTP| Api
```

---

## Trust boundary — the defining property

```mermaid
graph LR
    subgraph Deterministic["Deterministic source of truth"]
        Strategies[Matching strategies]
        Classifier[MatchClassifier]
        DBState[(Persisted reconciliation state)]
    end
    subgraph AILayer["AI layer — read-only, advisory"]
        Explain[AiExplanationService]
        Assistant[FinanceAssistantService]
        ToolsRO[4 read-only tools]
    end
    Deterministic -- "read-only access" --> ToolsRO
    ToolsRO --> Assistant
    ToolsRO --> Explain
    AILayer -. "NO write path" .-> Deterministic
```

`[CODE]` Verified by absence: neither `AiExplanationService` nor
`FinanceAssistantService` nor any of the four tools contains a write path into
`ReconciliationResult.Status`, `ReconciliationRun.MatchRate`, or any `Amount` field. The
only fields AI ever writes are the exception's own `AiExplanation`,
`AiSuggestedCategory`, and `AiExplanationGeneratedAt`.

**Frontend consequence:** this boundary must be *visible*, not merely true. See
[design/05-ai-ux.md](../design/05-ai-ux.md).

---

## Reconciliation flow

```mermaid
sequenceDiagram
    participant C as Client
    participant Ctrl as ReconciliationController
    participant Orc as ReconciliationOrchestrator
    participant S1 as Strategy 1 (exact)
    participant S2 as Strategy 2 (24h tolerance)
    participant MC as MatchClassifier
    participant DB as PostgreSQL

    C->>Ctrl: POST /api/reconciliation/runs {batchId}
    Ctrl->>Orc: ExecuteAsync(request)
    Orc->>DB: load Payment/Bank/Settlement by batch
    Note over Orc: references = union(payment, bank, settlement) keys
    loop each transaction reference
        Orc->>S1: Evaluate(evidence)
        Orc->>S2: Evaluate(evidence, exactEvidence)
        Orc->>MC: Classify(evidence, exact, tolerance)
        MC-->>Orc: ClassificationDecision
        Orc->>DB: persist NormalizedTransaction + Result (+ Exception)
    end
    Orc->>DB: persist run (status, matchRate, totalUnits)
    Ctrl-->>C: 201 Created
```

## AI request flow

```mermaid
sequenceDiagram
    participant C as Client
    participant Ctrl as FinanceAssistantController
    participant Svc as FinanceAssistantService
    participant Prov as IFinanceAssistantProvider
    participant Tools as 4 read-only tools

    C->>Ctrl: POST /api/finance-assistant/ask {runId, question}
    Ctrl->>Svc: AskAsync
    Svc->>Prov: call 1 — tool selection (Tools = 4 definitions)
    Prov-->>Svc: ToolCalls[]
    loop each tool call
        Svc->>Tools: ExecuteAsync (read-only, persisted data)
        Tools-->>Svc: ToolResult
    end
    Svc->>Prov: call 2 — synthesis (Tools = EMPTY)
    Prov-->>Svc: final answer
    Svc-->>Ctrl: {answer, toolsUsed[], traceId?}
```

Recursion is impossible by construction — the second call is built with an empty tool
array and throws if the model attempts a tool call anyway. See
[architecture/06-ai-architecture.md](06-ai-architecture.md).

---

## External integrations

| Integration | Package | Role |
|---|---|---|
| PostgreSQL | `Npgsql.EntityFrameworkCore.PostgreSQL` | System of record |
| Google Gemini | `Google.GenAI` | Primary AI provider |
| OpenAI | `OpenAI` | Fallback AI provider |

Reconciliation never calls an AI provider. Its throughput therefore **cannot** be
degraded by AI latency — a genuine architectural strength worth stating explicitly.
