namespace FinSight.Application.Abstractions.Services;

/// <summary>
/// The outcome of a rate-limit check: either the attempt was consumed and
/// may proceed, or it was rejected and the caller should wait
/// <see cref="RetryAfter"/> before trying again.
/// </summary>
public readonly record struct RateLimitDecision(bool IsAllowed, TimeSpan? RetryAfter)
{
    public static RateLimitDecision Allow() => new(true, null);

    public static RateLimitDecision Deny(TimeSpan retryAfter) => new(false, retryAfter);
}

/// <summary>
/// Abuse protection for password-reset requests, independent of whether the
/// requested email belongs to a real account.
///
/// This is deliberately checked BEFORE any user lookup: the anti-
/// enumeration guarantee the rest of the forgot-password flow relies on
/// (identical response for a known and an unknown address) only holds if
/// the limiter itself never branches on account existence. Callers must
/// invoke this for every request, pass an already-normalized email (the
/// same normalization the rest of the application uses), and treat a
/// denied decision as final -- no lookup, no token issuance, no email send.
/// </summary>
public interface IPasswordResetRateLimiter
{
    RateLimitDecision CheckAndConsume(string normalizedEmail, string clientIp);
}
