# Icon System

## Standard: Lucide only

**Lucide is the single icon library for the production UI.** One library, one stroke
weight, one sizing scale, one wrapper component.

> **Status:** target. `[CODE]` Lucide is **not installed** — no icon has a real
> consumer yet. The current frontend contains **zero** icons: no inline SVG, no
> HTML-entity glyphs, no emoji. Lucide is introduced in the phase whose UI first
> needs an icon, so this standard is met from the first icon onward.

---

## Prohibited — no exceptions

| Prohibited | Examples |
|---|---|
| **Emoji** | 🚀 ✅ 📊 — anywhere in the product UI |
| **Unicode decorative symbols** | ★ ▲ ● ◆ |
| **Manually typed UI symbols** | `✓` `→` `←` `↓` `⚠` |
| **HTML entity glyphs used as icons** | `&larr;` `&rarr;` `&darr;` `&hellip;` `&middot;` |
| **Ad-hoc / hand-drawn SVG** | Inline paths written by hand or generated |
| **AI-generated icons** | Any |
| **A second icon library** | Material Icons, Font Awesome, Heroicons, Bootstrap Icons |
| **Icon fonts** | Ligature-based icon fonts of any kind |

### Why entity glyphs are not acceptable

They look like icons and behave like text: they inherit font metrics, render differently
across platforms, resist consistent sizing and alignment, and are announced literally by
screen readers. A back arrow rendered as `&larr;` is announced as "leftwards arrow"
inside the link text. `[CODE]` The current frontend has exactly this problem in 11 places.

### The one permitted exception

Typographic punctuation used **as punctuation**, not as an icon — a true ellipsis in
"Loading…", a middot separating metadata. These are text and are fine. An arrow standing
in for a navigation affordance is not.

---

## Usage rules

**Icons must be functional.** Every icon either identifies an action, indicates a state,
or aids scanning in a dense table. An icon that decorates a heading is deleted.

| Rule | Detail |
|---|---|
| **Sizes** | 16 (inline/table) · 20 (buttons/nav) · 24 (headers) — no other sizes |
| **Stroke** | One consistent width across the product, Lucide's default |
| **Colour** | `currentColor` always — icons inherit their context, never carry their own palette |
| **Alignment** | Optically centred against the label's cap height, not the line box |
| **Pairing** | Icon + text label by default. Icon-only is permitted **only** for universally understood controls (close, previous, next) and **must** carry an accessible name |

### Accessibility

| Case | Markup |
|---|---|
| Decorative (label adjacent) | `aria-hidden="true"` |
| Icon-only control | `aria-label` on the control, `aria-hidden` on the icon |
| Status icon | Accompanied by visible text — **never the sole carrier of status** |

Restating the system-wide rule: **status is never colour alone, and never icon alone.**
An icon plus colour plus a text label is the minimum for a reconciliation status.

---

## Implementation

A single `<app-icon>` wrapper over `lucide-angular` gives one place to enforce size,
stroke, colour inheritance, and `aria-hidden` defaults — and one place to change if the
library ever does.

Import only the icons actually used, so the bundle carries no unused set.

### Icon vocabulary — stable meanings

Assign each concept **one** icon and never reuse it for a second meaning:

| Concept | Suggested Lucide icon |
|---|---|
| Batch / records | `layers`, `file-text` |
| Upload | `upload`, `upload-cloud` |
| Reconciliation run | `refresh-cw`, `git-compare` |
| Matched | `check-circle` |
| Mismatched | `alert-triangle` |
| Missing | `circle-slash` |
| Duplicate | `copy` |
| Unresolved | `help-circle` |
| Evidence / detail | `search`, `file-search` |
| AI explanation | `sparkles` — **used once, for the AI panel only**, never as generic decoration |
| Finance Assistant | `message-square` |
| Verification | `shield-check` |
| Verified (pass) | `badge-check` |
| Failed | `x-circle` |
| Previous / next | `chevron-left` / `chevron-right` |
| Close | `x` |
| External link | `external-link` |

Fix this table in **P2** and treat it as the vocabulary. Adding a synonym for an existing
concept is how icon sets become incoherent.

---

## Verification

Every phase's visual QA includes a **grep for banned glyphs** across `frontend/src` —
emoji ranges, `&larr;`/`&rarr;`/`&darr;`/`&uarr;`, `✓ → ★ ⚠ ●`, and inline `<svg>` outside
the icon wrapper. See [quality/02-visual-qa.md](../quality/02-visual-qa.md).
