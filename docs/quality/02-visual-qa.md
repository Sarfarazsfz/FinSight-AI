# Visual QA

A mandatory gate on every frontend phase. A phase that compiles, passes tests, and looks
generic has **failed**.

---

## Why this is a formal gate

The current frontend `[CODE]` compiles cleanly, passes its checks, renders without console
errors, and was still judged too template-like. Correctness and quality are different
properties and need different gates.

---

## Checklist — every phase

### 1. Banned content — grep, do not eyeball

| Check | Expectation |
|---|---|
| Emoji anywhere in `frontend/src` | **Zero** |
| Entity glyphs used as icons — `&larr; &rarr; &darr; &uarr; &hellip;` as affordances | **Zero** |
| Typed symbols — `✓ → ← ★ ⚠ ●` | **Zero** |
| Inline `<svg>` outside the icon wrapper | **Zero** |
| **`"Track 04"`, `"Buildathon"`, `"Track"`, `"Phase N"`** in any user-visible string | **Zero** |
| Lorem ipsum / placeholder copy | **Zero** |
| Hardcoded hex colours or px values in component styles | **Zero** — tokens only |
| Hardcoded API URLs outside `environment` | **Zero** |
| Secrets, keys, tokens | **Zero** |

`[CODE]` The current frontend **passes** these gates: zero entity glyphs, zero inline
SVG, zero buildathon-context leaks. The grep gates are re-run every phase.

### 2. Design-system conformance

- Every spacing value from the scale — `4/8/12/16/24/32/48/64`
- Every colour from a token; no raw values
- Type from the defined scale; no ad-hoc sizes
- **Tabular numerals on every number** — amounts, counts, percentages, page numbers, row
  numbers
- Numeric table columns right-aligned
- Radii and borders from the token set
- No drop-shadow card grids; hairline rules doing the separation work
- One accent colour, used only for primary action / link / focus / active / verified

### 3. States — all four, deliberately

Every list and async surface must **actually render**: loading (skeleton matching final
layout, not a spinner) · empty (honest, distinguishing "nothing yet" from "nothing
matches") · error (with retry where meaningful) · loaded.

Force each state and look at it. A state that exists in code but has never been viewed
has not been designed.

### 4. Status treatment

- Every status shows a **text label** — never colour alone, never icon alone
- The six tokens are used consistently across badges, tables, and bars
- `Unresolved` is visually distinct from every other status
- Evidence discrepancies carry an explicit "Mismatch" label plus icon plus emphasis

### 5. Responsive — three viewports minimum

**1280 · 768 · 375.** At each: no horizontal body scroll · wide content scrolls inside
its own container · touch targets ≥44px on touch viewports · stacked evidence retains
explicit source labels · nothing overlaps, clips, or overflows.

### 6. Accessibility

Keyboard-only completion of the phase's primary task · visible focus everywhere · correct
heading order · table headers and captions exposed · form labels associated · contrast AA
for body, muted, and all six status pairs · `prefers-reduced-motion` respected · drawer
traps focus, Escape closes, focus restored.

### 7. Console and network

Zero console errors · zero unhandled promise rejections · no failed requests other than
those deliberately being tested · no 404s for assets.

### 8. The judgement question

> **Does this look like a financial system of record, or like a dashboard template?**

Signals it is drifting template-ward: card grids everywhere · everything boxed and
shadowed · colour used decoratively · rounded pills as the dominant shape · icons
decorating headings · centred marketing-column layout inside the workspace · numbers
without tabular alignment.

If the answer is "template", the phase is not done — regardless of test results.

---

## Method

The Browser pane is the tool of record. Prefer text-based verification where possible —
it is more reliable and reviewable than screenshots:

| Purpose | Tool |
|---|---|
| Content, structure, accessible names, heading order | Read the page / accessibility tree |
| Console errors | Console messages |
| Failed or unexpected requests | Network requests |
| Computed styles, token resolution | Evaluate in the page |
| Responsive | Viewport emulation at the three widths |
| Interaction | Drive the real controls, then re-read the page to confirm the result |
| Visual proof | Screenshot, when the change is genuinely visual |

**Never ask the user to check something manually.** Verify it and report the evidence.

---

## Reporting

Each phase report states, explicitly:

1. What was verified and how
2. Which checks passed
3. **Which checks failed or could not be run, and why** — never silently omitted
4. Screenshots or extracted output as evidence
5. Anything deferred, with the reason

An honest "could not verify X because Y" is worth more than an unqualified pass.
