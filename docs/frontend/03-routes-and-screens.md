# Routes and Screens

Target route map and per-screen specification. Every screen maps to a **real** endpoint
from [api/01-contract.md](../api/01-contract.md).

---

## Route map

```
/                                       Landing                    public
/login                                  Login                      public

/batches                                Batch history              authenticated home
/batches/upload                         Batch upload
/batches/:batchId                       Batch detail

/runs/:runId                            RunShell → redirect to overview
  ├ overview                            Run overview
  ├ results                             Results table
  │   └ ?resultId=…                     Evidence drawer (query param, not a route)
  ├ exceptions                          Exception queue
  ├ exceptions/:exceptionId             Exception detail (full page)
  ├ assistant                           Finance Assistant
  └ verify                              Independent verification

**                                      Not found
```

All routes lazy-loaded via `loadComponent`. Everything except `/` and `/login` is behind
`authGuard`.

### Divergence from `[ZIP]` doc 19 — recorded

`[ZIP]` specifies `/dashboard`, `/data`, `/reconciliation`, `/exceptions`, `/assistant`,
`/audit`, and explicitly **drops the landing page** ("internal ops tool, not a marketing
site").

**Resolved:**
- **Run-scoped map kept** over the flat map — it matches the data model, and a run is the
  object every one of those screens operates on.
- **Landing page kept.** An evaluator's first ten seconds matter, and the product needs a
  public front door. `[ZIP]`'s reasoning was sound for an internal-only tool; the current
  direction is explicitly broader.
- **`/audit` not built.** `[CODE]` No endpoint exists. Documented as deferred rather than
  faked.

---

## Screen specifications

Every screen must implement **all four states**: loading (skeleton matching final
layout) · empty · error (with retry) · loaded. A screen without all four is incomplete —
see [delivery/01-roadmap.md](../delivery/01-roadmap.md#phase-acceptance-model).

### `/` — Landing
Public, long-form. Full specification in
[design/03-landing-page.md](../design/03-landing-page.md).
**No API calls. No buildathon language. No fabricated metrics.**

### `/login` — Login
- **Goal** authenticate.
- **API** `POST /api/auth/login`.
- **Layout** minimal header, centred card, email + password, one primary action.
- **States** idle · submitting (button spinner, inputs disabled) · field-invalid ·
  auth-error (401 → inline `role="alert"`, never a toast).
- **Success** store session, redirect to `returnUrl` or `/batches`.
- **A11y** labelled inputs, `aria-invalid` + `aria-describedby`, visible focus, Enter
  submits.
- **No** sign-up link, forgot-password link, or social login — none exist.

### `/batches` — Batch history (authenticated home)
- **Goal** launch work. **A launcher, not a dashboard.**
- **API** `GET /api/batches?pageNumber&pageSize`.
- **Above the fold** concise heading, one-line explanation, **Upload batch** CTA, recent
  batch table.
- **Columns** batch label · validation status · payments · bank · settlements · total ·
  created at · action. Counts use tabular numerals, right-aligned.
- **Ordering** newest first (server-side).
- **States** skeleton rows · empty ("No batches yet — upload your first record set") ·
  error + retry · loaded + pagination.

### `/batches/upload` — Batch upload
- **API** `POST /api/batches` (multipart).
- **Layout** batch label + created-by, then **three distinct intake slots**: Payments ·
  Bank · Settlements.
- **Each slot** drag-and-drop, browse, filename, size, ready state, remove/replace.
  Professional intake, **not** giant decorated dashed rectangles.
- **Stages** `PROCESSING → VALIDATION → READY FOR RECONCILIATION`.
- **On 400 with `errors[]`** grouped by source; within each, `row · field · message`.
  **Never parse `detail`.** See [api/02](../api/02-error-handling.md).
- **On success** ingestion summary + "Go to batch".

### `/batches/:batchId` — Batch detail
- **API** `GET /api/batches/{batchId}`.
- **Content** batch summary, record counts, validation status, **Run reconciliation**
  primary action → `POST /api/reconciliation/runs` → navigate to the new run.
- **States** loading · not-found (404) · loaded · running.

### `/runs/:runId` — RunShell
- **API** `GET /api/reconciliation/runs/{runId}/summary`, fetched once into
  `RunContextStore`.
- **Sticky header** batch label · run status · match rate · five status counts.
- **Tabs** Overview · Results · Exceptions · Assistant · Verify.
- Redirects to `overview` by default.

### `/runs/:runId/overview` — Run overview
- **The most important authenticated screen.** Full spec in
  [design/04-application-ux.md](../design/04-application-ux.md#run-overview).
- **API** run summary (from shell context).
- **Above the fold** match-rate hero · total units · matched/mismatched/missing/duplicate/
  unresolved · one status bar · three actions: **View exceptions · Ask assistant ·
  Verify independently**.
- Handle `matchRate` **nullable** (a run that has not completed).

### `/runs/:runId/results` — Results
- **API** `GET …/results?pageNumber&pageSize`.
- **Columns** transaction reference · status badge · reason code · strategy used
  (nullable) · created at.
- **Filter** by status, client-side over the fetched page; **server-side pagination only**.
- Row click → evidence drawer via `?resultId=`.

### Evidence drawer (over Results)
- **API** `GET …/results/{resultId}`.
- **Content** Payment · Bank · Settlement side by side; the differing field explicitly
  marked with a label and icon, never colour alone.
- **A11y** CDK focus trap, Escape closes, focus restored to the originating row.
- Mobile: full-screen sheet.

### `/runs/:runId/exceptions` — Exception queue
- **API** `GET …/exceptions?pageNumber&pageSize`.
- **Columns** transaction reference · category · involved sources · AI-explained
  indicator · created at.
- Filter by category. Row click → exception detail.
- **Empty state** "No exceptions — every unit reconciled" — shown **only** when genuinely
  true.

### `/runs/:runId/exceptions/:exceptionId` — Exception detail
- **The hero experience.** Full spec in
  [design/04-application-ux.md](../design/04-application-ux.md#exception-investigation).
- **API** `GET /api/reconciliation/exceptions/{id}`, then
  `POST …/ai-explanation` on demand.
- **Order on screen** verified evidence **first**, AI explanation **below**, always.
- **Queue navigation** previous / next within the current filter.
- Separate loading states: evidence is fast and deterministic; AI is slow and
  network-dependent. **Never block evidence on the AI call.**

### `/runs/:runId/assistant` — Finance Assistant
- **API** `POST /api/finance-assistant/ask`.
- Scoped to this run. Answer + `toolsUsed[]` provenance chips + `traceId` when present.
- Input disabled while a request is pending — consistent with the bounded two-call design.
- **Not** chat-bubble styled. See [design/05-ai-ux.md](../design/05-ai-ux.md).

### `/runs/:runId/verify` — Independent verification
- **API** `POST …/ground-truth-verification` with a `GroundTruthRow[]` body.
- Upload or paste a ground-truth dataset.
- **PASS** "Independently Verified" — confident, restrained, not cartoonish.
- **FAIL** "Verification failed" + the **complete** `failures[]` list.
- Show expected-vs-actual counts side by side either way.
- **Never fabricate a result.** Never hardcode a percentage.

### `**` — Not found
Plain, calm, one route back to `/batches`. No illustration.
