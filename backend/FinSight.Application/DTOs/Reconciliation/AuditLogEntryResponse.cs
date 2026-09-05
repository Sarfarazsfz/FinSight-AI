namespace FinSight.Application.DTOs.Reconciliation;

/// <summary>
/// One recorded audit event, exactly as stored -- nothing computed,
/// nothing inferred. Fields the underlying AuditLog entity does not carry
/// (there is no actor/user identity on it) are simply absent here rather
/// than backfilled with a guess.
///
/// This is evidence ABOUT a reconciliation run's execution, never a
/// second source of financial truth: match status, match rate, exception
/// counts and classification remain whatever the deterministic
/// reconciliation engine and Ground Truth Verification say they are.
/// </summary>
public sealed class AuditLogEntryResponse
{
    public Guid Id { get; init; }

    public DateTime OccurredAt { get; init; }

    public string EventType { get; init; } = string.Empty;

    public Guid? RunId { get; init; }

    public string? RelatedEntityType { get; init; }

    public Guid? RelatedEntityId { get; init; }

    /// <summary>
    /// The raw JSON payload exactly as persisted (jsonb column) --
    /// passed through unparsed, the same convention this API already
    /// uses for ReconciliationExceptionResponse.DiscrepancyDetail. The
    /// caller is responsible for parsing it defensively; this endpoint
    /// never attempts to interpret it.
    /// </summary>
    public string Detail { get; init; } = string.Empty;
}
