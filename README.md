# FinSight

**Financial Reconciliation Intelligence**

> Find what doesn't reconcile. Understand why. Prove the result.

FinSight reconciles three independent financial record sources — **Payment**, **Bank**, and **Settlement** — into a single measured outcome, queues everything it could not resolve for human review with the evidence attached, and lets the result be checked against an independently generated ground truth before anyone trusts it.

---

## The problem

Reconciling payment records against bank clearing and settlement records is still largely manual, spreadsheet-driven work. Discrepancies — amount differences, timing gaps, duplicates, missing counterpart records — are hard to find systematically and harder to explain. Teams close the books on judgement rather than proof.

The question FinSight exists to answer is a single one:

> **"Can I trust this reconciliation result?"**

## Core workflow

```
UPLOAD → VALIDATE → RECONCILE → UNDERSTAND
       → INVESTIGATE → AI EXPLAIN → INDEPENDENTLY VERIFY
```

| Stage | What happens |
|---|---|
| **Upload** | Three CSVs ingested against a labelled batch |
| **Validate** | Every row checked before reconciliation; failures returned per-row, per-field |
| **Reconcile** | Union of all three sources' references, two matching strategies, reason-coded classifier |
| **Understand** | Match rate plus a five-way status breakdown |
| **Investigate** | Exception queue with the full three-source evidence behind each decision |
| **AI explain** | Evidence-backed explanation, generated on request |
| **Independently verify** | Compare the run against an external ground truth |

## Deterministic truth, assistive AI

This is the architectural commitment the whole system is built around:

- **Match status, reason codes, match rate, and exception classification are computed entirely by deterministic C#.** No AI provider participates in that computation.
- **The AI layer is structurally read-only.** It reaches persisted data through exactly four read-only tools and has no write path into reconciliation state — verified by absence, not by convention.
- **The frontend never computes a financial value.** It renders what the API returns.
- **Ground truth is generated before any reconciliation run**, from the same plan that produced the source data — so comparing against it is a measurement, not self-grading.

AI explains and investigates results. It never decides them.

## Architecture

```
┌──────────────────────────────────────────────┐
│ Angular 20 SPA — Tailwind v4, signals, JWT   │
└──────────────────┬───────────────────────────┘
                   │ REST/JSON + Bearer token
┌──────────────────▼───────────────────────────┐
│ FinSight.Api            controllers, auth,   │
│                         ProblemDetails       │
├──────────────────────────────────────────────┤
│ FinSight.Application    use cases, AI tools, │
│                         ground-truth compare │
├──────────────────────────────────────────────┤
│ FinSight.Infrastructure EF Core, orchestrator│
│                         AI provider routing  │
├──────────────────────────────────────────────┤
│ FinSight.Domain         entities, enums      │
└───┬──────────────────┬───────────────────────┘
    │ PostgreSQL       │ Gemini → OpenAI fallback
```

Clean Architecture with a verified dependency direction — `Domain` has no project or package references at all, and no cycles exist anywhere in the graph.

## Technology

**Backend** — .NET 10 · ASP.NET Core · EF Core · PostgreSQL · JWT bearer auth · Google Gemini (primary) with OpenAI fallback

**Frontend** — Angular 20 · TypeScript (strict) · Tailwind CSS v4 with CSS custom properties · self-hosted Inter · signals · standalone components

## Current status

The backend is feature-complete for the reconciliation loop and frozen. The frontend is being built in small, independently verified phases.

**Implemented**

- Batch ingestion with structured, per-row/per-field validation errors
- Batch history and batch detail
- Reconciliation runs — union of all three sources, two matching strategies, 11 reason codes, 5 statuses
- Run summary, paginated results, three-source transaction evidence
- Exception listing and detail
- AI explanation for an exception, with graceful degradation when no provider is reachable
- Finance Assistant scoped to a run, reporting which tools it used
- Ground-truth verification over HTTP
- Audit trail (write path)
- Frontend: design-token system, authentication, protected application shell, batches entry

**Not yet built** — frontend screens for upload, reconciliation, results, exceptions, AI and verification; an audit-log read endpoint; throughput instrumentation.

**Test coverage** — 153 backend tests, 64 frontend tests.

No accuracy percentage is quoted here by design. Read it from a live run's ground-truth comparison.

## Repository structure

```
backend/      6 .NET projects (Domain, Application, Infrastructure, Api, Tests, DataGenerator)
frontend/     Angular 20 application
docs/         authoritative project documentation
edge-tests/   CSV fixtures, one directory per reconciliation scenario
test-data/    sample source CSVs
```

## Local setup

**Prerequisites** — .NET SDK 10 · PostgreSQL · Node.js and npm · optionally a Gemini and/or OpenAI API key (reconciliation itself never calls a provider and works without any key).

```bash
createdb finsight_ai
```

Configure development secrets (never committed):

```bash
cd backend/FinSight.Api
dotnet user-secrets set "ConnectionStrings:FinSightDb" "Host=localhost;Database=finsight_ai;Username=<you>;Password=<yours>"
dotnet user-secrets set "Jwt:Issuer" "FinSightAI"
dotnet user-secrets set "Jwt:Audience" "FinSightAI"
dotnet user-secrets set "Jwt:SecretKey" "<a long random string, 32+ characters>"
dotnet user-secrets set "Jwt:ExpirationMinutes" "60"
```

Apply migrations and run the API:

```bash
cd backend/FinSight.Api
dotnet ef database update --project ../FinSight.Infrastructure
dotnet run
```

The API listens on `http://localhost:5180`.

Run the frontend:

```bash
cd frontend
npm install
npm start
```

The application is served on `http://localhost:4200`, which the API's development CORS policy allows by default.

## Tests

```bash
# Backend
cd backend
dotnet test
```

Database-backed tests require `FINSIGHT_TEST_CONNECTION` pointing at a **dedicated** test database — the fixture wipes and re-migrates it.

```bash
export FINSIGHT_TEST_CONNECTION="Host=localhost;Database=finsight_test;Username=<you>;Password=<yours>"
```

```bash
# Frontend
cd frontend
npm test -- --watch=false --browsers=ChromeHeadless
```

## Synthetic data and verification

```bash
cd backend/FinSight.DataGenerator
dotnet run                    # writes payments/bank/settlements + ground-truth.csv
```

After ingesting a batch and running reconciliation, the same tool can verify that run against its ground truth, exiting non-zero on any mismatch:

```bash
FINSIGHT_RUN_ID=<run guid> dotnet run --project backend/FinSight.DataGenerator
```

## Demo path

Sign in → upload a freshly generated batch → validation passes → run reconciliation → read the match rate and status breakdown → open an exception and inspect the three source records behind it → request an AI explanation → verify the run against its independent ground truth.

## Documentation

Full documentation lives in [`docs/`](docs/README.md) — product scope, system and backend architecture, the reconciliation engine, the API contract, the AI trust boundary, the design system, testing strategy, and the implementation roadmap.

## Notes

There is no user registration endpoint; accounts are provisioned directly. No secret, key, or connection string is committed to this repository.
