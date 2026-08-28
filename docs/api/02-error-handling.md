# Error Handling and ProblemDetails

`[CODE]` Verified against `GlobalExceptionHandler` and every controller.

---

## Current behaviour — standardised

> **RESOLVED** `[ZIP]` doc 12 records two coexisting error shapes: hand-rolled
> `{ message }` objects from controllers, and `ProblemDetails` from the global handler.
>
> **Status: FIXED** `[CODE]`. **Every** error response is now RFC 7807 `ProblemDetails`,
> served as `application/problem+json`. The frontend has exactly one error shape to parse.

`Program.cs` registers `AddProblemDetails()` and
`AddExceptionHandler<GlobalExceptionHandler>()`; controllers produce `ProblemDetails`
directly for their own caught cases.

## Standard shape

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "pageSize must be between 1 and 100.",
  "traceId": "00-…"
}
```

## Status code mapping `[CODE]`

| Code | When | Source |
|---|---|---|
| **400** | Invalid body, missing field, invalid pagination, empty ground-truth array, **batch validation failure** | Controller |
| **401** | Missing/invalid JWT; bad login credentials | Middleware / `AuthController` |
| **403** | `UnauthorizedAccessException` | `GlobalExceptionHandler` |
| **404** | Batch / run / result / exception not found | Controller |
| **500** | Unhandled exception — **never leaks exception text** | `GlobalExceptionHandler` |
| **503** | Both AI providers unavailable (`AiProviderUnavailableException`) | `GlobalExceptionHandler` |

Not implemented, deliberately: **409** (no concurrent-run guard exists), **422** (400 is
sufficient at this scope), **429** (no rate limiting — DO NOT BUILD).

### Information disclosure

`[CODE]` `GlobalExceptionHandler` returns a generic title for 500 and does **not**
include exception messages, stack traces, or provider details. Asserted by test: an
exception carrying sensitive AI-provider text produces a 500 body containing neither the
message nor the exception type name.

---

## Structured batch validation errors

`[CODE]` **Undocumented in `[ZIP]` — this capability is new.**

When `POST /api/batches` fails CSV validation, the 400 `ProblemDetails` carries an
**additive `errors` extension** alongside the unchanged `detail`:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Batch validation failed:\nPayment row 2: payment_record_id - Required value is missing.",
  "errors": [
    {
      "source": "Payment",
      "rowNumber": 2,
      "field": "payment_record_id",
      "message": "Required value is missing."
    }
  ]
}
```

| Field | Type | Notes |
|---|---|---|
| `source` | string | `"Payment"` · `"Bank"` · `"Settlement"` |
| `rowNumber` | int, **nullable** | 1-based including the header, so the first data row is `2` |
| `field` | string | CSV column name, e.g. `payment_record_id` |
| `message` | string | Fixed, generic message — **never echoes the offending value** |

### Frontend rules — mandatory

1. **Render `errors[]`. Never parse `detail`.** The `detail` string exists only for
   non-UI consumers and log readability.
2. **Group by `source`**, then list `row · field · message` within each group.
3. `errors` is **optional** — every other 400 in the API omits it. Handle its absence.
4. Ordering is Payment → Bank → Settlement, matching validator execution order.

### Privacy note

`[CODE]` Every `message` is a fixed generic string and `field`/`source` are fixed column
names. **No raw CSV cell values are ever echoed back** — no amounts, references, or
account identifiers. Displaying the full `errors[]` array is safe.

### Implementation note — worth knowing

`[CODE]` The structured errors travel on `InvalidDataException.Data["Errors"]`, not on a
custom exception type. `System.IO.InvalidDataException` is **sealed** in .NET 10, so a
`BatchValidationException` subclass is impossible. The service-layer contract
("validation failure throws `InvalidDataException`") is therefore preserved exactly, and
the controller enriches the response when the structured payload is present. See
[delivery/02-demo-runbook.md](../delivery/02-demo-runbook.md) — this is one of the
documented "what broke and how we recovered" items.

---

## Frontend consumption

One `ProblemDetails` model, one parse path:

```ts
interface ProblemDetails {
  type?: string; title?: string; status?: number;
  detail?: string; instance?: string; traceId?: string;
  errors?: IngestionValidationError[];   // batch validation only
}
```

| Status | UI treatment |
|---|---|
| 400 with `errors[]` | Grouped structured error block on the upload screen |
| 400 without `errors[]` | Inline message near the offending control |
| 401 | Interceptor clears session and redirects to login — never a toast |
| 403 | Full-surface "not permitted" state (not reachable today — single role) |
| 404 | Empty/not-found state for that surface, with a way back |
| 500 | Error state with retry; never surface `traceId` as the primary message |
| **503** | **Designed AI-unavailable state** — "AI explanation unavailable. Reconciliation result is unaffected." Evidence stays fully intact. See [design/05-ai-ux.md](../design/05-ai-ux.md) |

Never render `detail` as raw HTML. AI-adjacent text is server-supplied — treat all of it
as text.
