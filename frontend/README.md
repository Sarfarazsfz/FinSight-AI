# FinSight — Frontend

Angular 20 client for **FinSight — Financial Reconciliation Intelligence**.

See the [root README](../README.md) for the product overview and [`docs/`](../docs/README.md) for the authoritative architecture, design system and roadmap.

## Stack

| | |
|---|---|
| Framework | Angular 20 — standalone components, signals, `OnPush`, lazy routes |
| Language | TypeScript, strict mode |
| Styling | Tailwind CSS v4 with CSS custom properties as the token layer |
| Typography | Inter, self-hosted via `@fontsource/inter` — no external runtime assets |
| Change detection | zone.js |

No UI component library, no icon library, no state-management library. Dependencies are added only when a real consumer exists.

## Current scope

Implemented across phases F0–F3:

- **F0** Angular 20 scaffold with a verified Tailwind v4 toolchain
- **F1** FinSight design tokens — colour, typography, numeric/tabular type, spacing, radius, shadow, motion, reduced-motion, focus-visible; all contrast pairs meet WCAG AA
- **F2** Typed HTTP infrastructure — `AuthApi`, `AuthStore`, bearer and error interceptors, route guard, `ProblemDetails` model
- **F3** Login, authenticated application shell, protected batches entry

### Routes

| Route | Description |
|---|---|
| `/` | Redirects by session state |
| `/login` | Authentication — the only entry point; the backend has no registration or refresh endpoint |
| `/batches` | Authenticated entry point (guarded). Fetches nothing yet; batch integration is a later phase |

Screens for upload, reconciliation, results, exceptions, AI explanation and verification are not built yet and are deliberately absent from navigation rather than shown as disabled items.

## Backend requirement

The API must be running for authentication to work:

```bash
cd ../backend/FinSight.Api
dotnet run
```

It listens on `http://localhost:5180`, which `src/environments/environment.development.ts` targets. The API's development CORS policy allows `http://localhost:4200` by default.

There is no seeded development user — accounts are provisioned directly against the database.

## Commands

```bash
npm install                                          # install dependencies
npm start                                            # dev server on http://localhost:4200
npm run build                                        # production build
npm test -- --watch=false --browsers=ChromeHeadless  # unit tests
```

## Design direction

**"Ledger, not dashboard."** Warm light neutral surfaces, near-black type, a single restrained accent, hairline borders, minimal shadows, tabular numerals on every figure. Six semantic reconciliation statuses, each always paired with a text label — status is never conveyed by colour alone.

The design system is specified in [`docs/design/01-design-system.md`](../docs/design/01-design-system.md); all tokens live in `src/styles.css`.

## Conventions

- The frontend never computes a financial value. Match rate, statuses, reason codes and counts are rendered exactly as the API returns them.
- Structured validation errors are rendered from `ProblemDetails.errors[]`; the human-readable `detail` string is never parsed to reconstruct fields.
- No secret, key or token is stored in source. The only persisted value is the signed-in user's own JWT.
