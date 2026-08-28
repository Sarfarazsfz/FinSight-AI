# Ground Truth and Evaluation

FinSight's strongest differentiator. `[OFFICIAL WEB]` The official bar is *"measured
accuracy"* and *"one cherry-picked match proves nothing"* — this is the mechanism that
answers both.

---

## Why it is credible

Ground-truth labels are derived from the same `GeneratorPlan` that produced the source
CSVs, **before any reconciliation run exists**. They are never produced by running the
system under test and recording its output. See
[ADR-003](../adr/README.md#adr-003-ground-truth-is-independent-from-runtime-reconciliation-output).

An accuracy number computed from the system being evaluated proves nothing. A number
computed against labels that existed before the system ran is a genuine measurement.

## How it is generated

`GroundTruthGenerator` emits one labelled row per planned transaction. `CsvWriter.WriteAll`
writes `ground-truth.csv` alongside `payments.csv`, `bank.csv`, `settlements.csv`.

### `ground-truth.csv` columns `[CODE]`

| Column | Meaning |
|---|---|
| `transaction_reference` | Join key |
| `scenario_type` | Which `ReconciliationScenario` produced this row |
| `expected_status` | Expected `MatchStatus` |
| `expected_reason_code` | Expected `ReconciliationReasonCode` |
| `expected_exception_category` | Expected `ExceptionCategory` (blank for Matched) |
| `expected_payment_present` | bool |
| `expected_bank_present` | bool |
| `expected_settlement_present` | bool |
| `expected_amount_relationship` | e.g. `Exact` |
| `expected_date_relationship` | e.g. `Exact` |

## Scenario matrix `[CODE]`

| Scenario | Count | Expected status | Expected reason code |
|---|---|---|---|
| ExactMatch | 60 | Matched | `EXACT_MATCH` |
| ToleranceMatch | 10 | Matched | `TOLERANCE_MATCH` |
| AmountMismatch | 8 | Mismatched | `AMOUNT_MISMATCH` |
| DateMismatch | 5 | Mismatched | `DATE_OUT_OF_TOLERANCE` |
| MissingBank | 5 | Missing | `SOURCE_ABSENT_BANK` |
| MissingSettlement | 4 | Missing | `SOURCE_ABSENT_SETTLEMENT` |
| **MissingPayment** | **3** | Missing | `SOURCE_ABSENT_PAYMENT` |
| DuplicatePayment | 3 | Duplicate | `DUPLICATE_PAYMENT` |
| DuplicateBank | 2 | Duplicate | `DUPLICATE_BANK` |
| DuplicateSettlement | 1 | Duplicate | `DUPLICATE_SETTLEMENT` |
| UnresolvedReversedFraud | 2 | Unresolved | `UNRESOLVED` |

`[OFFICIAL WEB]` Comfortably above the 50-record floor. `[RECOMMENDATION]` Target ~100
units for the demo — a round number, meaningful, and quick enough to run live.

> **RESOLVED** `[ZIP]` docs 05/07 record `MissingPayment` as absent from both the
> orchestrator and the generator. `[CODE]` It now exists in both, plus an
> `edge-tests/missing-payment/` fixture.

---

## What the comparator checks `[CODE]`

`GroundTruthComparer.Compare` is genuinely comprehensive, not a stub:

- Reference-set validation **in both directions** — catches missing *and* extra references
- Per-reference status comparison
- Per-reference exception comparison
- Aggregate counts for all five `MatchStatus` values
- Match rate, computed independently on both sides, required to agree exactly
- Reason-code count comparison
- Exception-category count comparison
- Overall expected-vs-actual exception count

**Every check appends to a shared failure list rather than short-circuiting.** One pass
reports *all* discrepancies.

**Frontend consequence:** the FAIL state is as important as PASS and must render the
complete failure list, not the first item. See
[design/04-application-ux.md](../design/04-application-ux.md#independent-verification).

---

## HTTP contract — corrected

> ### Documented conflict — resolved
>
> `[ZIP]` doc 08 specifies a target endpoint:
> `GET /api/reconciliation/runs/{runId}/ground-truth-verification`
> returning `{ isSuccess, failureCount, failures, expectedMatchRate, actualMatchRate }`.
>
> `[CODE]` The **actual** implementation is:
> `POST /api/reconciliation/runs/{runId}/ground-truth-verification`
> with a `GroundTruthRow[]` request body, returning the full 17-field
> `GroundTruthComparisonResult`.
>
> **Resolution: `[CODE]` wins.** Building to the documented `GET` shape would fail. See
> [api/01-contract.md](../api/01-contract.md).

The `POST` design is also better: the caller supplies the ground truth, so verification
works against any dataset rather than only one the server happens to hold.

## Two ways to verify

1. **In-product** — `POST …/ground-truth-verification` from the UI.
2. **Offline** — `FinSight.DataGenerator` reads `FINSIGHT_RUN_ID`, calls the live API,
   compares, and sets a non-zero exit code on failure — genuinely CI-usable.

Both share one implementation (`FinSight.Application.Evaluation`), so they cannot drift.

---

## Honesty rules — non-negotiable

1. **Never hardcode or remember an accuracy percentage.** Read it from a live run.
2. **Never hide a failure.** A visible "3 of 100 discrepancies found, listed below" is
   more credible than silence — and is exactly what the official bar rewards.
3. **Never regenerate ground truth from reconciliation output.** That would reintroduce
   the self-grading problem this design exists to avoid.

## Maintenance discipline

`[ZIP]` ADR-003 consequence, still true: `GroundTruthGenerator` and `SourceRowGenerator`
both consume the same `GeneratorPlan`, but adding a scenario to one without the other
would silently break comparability. This is a discipline requirement, not a structural
guarantee — any new scenario must touch both.
