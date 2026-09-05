using System.Collections.Concurrent;
using FinSight.Application.Abstractions.Services;

namespace FinSight.Infrastructure.Authentication;

/// <summary>
/// Fixed-window counter, held in an in-process dictionary keyed by
/// normalized email and by client IP. Registered as a singleton -- a
/// scoped or transient lifetime would hand every request a fresh, empty
/// dictionary and defeat the entire purpose.
///
/// Deployment limitation, stated plainly: this is per-process state. It
/// protects a single running API instance; it does not coordinate across
/// multiple instances behind a load balancer. FinSight currently runs as
/// a single instance, so that is not a gap for this deployment, but it is
/// not "distributed" or "global" protection and must never be described
/// that way. A genuinely multi-instance deployment would need a shared
/// store (e.g. Redis) instead -- deliberately out of scope for this
/// phase.
///
/// <see cref="TimeProvider"/> is injected (defaulting to
/// <see cref="TimeProvider.System"/>) so tests can advance time
/// deterministically instead of sleeping.
/// </summary>
public sealed class InMemoryPasswordResetRateLimiter : IPasswordResetRateLimiter
{
    private sealed class Bucket
    {
        public int Count;
        public DateTimeOffset WindowStartUtc;
    }

    // Buckets are removed once their window is this many multiples old,
    // so a sustained flood of distinct throwaway emails/IPs does not grow
    // this dictionary forever. This is a coarse, synchronous safeguard --
    // not a TTL cache -- deliberately, to avoid pulling in any caching
    // dependency for what is a small, single-purpose counter.
    private const int StaleWindowMultiplier = 4;
    private const long SweepIntervalRequests = 500;

    private readonly ConcurrentDictionary<string, Bucket> _buckets = new();
    private readonly PasswordResetRateLimitOptions _options;
    private readonly TimeProvider _timeProvider;
    private long _requestsSinceSweep;

    public InMemoryPasswordResetRateLimiter(
        PasswordResetRateLimitOptions options,
        TimeProvider? timeProvider = null)
    {
        _options = options;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public RateLimitDecision CheckAndConsume(string normalizedEmail, string clientIp)
    {
        SweepIfDue();

        // Email is checked first: it is the primary defense, and checking
        // it before the IP bucket means a request that is going to be
        // denied anyway is not also spuriously counted against some other
        // caller's IP budget when many callers share one address (e.g. a
        // NAT'd office all requesting resets for the same corporate
        // alias would only exhaust their own IP budgets, never bleed into
        // each other's email-level counts either way -- the two buckets
        // are always independent regardless of check order).
        var emailDecision =
            Consume(
                "email:" + normalizedEmail,
                _options.MaxAttemptsPerEmail,
                _options.EmailWindow);

        if (!emailDecision.IsAllowed)
        {
            return emailDecision;
        }

        return Consume(
            "ip:" + clientIp,
            _options.MaxAttemptsPerIp,
            _options.IpWindow);
    }

    private RateLimitDecision Consume(string key, int limit, TimeSpan window)
    {
        var now = _timeProvider.GetUtcNow();
        var bucket = _buckets.GetOrAdd(key, _ => new Bucket { WindowStartUtc = now });

        lock (bucket)
        {
            if (now - bucket.WindowStartUtc >= window)
            {
                bucket.WindowStartUtc = now;
                bucket.Count = 0;
            }

            if (bucket.Count >= limit)
            {
                var retryAfter = bucket.WindowStartUtc + window - now;

                return RateLimitDecision.Deny(
                    retryAfter > TimeSpan.Zero ? retryAfter : TimeSpan.Zero);
            }

            bucket.Count++;

            return RateLimitDecision.Allow();
        }
    }

    private void SweepIfDue()
    {
        if (Interlocked.Increment(ref _requestsSinceSweep) % SweepIntervalRequests != 0)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();

        foreach (var (key, bucket) in _buckets)
        {
            var window =
                key.StartsWith("email:", StringComparison.Ordinal)
                    ? _options.EmailWindow
                    : _options.IpWindow;

            lock (bucket)
            {
                if (now - bucket.WindowStartUtc >= window * StaleWindowMultiplier)
                {
                    _buckets.TryRemove(key, out _);
                }
            }
        }
    }
}
