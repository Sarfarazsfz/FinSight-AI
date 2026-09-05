using FinSight.Domain.Entities;

namespace FinSight.Application.Abstractions.Persistence;

public interface IPasswordResetTokenRepository
{
    /// <summary>
    /// Looked up by hash, never by raw token -- the raw value is not
    /// stored, so this is the only possible lookup.
    /// </summary>
    Task<PasswordResetToken?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        PasswordResetToken token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates every outstanding token for a user. Called when a new
    /// reset is requested (so only the newest link works) and again after
    /// a successful reset (so no sibling link survives the change).
    /// </summary>
    Task InvalidateActiveTokensForUserAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}
