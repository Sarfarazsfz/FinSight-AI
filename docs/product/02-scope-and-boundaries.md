# Product Scope and Boundaries

Scope discipline is the reason this product has depth. Every excluded item below would
trade depth in the reconciliation loop for breadth across unrelated ones.

---

## In scope — MUST remain

| Capability | Status `[CODE]` |
|---|---|
| Payment / Bank / Settlement CSV ingestion | ✅ implemented |
| Row-level validation with structured, per-field errors | ✅ implemented |
| Normalisation by transaction reference | ✅ implemented |
| Deterministic reconciliation (exact + 24h date-tolerance strategies) | ✅ implemented |
| Reason-coded classification (5 statuses, 11 reason codes) | ✅ implemented |
| Measured match rate | ✅ implemented |
| Complete exception list (union of all three sources) | ✅ implemented |
| Transaction / exception investigation via API | ✅ implemented |
| Three-source evidence retrieval | ✅ implemented |
| AI explanation of verified exceptions | ✅ implemented |
| AI tool-based Q&A scoped to a run | ✅ implemented |
| Audit trail (write path) | ✅ implemented — **no read endpoint** |
| Ground-truth verification over HTTP | ✅ implemented |
| Operational Angular UI | 🚧 early foundation — being rebuilt |

---

## Classification of excluded work

`[RECOMMENDATION]` throughout, grounded in `[ZIP]` doc 02 and the one-loop principle in
[ADR-006](../adr/README.md#adr-006-one-finance-ops-loop-instead-of-multiple-shallow-features).

### DO NOT BUILD

| Item | Reason |
|---|---|
| **Forward cash forecasting** | A different finance-ops loop (prediction, not verification). Listed officially as an *alternative* direction, not an addition. |
| **Tax-line matching** | Same reasoning — a distinct loop. |
| **General-purpose chatbot** | Anything not scoped to a specific run or exception reduces grounding and reads as decorative, contradicting the verification emphasis. The assistant stays run-scoped. |
| **Unrelated CRUD screens** | Surface area with no product value. |
| **Multi-tenancy** | No tenant concept exists in the data model. Would require schema, auth, and UI changes for zero demonstrable benefit. |
| **Complex RBAC** | One role exists (`"User"`). A role editor is scope creep with no upside. |
| **Custom workflow builder** | Enormous surface, no connection to the loop. |
| **Report builder** | Same. |
| **Large admin portal** | Same. |
| **Microservices / event infrastructure / distributed architecture** | Runs are synchronous, single-batch, demo-scale. A queue adds failure surface without adding correctness. |
| **Unnecessary backend redesign** | The backend is frozen and verified. |
| **Social features** (comments, sharing, feeds) | No connection to the loop. |
| **Large chart libraries** | A five-status bar and honest numbers outperform a chart wall for this product. |
| **Excessive animation** | Contradicts the ledger direction. |
| **React in production** | Claude Design artifacts are sketches only. See ADR-008. |

### DEFER — real work, but not now; needs separate approval

| Item | Why deferred |
|---|---|
| **Audit-log read endpoint + timeline UI** | `[CODE]` The write path exists and is rich; **no read endpoint exists**. Requires backend work, which is frozen. Genuinely valuable — schedule deliberately, never smuggle into a frontend phase. |
| **Throughput instrumentation** | `[CODE]` No `Stopwatch` anywhere. `[OFFICIAL WEB]` "Throughput" is named in the evaluation bar, so this is not cosmetic. Requires a backend change. |
| **Exception resolution write path** | No such endpoint exists; would change what the data model means. |
| **Refresh tokens** | `[CODE]` The backend issues an access token only, with no refresh endpoint. Building silent-refresh against a scheme that does not support it is fiction. |
| **Websockets / real-time push** | All operations are synchronous request/response. Re-fetching on user action is correct today. |

### ONLY IF TIME REMAINS

| Item | Condition |
|---|---|
| **Dark mode** | Only after the light system is complete and polished. A toggle is not a feature; a second complete, tested palette is. |
| **Advanced analytics** | Only as read-only summaries derived from already-reconciled data — never a forecast. |
| **i18n** | No second locale is required by anything. |
| **Mobile app** | The responsive web app covers every stated need. |
| **Editorial serif heading pairing** | `[ZIP]` doc 18 proposed it. Two type families is a coherence risk; revisit only once the single-family system is proven. |

---

## Why this discipline matters

`[OFFICIAL WEB]` The official brief asks for an agent that closes **one** finance-ops
loop. `[RECOMMENDATION]` A submission showing four shallow loops is weaker than one
provable loop, because the stated bar — *throughput plus measured accuracy plus an
honest exception list* — is composed entirely of depth measures, not breadth measures.
