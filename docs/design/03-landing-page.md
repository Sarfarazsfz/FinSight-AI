# Landing Page

A long-form, premium fintech product page. Not a generic SaaS template, and not a
marketing funnel with nothing behind it.

**Route:** `/` (public) · **API calls:** none.

---

## What it must communicate

In order, and each answerable within ten seconds of reaching its section:

1. **What FinSight is** — reconciles Payment, Bank, and Settlement records into one
   measured result.
2. **What problem it solves** — three-way reconciliation is still manual and unprovable.
3. **How it works** — the seven-stage workflow, honestly.
4. **Why the output can be trusted** — deterministic engine, AI kept out of the truth
   path, independent verification.
5. **What makes it different** — the result can be *proved*, not merely claimed.
6. **How to enter the product** — one unmistakable action.

---

## Absolute prohibitions

| Never | Why |
|---|---|
| **Fabricated customers, logos, testimonials** | Dishonest, and instantly recognisable as filler |
| **Fabricated revenue, adoption, or scale numbers** | Same |
| **Fabricated performance claims** | `[CODE]` No throughput measurement exists — see [quality/03](../quality/03-performance.md) |
| **A specific accuracy percentage** | Must be read from a live run, never remembered |
| **"Production ready", "enterprise grade", "zero hallucinations", "real-time", "scales to millions"** | None is measured; each is a credibility liability |
| **Buildathon / track / phase language** | This is a product page, not a submission page |
| **Stock illustration, AI-generated imagery, "AI sparkle" decoration** | Contradicts the ledger direction |

Every number on this page must trace to a real product property.

---

## Section structure

### 1. Navigation
Sticky, thin, hairline bottom border. Wordmark left; three or four minimal links
(Product · How it works · Verification); right side an **outlined "Log in"** and a
**solid dark primary CTA**. Collapses to wordmark + Log in on mobile. No mega-menu.

### 2. Hero
Large editorial headline, tight leading, generous whitespace. One-sentence subhead
explaining the product plainly. Primary CTA plus a quiet text link to the workflow
section.

Direction: lead with **what it does and that it can be proved** — deterministic and
verifiable first, AI second. Never headline the AI.

A restrained visual motif is permitted — a reconciliation flow (three sources → one
result → verified). It must be typographic and structural, not an illustration.

### 3. Proof strip
A thin band directly under the hero listing real capabilities, each one word or short
phrase plus a single clarifying line:

- Multi-source reconciliation
- Measured match rate
- Complete exception list
- Evidence-backed investigation
- Independent verification

Separated by hairline rules. No icons-in-circles, no cards.

### 4. Problem
Two-column editorial. Finance teams line up payment, bank, and settlement records by
hand, chase each mismatch individually, and close on judgement rather than proof.
State it plainly; do not dramatise.

### 5. Solution
One workflow, with the evidence attached. Every unit classified with a specific reason
code; every unresolved case queued with its evidence; the whole result checkable against
an independent source.

### 6. Workflow
The seven stages as a horizontal stepper on wide screens, stacked on narrow:

```
UPLOAD → VALIDATE → RECONCILE → UNDERSTAND → INVESTIGATE → AI EXPLAIN → INDEPENDENTLY VERIFY
```

Numbered, hairline-separated, one sentence each. This is the product's spine and should
be the most memorable section on the page.

### 7. Evidence
The differentiating section. Show — through structure, not screenshots-as-decoration —
that every classification carries the underlying Payment, Bank, and Settlement rows, and
that the differing field is identified explicitly.

`[RECOMMENDATION]` A simplified, honest three-column comparison rendered in real markup
is stronger here than a screenshot: it is legible, responsive, accessible, and cannot go
stale.

### 8. AI
Position AI accurately and modestly:

> AI explains verified results. It never decides them.

Cover: explanations are generated from persisted evidence · the assistant is scoped to
one run and reports which tools it used · reconciliation never calls an AI provider, so
its correctness and speed cannot be affected by one.

**Do not oversell.** No "revolutionary", "next-generation", "magic". The restraint is the
selling point.

### 9. Independent verification
The closing argument. Ground truth is generated before any reconciliation runs, from the
same plan that produced the source data — so a comparison against it is a real
measurement, not self-grading.

Show both outcomes honestly: a pass state, and the fact that a failure lists **every**
discrepancy rather than the first.

### 10. Final CTA
Dark band. Restate the promise — *Find what doesn't reconcile. Understand why. Prove the
result.* — and one primary action into the product. **Do not end on a feature list.**

### 11. Footer
Minimal: wordmark, one-line descriptor, repository link. No fake link farm, no
newsletter, no social icons.

---

## Craft requirements

| Aspect | Requirement |
|---|---|
| Rhythm | Consistent vertical spacing from the scale; sections separated by hairline rules, not boxes |
| Scroll | Each section resolves cleanly; no parallax, no scroll-jacking, no reveal-on-scroll chains |
| Motion | At most a subtle fade/rise on section entry, disabled under `prefers-reduced-motion` |
| Reading width | Prose capped around 60–75 characters |
| Responsive | Multi-column → single column; the stepper stacks; nothing scrolls horizontally |
| Performance | `@defer` below-the-fold sections; no external assets beyond the self-hosted font |
| Accessibility | One `<h1>`; correct heading order; visible focus; every link has a descriptive name |
| Density | Public surface is **airy**; the application is where density lives |

---

## Relationship to the reference

`[RECOMMENDATION]` Modern Treasury is a reference for **principles only**: editorial
hero proportions, restrained navigation, generous whitespace, hairline dividers, strong
CTA hierarchy, long-form storytelling, professional fintech tone.

**Nothing is copied** — not branding, logos, colours, typography, layout, copy,
illustrations, assets, navigation labels, or section structure. FinSight's palette, type
scale, section order, and voice are its own. If a section here resembles the reference
beyond shared genre conventions, change it.
