# Application UX

The authenticated workspace. Same visual family as the public surface, **denser**,
operational, evidence-first, ledger-like.

---

## Workspace shell

Compact left rail (five items — see
[frontend/02-information-architecture.md](../frontend/02-information-architecture.md#navigation--intentionally-small)),
main content area, and a contextual right region used only by the evidence drawer.

Below the rail breakpoint the rail becomes a slim top bar. The workspace is **wider and
left-aligned** than the public surface — an operator screen, not a centred marketing
column.

Minimal card dependence: data sits on the page separated by hairline rules and spacing.
A card is justified only when it groups genuinely distinct content.

---

## Batch history — the launcher

`/batches` is the authenticated home. **It is not a KPI dashboard.** An operator arrives
to start work or to reopen recent work.

**Above the fold** concise heading · one-line explanation · **Upload batch** primary CTA ·
recent batches table.

**Table** batch label · validation status badge · payments · bank · settlements · total ·
created at · action. Counts right-aligned, tabular numerals. Newest first.

All four states designed: skeleton rows matching the final layout · empty ("No batches
yet — upload your first record set" + CTA) · error with retry · loaded with pagination.

---

## Batch upload

Three visually distinct intake slots — **Payments · Bank · Settlements** — each
supporting drag-and-drop, browse, filename, size, ready state, and remove/replace.
Professional data intake, not giant decorated dashed rectangles.

**Stages, visible:** `PROCESSING → VALIDATION → READY FOR RECONCILIATION`.

### Validation errors — a designed moment

`[CODE]` On a 400 with `errors[]`, render the structured array, **grouped by source**:

```
Payments  (2 errors)
  Row 2  ·  payment_record_id  ·  Required value is missing.
  Row 7  ·  amount             ·  Amount must be greater than zero.

Bank      (1 error)
  Row 3  ·  bank_record_id     ·  Must match BANK-000001 style.
```

Row number and field in tabular numerals / monospace-adjacent treatment; message in body
text. Group headers carry counts.

**Never parse the free-text `detail` string.** See
[api/02-error-handling.md](../api/02-error-handling.md#structured-batch-validation-errors).

This is the most visible payoff of the backend's structured-error work and should be
treated as a designed screen state, not an afterthought.

---

## Run overview

**The most important authenticated screen.** The first two seconds must answer: *is this
reconciliation healthy?*

### Above the fold

| Element | Treatment |
|---|---|
| **Match rate** | `numeric-hero`, tabular numerals, the largest element on the page. Handle **nullable** — a run that has not completed shows a Pending state, never `0%` |
| **Total units** | Prominent, beside the hero |
| **Five status counts** | Matched · Mismatched · Missing · Duplicate · Unresolved — each with its status token **and text label** |
| **One status bar** | A single horizontal proportional bar segmented by the five statuses, with an accessible text summary |
| **Three actions** | **View exceptions** · **Ask assistant** · **Verify independently** |

**One visualisation, not a chart wall.** No pie charts, no time series, no chart library.
A horizontal proportional bar plus honest numbers communicates more than a dashboard grid
and cannot mislead.

The "Independently Verified" indicator, once a verification has run, is distinctive but
**restrained** — it is the single strongest visual moment in the product and loses that
power if it competes with decoration.

---

## Exception investigation — the hero experience

This receives the most interaction-design attention in the product.

### Flow

```
Queue → select → EVIDENCE FIRST → AI explanation second → next exception
```

### Queue

Dense table: transaction reference · category badge · involved sources · AI-explained
indicator · created at. Filterable by category. Server-side pagination.

**Empty state:** "No exceptions — every unit reconciled." Shown **only** when genuinely
true. Never assert completeness speculatively.

### Evidence — the core

Payment · Bank · Settlement presented **side by side** on wide screens, stacked with
explicit source labels on narrow. Aligned field-by-field so a discrepancy is visible by
scanning across.

**The differing field is marked explicitly:**

```
Amount            ⚠ Mismatch
  Payment      ₹1,200.00
  Bank         ₹1,215.00      ← differs
  Settlement   ₹1,200.00
```

| Requirement | Detail |
|---|---|
| **Never colour alone** | An explicit "Mismatch" label, an icon, and a typographic emphasis — colour reinforces |
| Missing source | Rendered as an explicit "No record" state, never a blank cell |
| Amounts | Tabular numerals, right-aligned, as returned by the API |
| Raw evidence | `discrepancyDetail` available in a collapsed monospace region — it is a system record, and should look like one |

### Queue navigation

**Previous / next** within the current filter, with position ("4 of 17"). Keyboard
shortcuts are welcome. This is what turns a viewer into a workflow — an operator works a
queue, they do not browse a list.

---

## Independent verification

`/runs/:runId/verify` — the product's most differentiated surface.

**Input** upload or paste a `GroundTruthRow[]` dataset. Explain plainly what is being
supplied and why it is independent.

**PASS**
> **Independently Verified**

Confident, restrained, unmistakable. The one permitted celebratory moment in the product.
Not cartoonish, no confetti.

**FAIL**
> **Verification failed**

Followed by the **complete** `failures[]` list — every discrepancy, not the first. A
visible "3 of 100 discrepancies found" is more credible than silence, and is exactly what
the honesty standard requires.

**Either way** show expected-vs-actual side by side: total units, each of the five status
counts, and match rate.

**Never** fabricate a result, hardcode a percentage, or hide a failure.

---

## Cross-cutting requirements

| Requirement | Applies to |
|---|---|
| All four states — loading, empty, error, loaded | Every list and every async surface |
| Skeletons match final layout | Every table and card |
| Tabular numerals | Every number, everywhere |
| Status carries a text label | Every status indication |
| Server-side pagination | Every paged list |
| No client-side recomputation of financial values | Everything |
| Errors offer retry where meaningful | Every failed fetch |
| Nullable fields render meaningfully | `matchRate`, `strategyUsed`, all `ai*` fields |
