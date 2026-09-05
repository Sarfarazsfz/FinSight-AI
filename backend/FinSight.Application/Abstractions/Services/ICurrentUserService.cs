namespace FinSight.Application.Abstractions.Services;

/// <summary>
/// The authenticated caller's identity for the current request.
///
/// Deliberately the ONLY source of "who is asking" that authorization code
/// may consult. Never accept a user id, email, or ownership claim from a
/// request body or query string as authoritative -- those are attacker
/// controlled. This service reads the identity the JWT bearer middleware
/// already validated, nothing else.
///
/// Both members are nullable: every consumer sits behind [Authorize], so
/// they should always be populated, but a service should not crash on a
/// malformed or unexpected token shape -- it should treat a missing
/// identity as "not authenticated" and let the caller decide how to
/// respond (typically 401).
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// The stable, immutable identifier from the JWT's NameIdentifier/sub
    /// claim -- FinSight.Domain.Entities.User.Id. This, not email, is the
    /// key every ownership check is keyed on.
    /// </summary>
    Guid? UserId { get; }

    /// <summary>
    /// The email claim, exposed only for display/audit purposes (e.g. an
    /// ingestion record's human-readable "created by" label). Never used
    /// for ownership decisions -- UserId is.
    /// </summary>
    string? Email { get; }
}
