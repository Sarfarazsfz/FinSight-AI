# Design System

**Design north star: "LEDGER, NOT DASHBOARD."**

A financial system of record — dense where density serves the reader, quiet everywhere
else. Not an analytics template.

**Implementation:** Tailwind CSS v4 as the styling engine; CSS custom properties (via
Tailwind's `@theme`) as the token layer. SCSS is **not** a competing design system. See
[ADR-008](../adr/README.md#adr-008-tailwind-css-v4-with-css-custom-properties).

> **Status:** target specification. Nothing is installed yet. `[CODE]` The current
> frontend implements a different, SCSS-based, visually generic system that this
> document replaces. See
> [frontend/01-architecture.md](../frontend/01-architecture.md#current-vs-target).

---

## Superseded baseline — recorded, not merged

`[ZIP]` doc 18 specifies a **dark** system: near-black `#0E0C0A` base, warm ivory text,
restrained amber `#C79A4B` accent, editorial serif headings.

**Resolution:** the approved direction is a **warm light neutral** system with near-black
type and a single restrained accent. `[ZIP]` doc 18's palette is **superseded**.

**Retained permanently from `[ZIP]` doc 18** — its single governing rule, which is a
product principle rather than a visual choice:

> **Verified financial data must be visually stronger than AI commentary — always.**

Also retained: no drop-shadow card grids, no gradient heroes, no emoji, no filled bright
status pills, no consumer chat-bubble styling, right-side drawer for investigation.

---

## Token layer

All tokens are CSS custom properties, declared once in Tailwind's `@theme` block and
consumed through Tailwind utilities. **No component may hardcode a raw hex or px value.**

### Colour — base

| Token | Role |
|---|---|
| `--color-bg` | Warm light neutral page background |
| `--color-surface` | Cards, tables, panels — one step from the page |
| `--color-surface-sunken` | Inset regions, code/payload previews |
| `--color-border` | Hairline dividers and table rules |
| `--color-border-strong` | Input borders, emphasised separation |
| `--color-text` | Near-black primary |
| `--color-text-muted` | Secondary/metadata |
| `--color-text-faint` | Tertiary, column labels |
| `--color-ink` | Dark surface for the primary CTA and dark bands |
| `--color-on-ink` | Text on dark surfaces |

### Colour — accent

**Exactly one accent.** Reserved for: primary action, links, focus ring, active
navigation, and the verified state. **Never decorative.** Tokens: `--color-accent`,
`--color-accent-strong` (hover/active), `--color-accent-soft` (tint background).

### Semantic status colours

Six tokens, each a foreground/background pair. Used identically in badges, table cells,
and status bars so the reader pattern-matches instantly.

| Status | Token pair | Meaning |
|---|---|---|
| **Matched** | `--status-matched{,-bg}` | Reconciled successfully |
| **Mismatched** | `--status-mismatched{,-bg}` | Sources disagree on amount or date |
| **Missing** | `--status-missing{,-bg}` | A source has no counterpart record |
| **Duplicate** | `--status-duplicate{,-bg}` | More than one record for a reference |
| **Unresolved** | `--status-unresolved{,-bg}` | Cannot be determined — deliberately distinct |
| **Pending** | `--status-pending{,-bg}` | Run not yet complete |

`Unresolved` must not look like a variant of any other status. "We don't know yet" is a
distinct, honest outcome and must read as one.

> ### Status is never colour alone — mandatory
>
> Every status indication carries a **text label**. Colour is reinforcement, never the
> signal. This is an accessibility requirement *and* a correctness requirement: a
> financial status conveyed only by hue is unreadable to a colour-blind operator and
> invisible in print or a screenshot.

`ExceptionCategory` maps onto the same six tokens — no second colour system:
`AmountMismatch`/`DateMismatch` → mismatched · `MissingRecord` → missing ·
`DuplicateRecord` → duplicate · `Unresolved` → unresolved.

### Feedback

`--color-danger{,-bg}` for destructive and error states — deliberately distinguishable
from `--status-unresolved`, since "this failed" and "this could not be classified" are
different messages.

---

## Typography

**One family.** A high-quality sans with excellent numerals. `[CODE]` Inter is already
self-hosted via `@fontsource/inter` — **keep it**. No external runtime font assets, ever.

`[ZIP]` doc 18's editorial-serif heading pairing is **deferred**, not adopted: two
families is a real coherence risk. Revisit only once the single-family system is proven.

### Scale

| Token | Use |
|---|---|
| `display` | Landing hero only |
| `heading-1` | Page titles |
| `heading-2` | Section titles |
| `heading-3` | Card and panel titles |
| `body-lg` | Landing subheads, lead paragraphs |
| `body` | Default |
| `small` | Table cells, secondary text |
| `meta` | Uppercase eyebrows, column labels, timestamps |
| `numeric-hero` | Match rate |
| `numeric-lg` | Stat tiles |

Headings use tight leading and slight negative letter-spacing. Body uses generous
leading. Line length caps around 60–75 characters for prose.

### Numeric typography — non-negotiable

**`font-variant-numeric: tabular-nums` on every amount, count, percentage, page number,
row number, and metric.** In a ledger, digits must align vertically down a column. This
single property does more to make the product read as financial software than any colour
decision.

Additional rules: amounts and counts **right-aligned** in tables · currency rendered as
the API returns it, formatted not recomputed · never abbreviate a financial figure
(`1.2k` is not acceptable for a reconciliation count).

---

## Spacing, containers, grid

**Spacing scale:** `4 · 8 · 12 · 16 · 24 · 32 · 48 · 64`. Nothing off-scale.

Generous whitespace *between* sections; tight, deliberate spacing *within* a data row.
Density is earned in tables, not sprayed everywhere.

**Containers:** public content max ~1180px, centred, with consistent inline padding.
Application content is wider and left-aligned against the shell rail — an operator
workspace, not a centred marketing column.

**Grid:** CSS Grid and Flexbox via Tailwind utilities. No grid framework.

---

## Borders, radii, shadows

- **Hairline `1px` borders** are the primary separation device. Thin dividers over
  boxes; a rule between rows beats a card around each row.
- **Radii** small (inputs, badges) · medium (buttons, cards) · large (major panels,
  modals). Deliberately restrained — heavily rounded corners read as consumer SaaS.
- **Shadows** minimal. At most a subtle shadow on genuinely floating surfaces (drawer,
  modal, sticky header). **Hierarchy comes from spacing and typography, not elevation.**
  No shadowed card grids.

---

## Components

### Buttons

| Variant | Use |
|---|---|
| **Primary** | Solid dark ink. One per view. The single most important action |
| **Accent** | Solid accent. Reserved for the verification action |
| **Outline** | Hairline border, transparent. Secondary actions, "Log in" in public nav |
| **Ghost** | Text only. Tertiary, toolbar, destructive-adjacent |

Consistent height, `gap` for icon+label, disabled at reduced opacity with
`cursor: not-allowed`, loading state replaces the icon with a spinner and keeps the label.

### Inputs

Hairline border, medium radius, comfortable height, visible placeholder in faint text.
Focus uses the accent ring. Invalid state adds a danger border **plus** a text message —
never colour alone. Labels always visible; placeholders are never labels.

### Tables

The most important component in the product.

- Sticky header; `meta`-styled uppercase column labels
- Hairline row rules, **no zebra striping** (dated, and it fights status backgrounds)
- Subtle row hover
- Numeric columns right-aligned with tabular numerals
- Row click opens detail; the whole row is the target, with a keyboard-focusable control
- **Horizontal scroll inside the table's own container** — the page body never scrolls
  horizontally
- `<th scope="col">` on every header; a caption or `aria-label` on every table

### Filters and search

Inline above the table, not in a drawer. Status/category filters are toggle chips
reflecting the six status tokens. Active filters are visible and individually
clearable. Filtering operates over the fetched page — **pagination stays server-side**.

### Badges

Label + status colour, subtle tinted background, small radius. **Always includes text.**
No large filled pills, no colour-only dots.

### Navigation

Public: sticky, thin, hairline bottom border, wordmark left, minimal links, outlined
"Log in" plus solid primary CTA right.
Application: compact left rail, five items max, active item marked by weight and an
accent edge — not a filled block.

### Drawers

Right-side, over the results table, with a scrim. CDK focus trap; Escape closes; focus
returns to the originating row. Full-screen sheet on mobile. Used for evidence, never
for AI output.

### Modals

Centred, focus-trapped, for destructive confirmation only. Nothing destructive exists
today — do not invent uses.

### Skeletons

Match the final layout's shape and size. Subtle shimmer, disabled under reduced motion.
Never a centred spinner for page content — a spinner communicates "wait", a skeleton
communicates "here is what is coming".

### Empty states

One sentence plus one action. **No illustration, no emoji.** Distinguish honestly
between "nothing exists yet" and "nothing matches your filter".

### Error states

Reuse the danger token and the badge visual language rather than inventing a new one.
Always offer a retry where retrying is meaningful. Never dead-end.

---

## Motion

150–200 ms, ease-out. Purposeful only: route and tab transitions, drawer slide, skeleton
shimmer, state changes. **`prefers-reduced-motion: reduce` must disable all of it** —
implemented in the token layer so no component can opt out.

One restrained celebratory moment is permitted: the **Independently Verified** state.
Confident, not cartoonish.

---

## Patterns to avoid — DO NOT BUILD

Generic dashboard templates · excessive card grids · gradient heroes or buttons · neon ·
glassmorphism · heavy drop shadows · excessive rounded pills · stock illustration ·
AI-generated visual assets · "AI sparkle" iconography · emoji anywhere · consumer
chat-bubble styling · zebra-striped tables · filled bright status pills · animation
everywhere · **fabricated metrics of any kind**.
