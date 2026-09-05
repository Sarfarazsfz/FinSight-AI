# Routes and Screens

> **SUPERSEDED PLANNING ARTIFACT.** This document is the pre-implementation *target* route
> map and screen specification written before the F-series frontend phases were built. The
> as-built application diverges from it in several concrete ways (see "Divergence from the
> as-built application" below, and the correction notes inline on the affected sections).
> **It is not a current reference.** For the actual routes, read
> [frontend/src/app/app.routes.ts](../../frontend/src/app/app.routes.ts) directly; for the
> actual API surface, read [api/01-contract.md](../api/01-contract.md); for current feature
> status, read the root [README.md](../../README.md). The screen-level intent captured here
> (states, a11y notes, wording) mostly still holds and is kept for its planning/historical
> value — only the route *shape* and a handful of specific claims listed below are stale.

Target route map and per-screen specification. Every screen maps to a **real** endpoint
from [api/01-contract.md](../api/01-contract.md).

---

## Route map (as planned — see divergence note below for what was actually built)

```
/                                       Landing                    public
/login                                  Login                      public

/batches                                Batch history              authenticated home
/batches/upload                         Batch upload
/batches/:batchId                       Batch detail                [PLANNED ONLY -- not built, see below]

/runs/:runId                            RunShell → redirect to overview
  ├ overview                            Run overview                [PLANNED AS A SUB-ROUTE -- see below]
  ├ results                             Results table
  │   └ ?resultId=…                     Evidence drawer (query param, not a route) [PLANNED ONLY -- built as a real route instead, see below]
  ├ exceptions                          Exception queue
  ├ exceptions/:exceptionId             Exception detail (full page)
  ├ assistant                           Finance Assistant            [PLANNED AS A ROUTE -- built embedded instead, see below]
  └ verify                              Independent verification

**                                      Not found
```

All routes lazy-loaded via `loadComponent`. Everything except `/` and `/login` is behind
`authGuard`. **These two facts still hold in the as-built app.**

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
- ~~**`/audit` not built.** `[CODE]` No endpoint exists.~~ **Superseded (P-1H).** A
  read-only backend endpoint now exists — `GET /api/reconciliation/runs/{runId}/audit` —
  and its evidence is surfaced in the Run Workspace under an "Audit evidence" section.
  There is still **no standalone `/audit` frontend route**; the evidence is embedded in
  `/runs/:runId`, the same way Finance Assistant is. See
  [api/01-contract.md](../api/01-contract.md) for the endpoint contract.

### Divergence from the as-built application — recorded (P-1I audit)

The route map above was the *plan*. The application that actually shipped diverges from it
in four ways. **The real route table is
[frontend/src/app/app.routes.ts](../../frontend/src/app/app.routes.ts); this list exists
only to correct this document, not to replace that file as the source of truth:**

- **No `/runs/:runId/overview` sub-route exists.** The run-overview content renders
  directly at `/runs/:runId` — there is no child route, no redirect, and no `RunShell`
  wrapper component distinct from the page itself.
- **No `/runs/:runId/assistant` route exists.** Finance Assistant is not a route at all —
  it is embedded directly in the Run Workspace page: a persistent right-side rail on
  desktop, a drawer/sheet on narrower viewports. See the "Finance Assistant" note further
  below.
- **No `/batches/:batchId` route exists.** There is no batch-detail page; `/batches` (the
  history list) and `/batches/upload` are the only batch-scoped routes. A run is created
  directly from the upload flow rather than from a separate detail screen.
- **Results evidence is a real routed page, not a query-param drawer.**
  `/runs/:runId/results/:resultId` is an actual route (confirmed in `app.routes.ts`), not
  `?resultId=…` over `/results`. See the "Evidence drawer" note further below.

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

> **Not built.** No batch-detail route exists in the shipped app (confirmed in
> `app.routes.ts`) — `/batches` and `/batches/upload` are the only batch-scoped routes. The
> plan below was never implemented this way; kept for historical/planning reference only.

- **API** `GET /api/batches/{batchId}`.
- **Content** batch summary, record counts, validation status, **Run reconciliation**
  primary action → `POST /api/reconciliation/runs` → navigate to the new run.
- **States** loading · not-found (404) · loaded · running.

### `/runs/:runId` — RunShell

> **Built differently.** The shipped Run Workspace has no separate `overview` child route
> and no distinct `RunShell` wrapper — this page's own content (header, five-count
> breakdown, run performance, audit evidence) renders directly at `/runs/:runId`. There is
> also no tab bar; Results, Exceptions, and Verify are reached via in-page links/buttons to
> their own routes, and Finance Assistant is a persistent rail/drawer on this same page
> rather than a tab. The plan below describes the pre-implementation intent, not the
> as-built page.

- **API** `GET /api/reconciliation/runs/{runId}/summary`, fetched once into
  `RunContextStore`.
- **Sticky header** batch label · run status · match rate · five status counts.
- **Tabs** Overview · Results · Exceptions · Assistant · Verify.
- Redirects to `overview` by default.

### `/runs/:runId/overview` — Run overview

> **Not a separate route.** This content is what actually renders at `/runs/:runId`
> itself — there is no `/overview` child route or redirect in the shipped app. The screen
> intent described below (match-rate hero, five-way breakdown, nullable `matchRate`
> handling) does still broadly describe the real Run Workspace; only the route shape is
> stale.

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
- Row click → navigates to the result's own routed page (see below) — not a query-param
  drawer over this one.

### Result evidence — `/runs/:runId/results/:resultId` (built as a real routed page, not a drawer)

> **Built differently than planned.** This is a real route,
> `/runs/:runId/results/:resultId` (confirmed in `app.routes.ts`), not `?resultId=…` over
> `/results` and not an overlay/drawer. The page's own source explicitly documents this
> choice: *"This is a plain routed page, not a drawer/overlay — deliberately..."*
> (`result-detail-page.ts`). The content and evidence-presentation intent described below
> still applies; only "drawer" and "query param" are stale.

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

### Finance Assistant — embedded in the Run Workspace, not a route

> **Not a route.** There is no `/runs/:runId/assistant` path in the shipped app. Finance
> Assistant is embedded directly in `/runs/:runId`: a persistent right-side rail on
> desktop, a drawer/sheet on narrower viewports (`FinanceAssistantPanel`, rendered inline
> by the Run Workspace page). The interaction/content intent below still applies; only the
> "own route" framing is stale.

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

> **Built differently.** The shipped wildcard route (`app.routes.ts`) redirects to `/`
> (the landing page), not `/batches` — there is no dedicated not-found screen with its own
> content; an unknown path simply lands on Landing.

Plain, calm, one route back to `/batches`. No illustration.
