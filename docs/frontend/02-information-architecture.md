# Information Architecture

How the product is organised, what form each experience takes, and why.

---

## UX hierarchy

The product's information hierarchy follows the trust chain, in this order:

```
RESULT → EVIDENCE → EXCEPTION → INVESTIGATION → AI ASSISTANCE → INDEPENDENT VERIFICATION
```

Every screen should make its position in that chain obvious. Anything that inverts it —
AI output above evidence, commentary above data — is a defect, not a style choice.

---

## Surface forms

| Surface | Form | Rationale |
|---|---|---|
| Landing | **Route** `/` | Public, long-form, editorial |
| Login | **Route** `/login` | Focused, minimal |
| Batch history | **Route** `/batches` | Operations launcher — a list, not a KPI wall |
| Batch upload | **Route** `/batches/upload` | Multi-input, multi-stage; deserves full attention |
| Batch detail | **Route** `/batches/:batchId` | Entry point to "Run reconciliation" |
| **Run workspace** | **Shared shell** `/runs/:runId` | See below |
| Run overview | **Tab** within the run shell | The headline |
| Results | **Tab** | Dense table |
| Transaction evidence | **Drawer** over Results | Keeps the table visible for context — an investigation stays in flow |
| Exceptions | **Tab** | Queue |
| Exception detail | **Route** `/runs/:runId/exceptions/:id` | Deep; needs prev/next queue navigation and a shareable URL |
| AI explanation | **Contextual panel, inline, below evidence** | Never a modal. Subordination must be structural, not stylistic |
| Finance Assistant | **Tab** | Scoped to the run |
| Independent verification | **Tab** | Scoped to the run |
| Destructive confirmation | **Modal** | Rare; nothing destructive exists today |

### Why a drawer for evidence but a route for exception detail

Evidence is consulted *while scanning results* — a drawer preserves the list and the
reader's place. An exception is *worked*, one at a time, with queue navigation and a URL
worth sharing. Different jobs, different containers.

---

## RunShell — evaluated and approved

**Decision: YES, build a shared `RunShell`.**

A reconciliation run is a **workspace**, not five unrelated pages. Overview, Results,
Exceptions, Assistant, and Verification all operate on the same run and all need the same
context header.

| Benefit | Detail |
|---|---|
| One summary fetch | `RunContextStore` provided at the shell route; tabs consume it instead of each re-fetching |
| Persistent orientation | Match rate and status counts stay visible while investigating |
| Coherent navigation | Tabs, not full page transitions, between facets of one object |
| Correct URL semantics | Every tab is deep-linkable and shareable |

**Structure**

```
/runs/:runId                    RunShell
  ├─ (sticky header)            batch label · run status · match rate · five status counts
  ├─ (tab bar)                  Overview · Results · Exceptions · Assistant · Verify
  └─ <router-outlet>
```

See [ADR-010](../adr/README.md#adr-010-runshell-as-shared-run-context).

---

## Navigation — intentionally small

Primary navigation is **five items**. Nothing else.

```
Batches          → /batches
Reconciliation   → current run overview
Exceptions       → current run exceptions
Finance Assistant→ current run assistant
Verification     → current run verify
```

The last four are run-scoped and are disabled or hidden when no run is selected. **Do not
add a sixth item** without removing one. Enterprise menu sprawl is the failure mode this
constraint exists to prevent.

**No audit item.** `[CODE]` No audit-log read endpoint exists. Its absence is stated
honestly in the documentation rather than faked with a dead nav entry.

---

## UX priority — validated, not assumed

The proposed priority was checked against `[CODE]` capability and `[OFFICIAL WEB]`
judging emphases. Two adjustments were made, both justified.

| Rank | Experience | Judged value | Effort | Note |
|---|---|---|---|---|
| **1** | **Exception investigation + evidence comparison** | Highest | High | **Merged.** Evidence *is* the investigation; separating them is artificial and would produce two half-screens |
| **2** | **Run overview** | Highest | Medium | The match-rate moment; answers "can I trust this?" in two seconds |
| **3** | **Independent verification** | Highest | Medium | The genuine differentiator; `[OFFICIAL WEB]` "verification capacity" |
| **4** | **Batch upload + validation errors** | High | Medium | First real interaction; the only place structured `errors[]` becomes visible |
| **5** | **AI explanation + 503 state** | High | Low-Med | **Promoted** from 6th — `[OFFICIAL WEB]` judges graceful runtime fallbacks explicitly |
| **6** | Finance Assistant + tool trail | High | Medium | `[OFFICIAL WEB]` judges appropriate agent use |
| **7** | Landing | Medium-High | Medium | First ten seconds for an evaluator |
| **8** | Batch history | Medium | Low | A launcher, not a dashboard |
| **9** | Login | Medium | Low | Must be calm and unremarkable |

### Divergence from the proposed list — recorded

- The proposal listed "exception investigation" (2) and "evidence comparison" (3) as
  separate priorities. **Merged into one**, because the evidence comparison is the
  substance of the investigation screen. Building them separately would produce a queue
  with nothing behind it, then a viewer with no queue.
- The proposal placed AI explanation at 6. **Promoted to 5**, on `[OFFICIAL WEB]`
  evidence that graceful fallback handling is directly judged.

Implementation order differs from priority order for dependency reasons — see
[delivery/01-roadmap.md](../delivery/01-roadmap.md).
