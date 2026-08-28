/**
 * RFC 7807 ProblemDetails, as actually produced by the FinSight backend.
 *
 * Verified against FinSight.Api/ErrorHandling/GlobalExceptionHandler.cs and
 * every controller's `Problem(...)` call. The backend serializes with the
 * ASP.NET Core default camelCase policy -- no JsonSerializerOptions override
 * exists in Program.cs -- so these field names are the wire names.
 */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;

  /**
   * Structured batch-validation errors.
   *
   * Present ONLY on a 400 from `POST /api/batches` when CSV validation
   * fails. Every other error response in the API omits it, so treat it as
   * optional everywhere.
   */
  errors?: IngestionValidationError[];
}

/**
 * Mirrors FinSight.Application.DTOs.Ingestion.IngestionValidationError.
 *
 * `message` is always a fixed, generic string and `field`/`source` are fixed
 * CSV column and source names -- the backend never echoes a raw cell value
 * back, so rendering the whole array is safe.
 */
export interface IngestionValidationError {
  /** "Payment" | "Bank" | "Settlement" -- kept as string, matching the DTO. */
  source: string;

  /** 1-based including the header row, so the first data row is 2. Nullable. */
  rowNumber: number | null;

  /** CSV column name, e.g. "payment_record_id". */
  field: string;

  message: string;
}

/**
 * True when a caught `HttpErrorResponse#error` looks like a ProblemDetails
 * body rather than a network failure or an opaque string.
 */
export function isProblemDetails(value: unknown): value is ProblemDetails {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    return false;
  }

  return 'title' in value || 'detail' in value || 'status' in value;
}

/**
 * Returns the structured validation errors carried by a ProblemDetails, or
 * an empty array when there are none.
 *
 * The human-readable `detail` string is NEVER parsed to reconstruct these.
 * `detail` exists for logs and non-UI consumers; `errors[]` is the contract
 * the UI renders.
 */
export function extractValidationErrors(
  value: unknown,
): IngestionValidationError[] {
  if (!isProblemDetails(value)) {
    return [];
  }

  return value.errors ?? [];
}
