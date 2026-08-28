# Performance and Throughput

**No benchmark number is fabricated in this document.** Any figure quoted anywhere must
trace to a real measurement of a real run.

---

## Backend throughput — an open gap

`[CODE]` **No timing instrumentation exists.** No `Stopwatch`, metrics counter, or
equivalent appears in `ReconciliationOrchestrator` or its dependencies.

`[OFFICIAL WEB]` The official bar is *"Throughput plus measured accuracy plus an honest
exception list."* Throughput is named explicitly, so this is **not** a cosmetic gap.

### Status: DEFERRED — requires an explicit decision

The backend is frozen. Adding instrumentation is a **separate, separately-approved**
backend change, never bundled into a frontend phase. Two honest options:

| Option | Consequence |
|---|---|
| **Instrument it** | A small, additive `Stopwatch` around the existing orchestration call. No algorithmic change. Yields a real, quotable figure. |
| **Do not instrument it** | Say so plainly. Never estimate, never imply a number. "We did not measure throughput" is credible; a fabricated figure is not. |

**Do not resolve this silently.** Decide it deliberately before the demo — see
[delivery/02-demo-runbook.md](../delivery/02-demo-runbook.md).

### If measured — the protocol

1. Generate a fresh ~100-unit batch.
2. Ingest via `POST /api/batches`.
3. Trigger `POST /api/reconciliation/runs`, timing **only** that call.
4. Record: records processed (`totalUnits`) · wall-clock duration · records/second.
5. Record alongside it: batch size · machine spec (do **not** claim a production
   environment) · PostgreSQL version · **cold vs warm** run.
6. Report **both** cold and warm. Do not average away the cold start to make the number
   look better.

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
