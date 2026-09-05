# Performance and Throughput

**No benchmark number is fabricated in this document.** Any figure quoted anywhere must
trace to a real measurement of a real run.

---

## Backend throughput — measured and surfaced

`[OFFICIAL WEB]` The official bar is *"Throughput plus measured accuracy plus an honest
exception list."* Throughput is named explicitly.

### Status: MEASURED

Two independent, real measurements exist. Neither is estimated.

| Source | Where | Reachable? |
|---|---|---|
| `Stopwatch` in `ReconciliationOrchestrator` | Writes `duration_ms` and `records_per_second` into the `ReconciliationCompleted` audit payload | **Yes** — `IAuditLogReader` (read-only, ownership-scoped) exposes it on `GET /api/reconciliation/runs/{runId}/audit`, also rendered in the Run Workspace's "Audit evidence" section |
| `ReconciliationRun.StartedAt` → `CompletedAt` | Persisted on the run row | **Yes** — surfaced as `durationMs` / `recordsPerSecond` on `GET /api/reconciliation/runs/{runId}/summary` |

These two figures measure genuinely overlapping-but-not-identical windows — the audit
`Stopwatch` starts before the batch lookup and stops before the run's row is persisted;
`StartedAt`/`CompletedAt` bracket a slightly narrower span — so they can legitimately
differ by the batch lookup's own latency. Neither is recomputed from the other, and
neither is treated as more authoritative; both are shown as what they are: independent
wall-clock measurements of the same run.

`StartedAt` is stamped when the run is constructed and `CompletedAt` when it completes, so
the interval brackets the matching and classification loop. `ReconciliationSummaryBuilder`
computes both fields server-side; the frontend performs no timing arithmetic.

`[CODE]` Zero-duration and not-yet-completed runs return **null**, never `0` and never a
fabricated rate. The Run Workspace renders "This run has not completed, so no duration was
recorded" rather than a number.

### What this figure is, and is not

It **is** a single wall-clock measurement of one run, on whatever machine executed it,
between that run's own recorded start and completion.

It is **not**:

- a benchmark
- a cold-vs-warm comparison — **no cold/warm harness exists**, and none may be claimed
- a production throughput figure
- a sustained-load or concurrency measurement

The UI states this limitation directly next to the number.

### If a real benchmark is ever built — the protocol

1. Generate a fresh ~100-unit batch.
2. Ingest via `POST /api/batches`.
3. Trigger `POST /api/reconciliation/runs`.
4. Record: records processed (`totalUnits`) · wall-clock duration · records/second.
5. Record alongside it: batch size · machine spec (do **not** claim a production
   environment) · PostgreSQL version · **cold vs warm** run.
6. Report **both** cold and warm. Do not average away the cold start to make the number
   look better.

Until steps 5–6 are done, the single-run figure above is all that may be quoted.

### A genuine architectural strength

`[CODE]` Reconciliation never calls an AI provider. Its throughput therefore **cannot** be
degraded by AI latency or an AI outage. State this explicitly — it is a real property of
the design, not a marketing line.

### Explicitly not measured, not claimed

Behaviour at 1,000+ or 10,000+ records · concurrent simultaneous runs · sustained load ·
memory profile. None has been measured; none may be claimed.

---

## Frontend performance

### Baseline `[CODE]`

Current production build (F3): **302.11 kB raw / 83.91 kB estimated transfer** (initial),
0 errors, 0 warnings. This is the number later phases must not regress.

`[CODE]` The rebuilt frontend uses Tailwind's generated utilities with **zero** hand-written
component stylesheets. Measure again after each phase rather than assuming the figure holds.

### Techniques

| Technique | Application |
|---|---|
| Lazy routes | `loadComponent` per feature — already in place |
| `OnPush` | Every component |
| `@defer` | Below-the-fold landing sections |
| Server-side pagination | Every list — never fetch-all-and-slice |
| Self-hosted fonts | `@fontsource/inter` — no external runtime assets |
| Tailwind purging | Only used utilities ship |
| Icon tree-shaking | Import only the Lucide icons actually used |
| Image discipline | No stock imagery; any asset optimised and sized |

### Budgets

Keep Angular's budget configuration meaningful. If a budget must be raised, that is a
signal to examine the cause — not a formality. `[CODE]` The component-style budget was
previously raised for a large SCSS file; after the Tailwind migration it should be
reviewed downward rather than left loose.

### Per-phase checks

- Production build succeeds with **0 errors and 0 warnings**
- Initial bundle has not regressed against the recorded baseline
- No console errors
- No layout thrash or long tasks on route transitions
- Skeletons appear promptly — perceived performance matters more than raw milliseconds
  on a data-fetching screen

### Not optimised for

Sub-second cold start on a throttled 3G connection · offline operation · service workers ·
SSR — none is a requirement, and each adds build complexity with no benefit here.
