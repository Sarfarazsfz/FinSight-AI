namespace FinSight.Tests.Authentication;

/// <summary>
/// A <see cref="TimeProvider"/> the test controls explicitly, so
/// window-expiry behaviour can be asserted deterministically without
/// Thread.Sleep or a flaky wall-clock-dependent test.
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public ManualTimeProvider(DateTimeOffset? start = null)
    {
        _now = start ?? DateTimeOffset.UtcNow;
    }

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
