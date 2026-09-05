using System.Collections.Concurrent;
using FinSight.Application.TestData;

namespace FinSight.Infrastructure.TestData;

/// <summary>
/// Short-lived, in-memory registry of recently generated synthetic datasets.
///
/// Storage model:
///   POST /api/test-data/generate → stores { userId, request, seed } here for up to one hour.
///   GET  /api/test-data/download/{id}/{file} → looks up the stored session,
///        verifies the requesting user is the same one that generated it,
///        then regenerates the CSV deterministically using the stored seed.
///
/// The generator is pure and deterministic by seed, so we only need to
/// remember the request shape — not the full generated CSV content.
/// Bounded: expired entries are evicted on every store call; the dictionary
/// never grows beyond one-hour-window × concurrent users.
/// </summary>
public sealed class TestDataSessionStore
{
    private sealed record StoredSession(
        Guid UserId,
        DataGenerationRequest Request,
        long Seed,
        DateTimeOffset CreatedAt);

    private readonly ConcurrentDictionary<string, StoredSession> _sessions = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(1);

    /// <summary>
    /// Stores a generation session so it can be referenced by download endpoints.
    /// </summary>
    public void Store(
        string generationId,
        Guid userId,
        DataGenerationRequest request,
        long seed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(generationId);

        _sessions.TryAdd(
            generationId,
            new StoredSession(userId, request, seed, DateTimeOffset.UtcNow));

        // Evict expired entries to keep memory bounded.
        var cutoff = DateTimeOffset.UtcNow - Ttl;
        foreach (var key in _sessions.Keys)
        {
            if (_sessions.TryGetValue(key, out var s) &&
                s.CreatedAt < cutoff)
            {
                _sessions.TryRemove(key, out _);
            }
        }
    }

    /// <summary>
    /// Returns the stored request and seed if found, or null if expired/missing.
    /// </summary>
    public (DataGenerationRequest Request, long Seed)? TryGet(
        string generationId,
        Guid requestingUserId)
    {
        if (!_sessions.TryGetValue(generationId, out var session))
        {
            return null;
        }

        if (DateTimeOffset.UtcNow - session.CreatedAt > Ttl)
        {
            _sessions.TryRemove(generationId, out _);
            return null;
        }

        // Enforce per-user isolation — do not let one user download
        // another user's generated dataset.
        if (session.UserId != requestingUserId)
        {
            return null;
        }

        return (session.Request, session.Seed);
    }
}
