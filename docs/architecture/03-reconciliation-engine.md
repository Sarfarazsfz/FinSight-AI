# Reconciliation Engine

The most important document in this set. `[CODE]` verified against
`ReconciliationOrchestrator`, `MatchClassifier`, and both strategies.

---

## Stages

**LOAD** — CSV rows ingested via `BatchIngestionService` / `SourceCsvParser` into
`PaymentRecord` / `BankRecord` / `SettlementRecord` tied to a `Batch`. Validation runs
before anything is persisted; a failure aborts the whole batch and persists nothing.

**NORMALIZE** — Records grouped by `TransactionReference` into `paymentGroups`,
`bankGroups`, `settlementGroups` using `StringComparer.Ordinal`.

**RECONCILE** — For each reference, a `ReconciliationEvidence` is built from the three
groups, then evaluated by `StrategyOneExactReferenceMatch` and
`StrategyTwoAmountDateToleranceMatch`.

**CLASSIFY** — `MatchClassifier.Classify` applies a fixed precedence:
`Duplicate → Missing → Exact match → Tolerance match → Amount mismatch → Date mismatch →
Unresolved`.

**MEASURE** — `matchRate = round((matchedCount / totalUnits) * 100, 2)`, where
`totalUnits` is the number of normalized transactions produced.

**INVESTIGATE** — Paged results/exceptions plus a per-transaction detail endpoint expose
the full evidence behind any decision.

**EXPLAIN** — AI reads that evidence and produces language. It never alters it.

---

## Iteration source — the completeness guarantee

`[CODE]` The orchestrator iterates the **union of all three sources' reference sets**:

```
allReferences = paymentGroups.Keys
                  .Union(bankGroups.Keys)
                  .Union(settlementGroups.Keys)      // StringComparer.Ordinal
foreach (reference in allReferences.OrderBy(x => x)) { ... }
```

This is what makes the exception list **complete by construction** rather than by
curation: a Bank-only or Settlement-only reference is built into evidence, classified,
counted, and turned into an exception exactly like any other.

> ### RESOLVED — orphan-record defect
>
> `[ZIP]` docs 00/05/24 record a **BLOCKER**: the orchestrator iterated
> `paymentGroups.Keys` only, so Bank/Settlement records with no Payment counterpart were
> never classified, never counted, and never surfaced — making
> `SOURCE_ABSENT_PAYMENT` unreachable in practice.
>
> **Status: FIXED** `[CODE]`. The union-of-keys iteration above closes it. The fix
> required no change to `ReconciliationEvidence`, `MatchClassifier`, either strategy, or
> any entity — the downstream model already tolerated an absent payment.
>
> **Also fixed:** the matching gap in the synthetic-data generator. A `MissingPayment`
> scenario now exists (`MissingPaymentCount = 3`) along with an
> `edge-tests/missing-payment/` fixture.
> *Minor divergence: `[ZIP]` doc 07 suggested a count of 5; `[CODE]` uses 3. Harmless —
> the scenario exists and is exercised.*
>
> **Proven, not assumed:** `ReconciliationOrphanReferenceIntegrationTests` is an
> orchestrator-level integration test that asserts the orphan case end-to-end **and**
> the completeness invariant. See the lesson below.

---

## Deterministic rules `[CODE]`

- **Reference matching** — exact `Ordinal` string equality across all three sources.
- **Exact match** — all three present, references equal, amounts equal, dates equal, and
  bank status is not `REVERSED_FRAUD`.
- **Tolerance match** — amount tolerance is **0.00** (amounts must still be exactly
  equal); date tolerance is **24 hours**. In practice "tolerance match" only ever fires
  on date proximity. *This is a naming imprecision, not a logic defect — documented so
  nobody misreads the strategy name as amount tolerance.*
- **Duplicate** — any source holding more than one record for a reference within the
  batch. Highest precedence.
- **Missing** — any of the three sources absent for a reference.
- **Unresolved** — nothing above applies cleanly, including the deliberate
  `REVERSED_FRAUD` non-comparable business state.
- **Evidence** — `BuildExceptionDetail` serialises the raw Payment/Bank/Settlement rows
  plus both strategies' evidence into `ReconciliationException.DiscrepancyDetail` as JSON.
- **Exceptions** — exactly one per non-`Matched` result.
- **Match rate** — denominator is always `totalUnits`, never raw CSV row counts.

### Reason codes

`EXACT_MATCH` · `TOLERANCE_MATCH` · `AMOUNT_MISMATCH` · `DATE_OUT_OF_TOLERANCE` ·
`SOURCE_ABSENT_PAYMENT` · `SOURCE_ABSENT_BANK` · `SOURCE_ABSENT_SETTLEMENT` ·
`DUPLICATE_PAYMENT` · `DUPLICATE_BANK` · `DUPLICATE_SETTLEMENT` · `UNRESOLVED`

---

## Invariants

These hold for every run and are asserted by tests `[CODE]`:

1. `matched + mismatched + missing + duplicate + unresolved == totalUnits`
2. Exactly one `ReconciliationException` per non-`Matched` result
3. `matchRate == round(matched / totalUnits * 100, 2)`
4. The same batch reconciled twice produces identical status and reason-code assignments

**Frontend consequence:** the UI must never recompute any of these client-side. If a
displayed number disagrees with the API, the API is right. See
[ADR-007](../adr/README.md#adr-007-the-frontend-is-not-financial-truth-either).

---

## The testing lesson — preserve this even though the defect is closed

`[ZIP]` doc 15 makes a point worth keeping permanently, independent of the bug that
prompted it:

> Unit-testing a decision function does **not** prove the orchestration layer can ever
> construct the inputs that function was designed for.

The orphan defect shipped past roughly a hundred passing tests precisely because
`MatchClassifier` was unit-tested with a hand-built payment-absent evidence object,
while nothing tested whether the orchestrator could *produce* such an object. It could
not.

`[RECOMMENDATION]` Whenever a decision function gains a new branch, ask separately:
*what constructs the input that reaches this branch, and is that path tested?*
