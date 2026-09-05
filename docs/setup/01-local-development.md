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

## Create the first user

**Required — a fresh database contains no accounts.** FinSight has no public
registration endpoint and no seeded credentials, so the first account must be created
out-of-band before anyone can sign in. Migrations create the `users` table; they do not
populate it.

```bash
cd backend/FinSight.Api
dotnet run -- create-user --email operator@example.com --role User
```

You are prompted for the password twice, with **no echo** — nothing is displayed as you
type. The password is deliberately **not** a command-line argument: arguments are visible
in shell history and to anything that can read the process list.

For non-interactive use (CI, scripted setup), supply it through an environment variable
instead:

```bash
# bash
FINSIGHT_PROVISION_PASSWORD='<your password>' \
  dotnet run -- create-user --email operator@example.com --role User
```

```powershell
# PowerShell
$env:FINSIGHT_PROVISION_PASSWORD = '<your password>'
dotnet run -- create-user --email operator@example.com --role User
```

| Detail | Value |
|---|---|
| Roles | `Admin` or `User` — **exact, case-sensitive** (`admin` is rejected) |
| Password minimum | 8 characters |
| Email | Normalized to lowercase, must contain `@` |
| Success output | `Created user '<email>' with role '<role>'.` — exit code `0` |
| Duplicate email | `Provisioning failed: A user with email '<email>' already exists.` — non-zero exit, **nothing written**. The account already exists; sign in with it or choose another address |
| Invalid role/email/password | Rejected before any database write, non-zero exit |

The command hashes through the same `IPasswordService` the login path verifies against, so
a provisioned account is guaranteed to be able to sign in. **Neither the password nor its
hash is ever printed or logged.**

Run it again with a different address to add more accounts.

> This command runs and exits without starting the web server. A plain `dotnet run`
> never provisions anything, and the running API never exposes provisioning.

**Never commit credentials.** Choose the password at setup time; no default,
example, or fixture password exists anywhere in this repository.

### Provisioning vs. signup

Both exist and serve different purposes.

| | `create-user` | `/signup` |
|---|---|---|
| Who runs it | An operator, offline | Anyone, in the browser |
| Roles | `Admin` **or** `User` | Always `User` — never Admin |
| Needs the API running | No | Yes |
| Purpose | First account on a fresh clone; any admin account | Normal self-service account creation |

**Admin accounts can only be created by `create-user`.** The signup request carries no
role field at all, so a public caller cannot ask for elevated privileges.

## Authentication flows

Once the API and frontend are running, the full lifecycle is available in the browser.

| Route | Purpose |
|---|---|
| `/signup` | Create a standard user account. Redirects to `/login` on success — signup issues no token, so sign-in stays the single path that creates a session |
| `/login` | Sign in. Links to both signup and password reset |
| `/forgot-password` | Request a reset link |
| `/reset-password?token=…` | Set a new password using the link |

**Password policy** — minimum 8 characters, enforced identically by signup, reset and
`create-user` (one shared `CredentialPolicy`).

### Account enumeration

`/forgot-password` returns the **same** response for every syntactically valid address:

```
If an account exists for that email, we sent password reset instructions.
```

This is deliberate. Reporting "no account exists" would let anyone test which addresses
are registered. Do not "improve" this message to be more specific.

### Reset tokens

- 256 bits from a cryptographic RNG — not a GUID, not a counter, not a JWT.
- Only a **SHA-256 hash** is stored; the raw token exists solely inside the emailed link.
- **Single-use** and **time-limited** (60 minutes by default).
- Requesting a new link invalidates any previous one; completing a reset burns all
  outstanding links for that account.
- The token never appears in an API response, a log, or the database in raw form.

Configurable under `Auth:PasswordReset`:

| Key | Default | Notes |
|---|---|---|
| `FrontendBaseUrl` | `http://localhost:4200` | Becomes the host of the reset link. **Must** be set to the real origin in a deployment |
| `Lifetime` | `01:00:00` | How long a link stays redeemable |

### Testing password reset locally

**This project has no email provider.** In `Development`, reset links are written to a
local file sink instead of being emailed:

```
backend/FinSight.Api/dev-password-resets/
```

Each request writes one `reset-<timestamp>.txt` containing the recipient, the expiry and
the reset URL. Open that URL to complete the reset.

The sink directory is git-ignored — those files contain **live reset credentials**. The
link is deliberately written to a file rather than the application log, because logs get
shipped and aggregated.

Outside `Development`, an unconfigured sender is registered instead: a reset attempt
fails loudly rather than silently pretending mail was sent. **Configure a real email
provider before deploying**, by replacing the `IPasswordResetEmailSender` registration in
`FinSight.Infrastructure/DependencyInjection.cs`.

### Known limitation — sessions after a password reset

FinSight uses stateless JWTs with no server-side revocation list. A token issued **before**
a password reset stays valid until it expires (60 minutes by default). Resetting a
password therefore prevents future sign-ins with the old password but does not
retroactively kill an already-issued session. This is stated plainly rather than
implied otherwise.

### Forgot-password rate limiting

`/forgot-password` is rate-limited, checked before any account lookup so the limiter
itself cannot become an enumeration signal:

| Key | Limit | Window |
|---|---|---|
| Normalized email | 5 requests | 15 minutes |
| Client IP | 20 requests | 15 minutes |

Exceeding either returns `429 Too Many Requests` with a generic message and a
`Retry-After` header. Configurable under `Auth:PasswordResetRateLimit`
(`MaxAttemptsPerEmail`, `EmailWindow`, `MaxAttemptsPerIp`, `IpWindow`).

**This is an in-process, single-instance limiter** — state lives in application memory,
not a shared/distributed store. It protects the one running API instance, which is
FinSight's actual current deployment shape; it would need a shared store (e.g. Redis) to
protect a multi-instance deployment, and makes no general DDoS-protection claim.

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

Without it, every DB-backed fixture is **skipped** at `OneTimeSetUp` with the reason
`FINSIGHT_TEST_CONNECTION is not configured` — a plain `dotnet test` therefore reports
**0 failed** with those tests listed as skipped, not red. Set the variable and they run
normally; a genuine failure in them is still reported as a failure. See
[quality/01-testing.md](../quality/01-testing.md#environment-requirement).

## Synthetic data

Two complementary generators exist and serve different purposes — both are correct, both
are needed.

### 1. Canonical CLI evaluator — `FinSight.DataGenerator`

```bash
cd backend/FinSight.DataGenerator
dotnet run                      # default output directory
dotnet run -- <output-dir>      # explicit output directory
```

Writes four files: `payments.csv`, `bank.csv`, `settlements.csv` and the matching
`ground-truth.csv`.  Fixed seed (`42026`), 100 records, includes the `ToleranceMatch`
scenario — the reference dataset for demos and regression testing.

### 2. In-app parametrised generator — Synthetic Data Lab

Available at `/data-generator` in the frontend (requires login) or via `POST /api/test-data/generate`.

Supports 10 modes, 3 corruption intensities, and 4 sizes (50/100/250/500).  Omit the seed
for a cryptographically-random one; supply the same seed to reproduce identical files.
Ground truth is derived from scenario labels — **never** from reconciliation output.

```bash
# Quick API-level test with curl:
curl -s -X POST http://localhost:5180/api/test-data/generate \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"size":100,"mode":8,"intensity":1}' | jq .metadata
```

Downloads expire 1 hour after generation.  Re-generate with the same seed to re-download.

### The fixture is deterministic

`[CODE]` `GeneratorConfiguration` fixes the RNG seed (`42026`) and the exact scenario
distribution, so **every machine that runs the generator produces the same 100-transaction
dataset**:

| Outcome | Count | Breakdown |
|---|---|---|
| Matched | 70 | 60 exact · 10 within tolerance |
| Mismatched | 10 | 8 amount · 2 date |
| Missing | 12 | 5 bank · 4 settlement · **3 payment** (the orphan case) |
| Duplicate | 6 | 3 payment · 2 bank · 1 settlement |
| Unresolved | 2 | reversed/fraud scenario |
| **Total** | **100** | expected match rate **70.00%** |

This is why the reconciliation output is reproducible rather than merely repeatable: the
expected match rate is a declared constant (`ExpectedMatchRate = 70.00m`), not something
read back from a run.

> `test-data/generated/` is **git-ignored on purpose.** The dataset is regenerated from the
> seed rather than committed — a committed batch is exactly the "cherry-picked" artefact
> the evaluation bar warns about. Reproducibility comes from the seed, not from the file.

> **RESOLVED** `[ZIP]` doc 27 records a hardcoded `E:\Razorpay\...` path that broke the
> generator on any other machine. `[CODE]` Fixed — CLI override with a relative fallback.

## Ground-truth verification

Two ways to run the same comparison — they share one implementation
(`GroundTruthComparer`), so they cannot drift.

### In the browser

Open a completed run and choose **Verify against ground truth**, or go straight to:

```
/runs/<runId>/verify
```

Upload the `ground-truth.csv` that was generated alongside the batch. The page posts the
parsed rows to `POST /api/reconciliation/runs/{runId}/ground-truth-verification` and
renders the backend's verdict: PASS or FAIL, an expected-vs-actual table for total units,
all five statuses and the match rate, and every failure string verbatim.

**The comparison is stateless.** Nothing is persisted — there is no verification id, no
stored timestamp and no history. The page says so rather than implying otherwise.

**The labels are operator-supplied.** They are generated independently of reconciliation
(by `FinSight.DataGenerator`, from the scenario plan, before any run exists), but the file
is still handed over by whoever is at the keyboard. The correct claim is *"verified
against the supplied ground-truth labels"* — never *"self-verified"*.

### Offline / CI

```bash
# after generating data and running a reconciliation:
FINSIGHT_RUN_ID=<run guid> dotnet run --project backend/FinSight.DataGenerator
```

Requires `FINSIGHT_VERIFIER_EMAIL` and `FINSIGHT_VERIFIER_PASSWORD` — the endpoints are
`[Authorize]`-protected and the comparator signs in through the real login endpoint. Sets a
non-zero exit code on failure.

## Run performance

`GET /api/reconciliation/runs/{runId}/summary` returns `durationMs` and
`recordsPerSecond`, computed server-side from the run's persisted `StartedAt`/`CompletedAt`.
The Run Workspace displays them under **Run performance**.

Both are **null** for a run that has not completed — no zero, no estimate. This is one
wall-clock measurement of one run on the machine that executed it: **not a benchmark**, and
no cold/warm comparison exists. See
[quality/03-performance.md](../quality/03-performance.md).

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
