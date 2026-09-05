namespace FinSight.Domain.Entities;

/// <summary>
/// A single-use, time-limited password-reset grant.
///
/// Only the SHA-256 hash of the token is stored. The raw token exists
/// exactly once -- in the reset link sent to the address on file -- and is
/// never persisted, logged, or returned by any API response. A database
/// disclosure therefore does not hand an attacker usable reset links, for
/// the same reason password hashes are stored rather than passwords.
///
/// Single-use is enforced by <see cref="UsedAtUtc"/> rather than by
/// deleting the row, so a replayed link is positively identified as
/// already-consumed instead of being indistinguishable from a token that
/// never existed.
/// </summary>
public class PasswordResetToken
{
    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? UsedAtUtc { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private PasswordResetToken()
    {
    }

    public PasswordResetToken(
        Guid userId,
        string tokenHash,
        DateTime expiresAtUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User ID is required.",
                nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException(
                "Token hash is required.",
                nameof(tokenHash));
        }

        Id = Guid.NewGuid();
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
        UsedAtUtc = null;
        CreatedAt = DateTime.UtcNow;
    }

    public bool IsExpired(DateTime utcNow) =>
        utcNow >= ExpiresAtUtc;

    public bool IsUsed =>
        UsedAtUtc is not null;

    /// <summary>
    /// Usable exactly once, and only before expiry.
    /// </summary>
    public bool IsRedeemable(DateTime utcNow) =>
        !IsUsed && !IsExpired(utcNow);

    public void MarkUsed(DateTime utcNow)
    {
        if (IsUsed)
        {
            throw new InvalidOperationException(
                "This password reset token has already been used.");
        }

        UsedAtUtc = utcNow;
    }
}
