# Backend Architecture

`[CODE]` All names verified by direct source inspection. **The backend is frozen** —
see [governance](../README.md#backend-freeze).

---

## FinSight.Domain

Pure business model, zero framework dependency.

- **Entities** — `Batch`, `PaymentRecord`, `BankRecord`, `SettlementRecord`,
  `NormalizedTransaction`, `ReconciliationRun`, `ReconciliationResult`,
  `ReconciliationException`, `AuditLog`, `User`
- **Enums** — `MatchStatus`, `ReconciliationReasonCode`, `ExceptionCategory`,
  `ReconciliationRunStatus`, `AuditEventType` (full values in
  [api/01-contract.md](../api/01-contract.md#enumerations))
- **Value objects** — `Money`, `DateRange`, `TransactionReference`
- **Forbidden** — any reference to Application/Infrastructure/Api, or any NuGet package
  beyond the BCL

## FinSight.Application

Use-case contracts, DTOs, AI tool abstractions, repository interfaces, and the shared
evaluation logic.

- `Abstractions/Persistence/*` — repository interfaces (`IBatchRepository`,
  `IPaymentRecordRepository`, `IBankRecordRepository`, `ISettlementRecordRepository`,
  `INormalizedTransactionRepository`, `IReconciliationRunRepository`,
  `IReconciliationResultRepository`, `IReconciliationExceptionRepository`,
  `IAuditLogWriter`, `IUnitOfWork`, `IUserRepository`)
- `Abstractions/Reconciliation/*` — strategy interfaces
- `Abstractions/Services/*` — `IReconciliationService`, `IAiProvider`,
  `IAiExplanationService`, `IAuthService`, `IJwtTokenService`, `IBatchIngestionService`,
  `IBatchIngestionValidator`, `ISourceCsvParser`
- `Abstractions/Evaluation/*` — `IGroundTruthComparisonService`
- **`Evaluation/*`** — `GroundTruthComparer`, `GroundTruthComparisonResult`,
  `GroundTruthRow`, `GroundTruthActualResult`, `GroundTruthActualException`,
  `GroundTruthComparisonService`
  > `[CODE]` **Undocumented in `[ZIP]`.** The comparison logic was relocated here from
  > `FinSight.DataGenerator` so that both the HTTP endpoint and the offline console
  > verifier share one implementation rather than duplicating it.
- `AI/*` — `FinanceAssistantService`, `FinanceToolRegistry`, `IFinanceTool`, the four
  tool implementations, `FinanceToolRequestMapper`, tool DTOs
- `DTOs/*` — Ai, Auth, Ingestion, Reconciliation
- `Exceptions/*` — `AiProviderUnavailableException`

**Forbidden** — Infrastructure, EF Core, any AI SDK, ASP.NET Core.

## FinSight.Infrastructure

Framework- and SDK-touching implementations.

- `Persistence/` — `AppDbContext`, `UnitOfWork`, 9 EF configurations, 4 migrations
  (`InitialCreate`, `AddBankAndSettlementStatus`, `AddReconciliationQueryIndexes`,
  `AddUsers`)
- `Repositories/` — 9 implementations
- `Reconciliation/` — `ReconciliationOrchestrator`, `MatchClassifier`,
  `Strategies/StrategyOneExactReferenceMatch`, `Strategies/StrategyTwoAmountDateToleranceMatch`
- `AI/` — `AiExplanationService`, `AiProviderOptions`, `AiProviderRouter`,
  `FinanceAssistantProviderRouter`, `Gemini/*`, `OpenAI/*`
- `Authentication/` — `AuthService`, `JwtTokenService`, `JwtOptions`, `PasswordService`
- `FileParsing/SourceCsvParser`
- `Ingestion/BatchIngestionService`, `BatchIngestionValidator`
- `DependencyInjection.cs` — composition

**Forbidden** — referencing Api.

## FinSight.Api

HTTP surface, auth wiring, composition root.

- `Controllers/` — `AuthController`, `BatchesController`, `ReconciliationController`,
  `FinanceAssistantController`
- `ErrorHandling/GlobalExceptionHandler.cs`
- `Program.cs` — CORS, ProblemDetails, JWT, OpenAPI, DI

`[CODE]` Controllers contain **no** reconciliation or matching logic — verified.

## FinSight.Tests

`[CODE]` **34 test files · 153 `[Test]` methods.**
> `[ZIP]` doc 15 states "~100 tests across 22 files" — **stale**.

Layers: Reconciliation (unit) · Api (`WebApplicationFactory`) · Authentication · AI ·
Evaluation · Integration (`PostgresIntegrationFixture`, requires
`FINSIGHT_TEST_CONNECTION`). See [quality/01-testing.md](../quality/01-testing.md).

## FinSight.DataGenerator

Standalone console tool: synthetic data + ground truth + offline verification.

- `Generation/` — `TransactionGenerator`, `SourceRowGenerator`, `GroundTruthGenerator`,
  `CsvWriter`
- `Models/` — `ReconciliationScenario`, `GeneratorConfiguration`, `GeneratorPlan`
- `Validation/GroundTruthComparator` — HTTP/auth/CSV transport shell; the comparison
  logic itself now lives in `FinSight.Application.Evaluation`

> **RESOLVED** `[ZIP]` doc 04 records a hardcoded developer-machine output path
> (`E:\Razorpay\...`). `[CODE]` The generator now accepts a CLI output-directory
> override and falls back to a path relative to the executing assembly.

---

## Folder structure — no reorganisation required

```
backend/
  FinSight.Domain/{Entities,Enums,ValueObjects}
  FinSight.Application/{Abstractions,AI,DTOs,Evaluation,Exceptions}
  FinSight.Infrastructure/{AI,Authentication,FileParsing,Ingestion,Persistence,Reconciliation,Repositories}
  FinSight.Api/{Controllers,ErrorHandling,Properties}
  FinSight.Tests/{AI,Api,Authentication,Evaluation,Ingestion,Integration,Reconciliation}
  FinSight.DataGenerator/{Generation,Models,Validation}
```

`[CODE]` The existing convention is already layer- and feature-appropriate. Restructuring
would create churn with no benefit.
