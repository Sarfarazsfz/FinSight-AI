# Accessibility and Responsive Behaviour

Both are acceptance gates, not polish. A phase that fails either is not complete — see
[quality/02-visual-qa.md](../quality/02-visual-qa.md).

---

## Responsive strategy

**Desktop-first**, because this is finance-operations software used at a desk. Tablet and
mobile are genuinely supported, not merely non-broken.

| Breakpoint | Target |
|---|---|
| ≥1280 | Full workspace — rail + content + drawer |
| 1024–1279 | Full workspace, narrower content |
| 768–1023 | Rail collapses to a slim top bar |
| 640–767 | Single column; tables scroll horizontally in their own container |
| <640 | Mobile — drawer becomes a full-screen sheet |

### Rules

| Rule | Detail |
|---|---|
| **The page body never scrolls horizontally** | Wide content scrolls **inside its own** `overflow-x: auto` container |
| Tables | Horizontal scroll within the table container; sticky header preserved where practical |
| Evidence comparison | Three columns → stacked with **explicit source labels** — a stacked comparison without labels is unreadable |
| Drawer | Right-side panel → full-screen sheet below 640 |
| Landing | Multi-column → single column; the workflow stepper stacks vertically |
| Touch targets | Minimum 44×44 on touch viewports |
| Relative units | Type and spacing scale with the viewport; no fixed pixel layouts |
| Images/media | `max-width: 100%` |

### Public vs application density

The public surface stays **airy** at every breakpoint. The application is where density
lives — but density means efficient use of space, never cramped text or sub-minimum
touch targets.

---

## Accessibility

Target: **WCAG 2.1 AA**.

### Structure

- Semantic landmarks — `<header>`, `<nav>`, `<main>`, `<aside>`, `<footer>`
- One `<h1>` per page; heading order never skips a level
- A **skip-to-content** link, visible on focus
- Lists marked up as lists; tables marked up as tables

### Tables

- `<th scope="col">` on every header cell
- A `<caption>` or `aria-label` on every table
- Sortable headers expose `aria-sort`
- Never a `<div>` grid pretending to be a table — screen readers lose all row/column
  relationships

### Forms

- Every input has a visible, associated `<label>`. **Placeholders are never labels**
- `aria-invalid` on invalid fields, `aria-describedby` pointing at the message
- Error messages are text — never colour or an icon alone
- Enter submits; a form is never keyboard-trapped

### Status and colour

> **Status must never depend on colour alone.**

Every status carries a text label. Colour and icon are reinforcement. This applies to all
six tokens — Matched, Mismatched, Missing, Duplicate, Unresolved, Pending — and to the
evidence screen's discrepancy marking, which additionally uses an explicit "Mismatch"
label.

Contrast: **4.5:1** for body text, **3:1** for large text and meaningful UI boundaries.
Verify the muted-text and status-token pairs specifically — muted text on a tinted status
background is the most likely failure.

### Keyboard

- Every interactive element reachable and operable by keyboard
- **Visible focus on everything** — one consistent focus treatment product-wide
- Logical tab order following visual order
- Drawer and modal: CDK focus trap, **Escape closes**, **focus returns to the trigger**
- Table rows opening a detail view expose a real focusable control — not a click handler
  on a `<div>`
- Queue navigation (previous / next exception) is keyboard-operable

### Dynamic content

- `aria-live="polite"` for async results loading into an existing view
- `role="alert"` for errors requiring attention
- Loading states announced, not merely animated
- Skeletons `aria-hidden`, with an accessible "Loading…" announcement alongside

### Icons

Decorative icons `aria-hidden="true"`. Icon-only controls carry an `aria-label`. See
[design/02-icon-system.md](02-icon-system.md#accessibility).

### Motion

`prefers-reduced-motion: reduce` disables all transitions, shimmer, and entrance
animation. Implemented in the **token layer** so no component can opt out.

---

## Per-phase verification

| Check | Method |
|---|---|
| Keyboard-only pass | Complete the phase's primary task without a mouse |
| Focus visibility | Tab through every interactive element |
| Heading order | Inspect the accessibility tree |
| Table semantics | Confirm headers and captions are exposed |
| Contrast | Check body, muted, and all six status pairs |
| Reduced motion | Toggle the OS setting and re-verify |
| Three viewports | 1280 · 768 · 375 |
| No horizontal body scroll | Confirm at every breakpoint |

`[CODE]` The existing frontend already satisfies several of these — labelled inputs,
`aria-invalid`/`aria-describedby`, `role="alert"`, a skip link, keyboard-operable
dropzones, and one consistent focus ring. **Preserve that behaviour through the
rewrite**; it is part of the KEEP set even though its markup is being replaced.
