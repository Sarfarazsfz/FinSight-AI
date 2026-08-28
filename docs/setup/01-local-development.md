# Local Development and Repository

No real credentials appear in this document. Every value shown is a placeholder.

---

## Prerequisites

| Requirement | Notes |
|---|---|
| **.NET SDK 10** | `[CODE]` Every project targets `net10.0` |
| **PostgreSQL** | Current stable release |
| **Node.js + npm** | For the Angular frontend |
| **Gemini and/or OpenAI API key** | At least one is required for AI features. Reconciliation itself never calls a provider and works without any key |

## Database

```bash
createdb finsight_ai
```

## Secrets — development

`[CODE]` `FinSight.Api.csproj` declares a `UserSecretsId`. Configuration keys below are
read directly from source.

```bash
cd backend/FinSight.Api
dotnet user-secrets set "ConnectionStrings:FinSightDb" "Host=localhost;Database=finsight_ai;Username=<you>;Password=<yours>"
dotnet user-secrets set "Jwt:Issuer" "FinSightAI"
dotnet user-secrets set "Jwt:Audience" "FinSightAI"
dotnet user-secrets set "Jwt:SecretKey" "<a long random string, 32+ characters>"
dotnet user-secrets set "Jwt:ExpirationMinutes" "60"
dotnet user-secrets set "AI:Gemini:ApiKey" "<your Gemini key>"
dotnet user-secrets set "AI:OpenAI:ApiKey" "<your OpenAI key>"
```

> **Never print secret values back to a terminal**, including through a "redaction"
> filter — a pattern that silently fails to match leaks the real value. If a leak
> happens: stop immediately, say so plainly, and rotate the affected credentials.

For a hosted or demo deployment use environment variables instead:
`Jwt__SecretKey`, `AI__Gemini__ApiKey`, `AI__OpenAI__ApiKey`,
`ConnectionStrings__FinSightDb`.

## Migrations

```bash
cd backend/FinSight.Api
dotnet ef database update --project ../FinSight.Infrastructure
```

Install the tool first if needed: `dotnet tool install --global dotnet-ef`.

**No migrations may be created during frontend work.** The schema is frozen.

## Backend

```bash
cd backend/FinSight.Api
dotnet run
```

`[CODE]` Listens on `http://localhost:5180` (http profile) or `https://localhost:7148`
(https profile). CORS allows `http://localhost:4200` by default in Development.

## Frontend

```bash
cd frontend
npm install
npm start
```

Serves on `http://localhost:4200`, matching the backend's default CORS origin.

## Tests

```bash
cd backend
dotnet test
```

**Database-backed tests require `FINSIGHT_TEST_CONNECTION`** pointing at a **dedicated
test database** — the fixture wipes and re-migrates it. Never point it at a database
holding data you care about.

```bash
# bash
export FINSIGHT_TEST_CONNECTION="Host=localhost;Database=finsight_test;Username=<you>;Password=<yours>"
```

```powershell
# PowerShell
$env:FINSIGHT_TEST_CONNECTION = "Host=localhost;Database=finsight_test;Username=<you>;Password=<yours>"
```

Without it, every DB-backed fixture fails at `OneTimeSetUp` with the same clear
environment error — that is expected, not a logic failure. See
[quality/01-testing.md](../quality/01-testing.md#environment-requirement).

## Synthetic data

```bash
cd backend/FinSight.DataGenerator
dotnet run                      # default output directory
dotnet run -- <output-dir>      # explicit output directory
```

> **RESOLVED** `[ZIP]` doc 27 records a hardcoded `E:\Razorpay\...` path that broke the
> generator on any other machine. `[CODE]` Fixed — CLI override with a relative fallback.

## Offline ground-truth verification

```bash
# after generating data and running a reconciliation:
FINSIGHT_RUN_ID=<run guid> dotnet run --project backend/FinSight.DataGenerator
```

Compares the live API's output against `ground-truth.csv` and sets a non-zero exit code
on failure — usable in CI. Shares its comparison logic with the HTTP endpoint, so the two
cannot drift.

---

## Repository hygiene

### Structure

```
FinSight-AI/
  .gitignore  .gitattributes
  docs/                    ← this documentation set
  backend/                 6 projects
  frontend/                Angular 20
  edge-tests/              9 CSV scenario fixtures
  test-data/               source CSVs + generated/
```

### `.gitignore`

`[CODE]` Present and correct: `bin/`, `obj/`, `.vs/`, `frontend/node_modules/`,
`frontend/dist/`, `frontend/.angular/`, `test-data/generated/`, env and certificate
patterns.

> **RESOLVED** `[ZIP]` doc 26 records no `.gitignore` and ~96 MB of tracked build
> artifacts. Both are fixed.

**Do not modify `.gitignore` during frontend phases.**

### Outstanding

`[CODE]` `frontend/` is **untracked — 50 clean files, 0 in git**. This is
[risk #1](../delivery/04-risks.md) and is resolved by the F3 checkpoint commit.

`[CODE]` `backend/FinSight.Api/Program.cs` and
`backend/FinSight.Tests/AI/AiExplanationServiceTests.cs` show as modified, but
`git diff --ignore-cr-at-eol` is empty — **line-ending noise only**, no content change.

### Before making the repository public

1. Confirm no build artifacts are tracked.
2. Scan **history**, not just the working tree — untracking a file does not remove it
   from history.
3. Confirm no secrets in any commit.
4. Ensure a root `README.md` exists and is accurate.
5. Consider whether `test-data/generated/` should ship at all — a committed stale batch
   risks exactly the "cherry-picked" appearance the evaluation bar warns about. If
   included, label it a reference sample, not the demo batch.

### Commit discipline

One commit per completed phase, on `main`, with a descriptive message. Every phase is
independently revertable. Commit only after the phase's full acceptance model is
satisfied — see [delivery/01-roadmap.md](../delivery/01-roadmap.md#phase-acceptance-model).
