namespace FinSight.Infrastructure.Authentication;

/// <summary>
/// Forgot-password abuse-protection configuration, bound from the
/// "Auth:PasswordResetRateLimit" section. All four values have
/// conservative defaults so a fresh clone is protected without extra
/// configuration; override them in a real deployment if its traffic
/// pattern genuinely needs different limits.
///
/// Two layers, both applied to every request regardless of whether the
/// email belongs to a real account:
///
/// - Per normalized email -- the primary defense, since it directly
///   limits how many reset links a single target address can trigger.
/// - Per client IP -- a secondary cap on a single source hammering many
///   different target addresses, which the per-email limit alone would
///   not catch.
///
/// Defaults (5 requests / 15 minutes per email, 20 / 15 minutes per IP)
/// were chosen relative to this project's existing 60-minute reset-link
/// Lifetime (<see cref="PasswordResetOptions.Lifetime"/>): the rate-limit
/// window is deliberately shorter than the link's own lifetime so the two
/// concerns stay distinct. Five attempts is enough for a legitimate user
/// to recover from a typo or a missed inbox within the window while
/// making sustained hammering materially harder; twenty per IP tolerates
/// a handful of genuine users behind shared NAT (a small office, a campus
/// network) without giving a single source a meaningfully larger budget
/// than a handful of real users would need.
/// </summary>
public sealed class PasswordResetRateLimitOptions
{
    public int MaxAttemptsPerEmail { get; init; } = 5;

    public TimeSpan EmailWindow { get; init; } = TimeSpan.FromMinutes(15);

    public int MaxAttemptsPerIp { get; init; } = 20;

    public TimeSpan IpWindow { get; init; } = TimeSpan.FromMinutes(15);
}
