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
- AI explanation for an exception, with graceful degradation when no provider is reachable — every external provider call is bounded (30s), so an unresponsive provider degrades to the next configured one, or to a calm error, and never hangs the request indefinitely
- Finance Assistant scoped to a run, reporting which tools it used — subject to the same bounded provider timeout
- Ground-truth verification over HTTP, with a browser workflow at `/runs/:runId/verify` — stateless, and explicitly against operator-supplied labels
- Run performance — `durationMs` / `recordsPerSecond` computed from each run's persisted timestamps. One wall-clock measurement per run; not a benchmark
- Audit trail — write path (unchanged) plus a read-only, ownership-scoped viewer: `GET /api/reconciliation/runs/{runId}/audit` and a matching "Audit evidence" section on the Run Workspace, both reading the same existing `audit_logs` store. This is evidence about a run's execution (timing, throughput, which events fired), never a second source of financial truth — match status, match rate and exception counts remain whatever the reconciliation breakdown and Ground Truth Verification report
- Batch ownership — a batch belongs to the authenticated user who created it, and every reconciliation run, result, exception, and ground-truth verification scoped to that batch is only accessible to its owner. A request for another user's batch or run returns 404, identical to a genuinely-not-found one
- Forgot-password abuse protection — `POST /api/auth/forgot-password` is rate-limited (5 requests / 15 minutes per normalized email, 20 / 15 minutes per client IP; **429** with `Retry-After` beyond that). In-process only, not a distributed limiter; known and unknown addresses remain indistinguishable regardless of rate-limit state
- Frontend: design-token system, authentication, protected application shell, batch upload and history, run workspace with the five-way status breakdown, run performance, and audit evidence, ground-truth verification, results and evidence, exception queue and investigation, AI explanation, Finance Assistant

**Not yet surfaced in the UI** — ownership is enforced end-to-end but there is no visible indicator of it in the UI, and it is a single-owner boundary, not enterprise multi-tenancy — there is no organization/team/role-sharing model. Batches created before this boundary existed remain accessible only where their original `createdBy` label could be matched to a real registered account; unmatched legacy batches are inaccessible to everyone by design (safe default-deny, not a bug).

**Test coverage** — 352 backend tests, 400 frontend tests. 51 of the backend tests are database-backed integration tests requiring `FINSIGHT_TEST_CONNECTION`; without it they are **skipped**, so a plain `dotnet test` reports 301 passed / 0 failed / 51 skipped. Set the variable to run them. See [docs/setup](docs/setup/01-local-development.md#tests).

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

Apply migrations:

```bash
cd backend/FinSight.Api
dotnet ef database update --project ../FinSight.Infrastructure
```

Create the first user — a fresh database has no accounts. You are prompted for the
password with no echo; it is never passed as an argument:

```bash
dotnet run -- create-user --email operator@example.com --role Admin
```

Roles are `Admin` or `User` (exact, case-sensitive). Once the app is running, further
standard accounts can also be created through `/signup`; `create-user` remains the only
way to create an Admin. See
[docs/setup/01-local-development.md](docs/setup/01-local-development.md) for the
non-interactive form, the password-reset flow, and how to test reset links locally.

Run the API:

```bash
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

## Walking through the product

Generate a dataset (seed-fixed at 100 transactions, so every machine produces the same one — nothing is committed):

```bash
cd backend/FinSight.DataGenerator
dotnet run
```

Then, signed in at `http://localhost:4200`:

| # | Route | What to look at |
|---|---|---|
| 1 | `/batches/upload` | Upload the generated `payments.csv`, `bank.csv`, `settlements.csv` |
| 2 | `/batches` | Press **Run reconciliation** on the new batch |
| 3 | `/runs/:runId` | Match rate, the **five-count breakdown** (matched · mismatched · missing · duplicate · unresolved, summing to the total), and **Run performance** |
| 4 | `/runs/:runId/exceptions` → detail | The three source rows behind one classification, then request the AI explanation *below* that evidence |
| 5 | `/runs/:runId` — right-hand rail | The Finance Assistant, reporting which read-only tools it used. **Not a route** — it is a panel in the run workspace (a drawer below 1024px) |
| 6 | `/runs/:runId/verify` | Upload the generated `ground-truth.csv` → **PASS/FAIL**, expected-vs-actual, and every failure verbatim |
| 7 | `/runs/:runId/results` | Every reconciliation unit, server-paginated |

Expected for the seeded dataset: **70 matched · 10 mismatched · 12 missing · 6 duplicate · 2 unresolved**, match rate **70.00%**, and ground-truth verification **PASS**.

The full presenter script is in [docs/delivery/02-demo-runbook.md](docs/delivery/02-demo-runbook.md).

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

Accounts can be created two ways. `dotnet run -- create-user` provisions offline and is the only path that can create an **Admin**; `/signup` is normal self-service account creation and always produces a standard user — the request carries no role field, so a public caller cannot ask for elevated privileges. Both hash through the same password service the login path verifies against, and neither accepts, prints, or logs a password.

Password reset is single-use and time-limited, and only a hash of each reset token is stored. `/forgot-password` returns the same response whether or not an account exists, to avoid account enumeration. **No email provider is configured** — in Development reset links are written to a local git-ignored file sink; see [docs/setup](docs/setup/01-local-development.md#testing-password-reset-locally).

No secret, key, connection string, or default credential is committed to this repository.

## Deployment

The production architecture is:

```
Vercel (Angular SPA — static)
    ↓  direct HTTPS API calls (CORS allowed on Railway)
Railway (ASP.NET Core API — Docker)
    ↓  Npgsql + SSL
Supabase (PostgreSQL)
```

### Railway (backend)

| Setting | Value |
|---|---|
| Root directory | `backend` |
| Dockerfile | `backend/Dockerfile` |
| Exposed port | `8080` |
| Instances | **1** — required (rate limiter and synthetic-data session store are in-process singletons) |

All secrets are supplied as Railway environment variables — nothing is baked into the image.

**Required environment variables** (names only — set values in Railway dashboard):

```
ConnectionStrings__FinSightDb          # Supabase connection string
Jwt__Issuer
Jwt__Audience
Jwt__SecretKey
Jwt__ExpirationMinutes
Cors__AllowedOrigins__0                # Vercel deployment URL — set after Vercel deploy
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:8080
Auth__PasswordReset__FrontendBaseUrl   # Vercel deployment URL

# At least one AI provider key (all three are optional; missing key = provider skipped):
AI__Providers__Gemini__ApiKey
AI__Providers__Gemini__Enabled=true
AI__Providers__Gemini__Model           # default: gemini-2.5-flash
AI__Providers__Nvidia__ApiKey
AI__Providers__Nvidia__Enabled=true
AI__Providers__Nvidia__Model           # default: openai/gpt-oss-120b
AI__Providers__Nvidia__BaseUrl         # default: https://integrate.api.nvidia.com/v1

# Provider chain (comma-separated, or Railway array syntax __0 __1 ...):
AI__ExceptionExplanation__ProviderOrder=Gemini,NVIDIA
AI__FinanceAssistant__ProviderOrder=Gemini,NVIDIA
```

**Migrations** — not applied automatically. Run once after the Supabase database is created:

```bash
cd backend/FinSight.Api
dotnet ef database update --project ../FinSight.Infrastructure
```

The connection string must be available as `ConnectionStrings__FinSightDb` when this command runs.

**First user** — a fresh database has no accounts. Either register through `/signup` in the browser, or provision an Admin account:

```bash
# with ConnectionStrings__FinSightDb set in environment
dotnet run -- create-user --email admin@example.com --role Admin
```

**Password reset in production** — `forgot-password` returns 500 until a real email provider is wired up. The `UnconfiguredPasswordResetEmailSender` is registered for every non-Development environment by design; see `DependencyInjection.cs`. Login and signup work normally.

**Health check** — no dedicated HTTP health endpoint exists. Railway uses TCP/port verification.

### Vercel (frontend)

| Setting | Value |
|---|---|
| Framework | Other (static) |
| Root directory | `frontend` |
| Build command | `npm ci && npm run build` |
| Output directory | `dist/frontend/browser` |

`frontend/vercel.json` is committed in the repository and provides the SPA fallback rewrite. No `/api` proxy rewrite is needed — `environment.ts` points directly to the Railway origin, so all API calls are cross-origin requests from the browser to Railway, handled by CORS.

After Vercel deployment, update the Railway `Cors__AllowedOrigins__0` and `Auth__PasswordReset__FrontendBaseUrl` variables to the confirmed Vercel URL and redeploy Railway.

### Deployment order

1. Create Supabase project — collect connection string
2. Create Railway service — set environment variables (use placeholder for CORS / FrontendBaseUrl)
3. Run EF migrations against Supabase (`dotnet ef database update`)
4. Deploy Railway — verify API starts and responds
5. Create first user (signup or provisioning command)
6. Create Vercel project — `frontend/vercel.json` is committed; set Railway API URL in Railway `Cors__AllowedOrigins__0` after Vercel URL is known — deploy
7. Update Railway `Cors__AllowedOrigins__0` and `Auth__PasswordReset__FrontendBaseUrl` to confirmed Vercel URL
8. End-to-end verification: login → upload → reconcile → AI explain → ground-truth verify
