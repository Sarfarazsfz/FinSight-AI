# Frontend Architecture

Covers the **current** state, the **target** state, the approved **rebuild decision**,
and the **state model**.

---

## CURRENT vs TARGET

This distinction is mandatory in all frontend documentation. The current frontend is an
**early foundation, not the visual source of truth.**

| Aspect | **CURRENT** `[CODE]` | **TARGET** |
|---|---|---|
| Framework | Angular 20.3.30 | Angular 20 ✅ same |
| Language | TypeScript 5.9.3 | TypeScript ✅ same |
| Components | Standalone, `OnPush` | ✅ same |
| Routing | Lazy `loadComponent`, run-scoped | ✅ same |
| State | Signals + services | ✅ same |
| Interceptors / guards | Functional | ✅ same |
| HTTP | Typed `HttpClient` | ✅ same |
| **Styling** | **Tailwind CSS v4** ✅ delivered (F0) | ✅ same |
| **Design tokens** | CSS custom properties via Tailwind `@theme` ✅ delivered (F1) | ✅ same |
| **Icons** | **None yet** — zero SVG, zero glyphs; Lucide deferred to its own phase | **Lucide only** |
| Angular CDK | Not installed | Installed, used **only** where genuinely useful |
| Fonts | Inter, self-hosted via `@fontsource/inter` | ✅ same — no external runtime assets |
| Visual language | Generic / template-like | **"Ledger, not dashboard"** — evidence-first fintech |
| Screen coverage | 2 real screens (login, batches entry); **no placeholder routes** | Full loop |
| Product-UI hygiene | **Zero leaks** — enforced by test | ✅ same |

**Status:** F0–F3 delivered the toolchain, tokens, HTTP/auth infrastructure and the
authenticated shell. Angular CDK and Lucide remain uninstalled until a real consumer
exists. See [delivery/01-roadmap.md](../delivery/01-roadmap.md#phase-naming).

---

## Rebuild decision — approved

**Keep the TypeScript layer. Rewrite the presentation layer.** This is a re-skin on a
sound skeleton, not a restart.

### KEEP / REUSE — technically sound, verified working

| Item | Why |
|---|---|
| `core/models/*` | Typed 1:1 against real DTOs, including the `errors[]` extension. Hard-won and correct. |
| `AuthStore` | Signal-based, `localStorage`-persisted, try/catch-guarded. Right size for the problem. |
| `authInterceptor` | Attaches Bearer token in one place. |
| `errorInterceptor` | 401 → clear session → redirect. Verified live against the real API. |
| `authGuard` | Functional `CanActivateFn`. |
| `AuthApi`, `BatchApi` | Thin typed wrappers; multipart upload already correct. |
| `environments/*` + `fileReplacements` | Correct dev/prod split. |
| Run-scoped route shape | Better than the `[ZIP]` flat map — matches the data model. |
| Lazy `loadComponent` routing | Correct Angular 20 idiom. |
| `app.config.ts` bootstrap | Correct. |

### REFINE — logic survives, expression changes

| Item | Change |
|---|---|
| Design token vocabulary | Same semantics, re-expressed as Tailwind `@theme` CSS variables |
| Page state machines (`'loading' \| 'loaded' \| 'empty' \| 'error'`) | Keep the pattern, rebuild the markup |
| `errors[]` grouping logic | Sound — reuse it |
| `ReconciliationApi`, `FinanceAssistantApi` | Correct boundaries, empty bodies — fill in during their phases |

### REWRITE / REPLACE

| Item | Reason |
|---|---|
| **All `.scss` files** | Removed — the frontend was rebuilt with zero SCSS |
| **Every page and component template** | This is the layer judged generic |
| Landing page copy and structure | Rebuild for long-form storytelling; strip track framing |
| App shell, marketing header | Rebuild markup; keep behaviour |
| `FileDropzone`, `SkeletonBlock`, `EmptyState` | **Keep the interaction logic** — the dropzone's keyboard and drag handling is good — rebuild markup and styles |
| All inline SVGs and HTML-entity glyphs | → Lucide |

### REMOVE

| Item | Reason |
|---|---|
| `ComingSoonPage` | Placeholder scaffolding — delete as each real screen lands |
| Every `"Track 04"` / `"Phase 5.2"` string | Internal context must never ship in product UI |

---

## Target structure

```
frontend/src/
  styles/
    tailwind.css            Tailwind v4 entry + @theme token layer
    base.css                minimal element base (reset lives in Tailwind preflight)
  app/
    core/
      models/               ← KEEP  api DTOs, ProblemDetails
      api/                  ← KEEP  AuthApi BatchApi ReconciliationApi FinanceAssistantApi
      state/                ← KEEP  AuthStore  (+ RunContextStore, new)
      guards/               ← KEEP
      interceptors/         ← KEEP
    layout/
      marketing-shell/      public surfaces
      app-shell/            authenticated workspace
      run-shell/            NEW — shared run workspace, see IA doc
    shared/ui/              button badge card data-table drawer skeleton
                            empty-state error-state icon stat field dropzone pagination
    features/
      landing/ auth/ batches/ runs/ exceptions/ assistant/ verify/
```

## Conventions — required

- **Standalone components** throughout; no `NgModule`.
- **`ChangeDetectionStrategy.OnPush`** on every component.
- **Modern control flow** — `@if` / `@for` / `@switch`; never `*ngIf` / `*ngFor`.
- **Signals** for component and shared state; RxJS remains fine for HTTP chains.
- **`input()` / `output()` / `model()`** function-based APIs, not decorators.
- **Lazy routes** via `loadComponent`; one chunk per feature.
- **No `any`** in any service return type.
- **`@defer`** for below-the-fold landing sections.

## Explicitly not used

**NgRx or any state library** · Bootstrap · Angular Material as the visual system ·
PrimeNG · any second UI kit · any second icon set · animation libraries · chart
libraries · SSR · i18n · E2E framework.

`[RECOMMENDATION]` This app is a linear read/fetch pipeline with one piece of genuinely
global state. A state-management library would add ceremony and a second source of truth
for data the server already owns. See
[ADR-009](../adr/README.md#adr-009-signals-and-services-instead-of-a-state-management-library).

### Angular CDK — only where it earns its place

| Use | Justification |
|---|---|
| `Dialog` / `Overlay` | Focus-trapped evidence drawer — correct focus management is genuinely hard to hand-roll |
| `A11yModule` / `cdkTrapFocus` | Focus restoration on drawer close |

**Not** for layout, grids, tables, styling, or theming.

---

## State model

| Scope | Mechanism | Contents |
|---|---|---|
| **Global** | `AuthStore` (signal + `localStorage`) | Session only |
| **Run-scoped** | `RunContextStore`, provided at the `RunShell` route | Run summary shared by every tab — one fetch, not one per tab |
| **Page-local** | Component signals | Explicit `'loading' \| 'loaded' \| 'empty' \| 'error'` union |
| **Server state** | `httpResource` / `rxResource` keyed on route params | Re-fetch on param change |

### The non-negotiable rule

**The frontend never computes a financial value.** Match rate, reason codes, statuses,
counts, and classifications come from the API verbatim. If a rendered number disagrees
with the API, the API is right and the UI has a bug.

This extends the backend's AI trust boundary one layer outward: *the frontend is not
financial truth either.* See
[ADR-007](../adr/README.md#adr-007-the-frontend-is-not-financial-truth-either).

**Permitted client-side derivation** (presentation only, never persisted or reported):
filtering an already-fetched result list by status, sorting a fetched page, grouping
`errors[]` by source, formatting dates and amounts for display.

---

## Authentication behaviour

- Token attached by `authInterceptor` on every request.
- **401 → clear session → redirect to `/login`** with `returnUrl`.
- No refresh flow — `[CODE]` the backend has no refresh endpoint. Do not build one.
- No registration UI — no such endpoint exists.
- Route guard on every authenticated route, as defence in depth alongside the interceptor.

## Performance

Lazy routes · `OnPush` everywhere · `@defer` below the fold · server-side pagination only
· self-hosted fonts · realistic bundle budgets. Baseline to beat `[CODE]`: **315.82 kB
raw / 83.91 kB transfer** initial (F3). Tailwind's
generated utilities should reduce component-style weight substantially.
