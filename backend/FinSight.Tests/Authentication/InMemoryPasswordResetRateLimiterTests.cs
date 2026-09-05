using FinSight.Infrastructure.Authentication;

namespace FinSight.Tests.Authentication;

/// <summary>
/// The limiter itself, isolated from HTTP and from the account service.
/// Uses <see cref="ManualTimeProvider"/> throughout instead of Thread.Sleep
/// so window-expiry behaviour is deterministic and the suite stays fast.
///
/// Every test uses a small, test-only <see cref="PasswordResetRateLimitOptions"/>
/// -- never the production defaults -- so limits can be exhausted in a
/// handful of calls.
/// </summary>
[TestFixture]
public sealed class InMemoryPasswordResetRateLimiterTests
{
    private static InMemoryPasswordResetRateLimiter CreateLimiter(
        ManualTimeProvider timeProvider,
        int maxAttemptsPerEmail = 3,
        int maxAttemptsPerIp = 100) =>
        new(
            new PasswordResetRateLimitOptions
            {
                MaxAttemptsPerEmail = maxAttemptsPerEmail,
                EmailWindow = TimeSpan.FromMinutes(15),
                MaxAttemptsPerIp = maxAttemptsPerIp,
                IpWindow = TimeSpan.FromMinutes(15),
            },
            timeProvider);

    [Test]
    public void CheckAndConsume_UpToTheLimit_AllowsEveryRequest()
    {
        var limiter = CreateLimiter(new ManualTimeProvider(), maxAttemptsPerEmail: 3);

        for (var i = 0; i < 3; i++)
        {
            var decision = limiter.CheckAndConsume("person@example.com", "203.0.113.1");

            Assert.That(decision.IsAllowed, Is.True, $"attempt {i + 1} of 3 should be allowed");
        }
    }

    [Test]
    public void CheckAndConsume_TheAttemptImmediatelyBeyondTheLimit_IsDenied()
    {
        var limiter = CreateLimiter(new ManualTimeProvider(), maxAttemptsPerEmail: 3);

        for (var i = 0; i < 3; i++)
        {
            limiter.CheckAndConsume("person@example.com", "203.0.113.1");
        }

        var fourth = limiter.CheckAndConsume("person@example.com", "203.0.113.1");

        Assert.Multiple(() =>
        {
            Assert.That(fourth.IsAllowed, Is.False);
            Assert.That(fourth.RetryAfter, Is.Not.Null);
            Assert.That(fourth.RetryAfter!.Value, Is.GreaterThan(TimeSpan.Zero));
        });
    }

    [Test]
    public void CheckAndConsume_AfterTheWindowElapses_AllowsRequestsAgain()
    {
        var time = new ManualTimeProvider();
        var limiter = CreateLimiter(time, maxAttemptsPerEmail: 2);

        limiter.CheckAndConsume("person@example.com", "203.0.113.1");
        limiter.CheckAndConsume("person@example.com", "203.0.113.1");

        var blocked = limiter.CheckAndConsume("person@example.com", "203.0.113.1");
        Assert.That(blocked.IsAllowed, Is.False);

        // Deterministic: advance the fake clock past the window rather
        // than sleeping the real one.
        time.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));

        var afterWindow = limiter.CheckAndConsume("person@example.com", "203.0.113.1");

        Assert.That(afterWindow.IsAllowed, Is.True);
    }

    [Test]
    public void CheckAndConsume_DifferentEmails_HaveIndependentBuckets()
    {
        var limiter = CreateLimiter(new ManualTimeProvider(), maxAttemptsPerEmail: 1);

        var first = limiter.CheckAndConsume("a@example.com", "203.0.113.1");
        var second = limiter.CheckAndConsume("b@example.com", "203.0.113.1");

        Assert.Multiple(() =>
        {
            Assert.That(first.IsAllowed, Is.True);
            Assert.That(second.IsAllowed, Is.True, "a different email must not share a's budget");
        });
    }

    [Test]
    public void CheckAndConsume_TheSameNormalizedEmail_SharesOneBucketRegardlessOfCaller()
    {
        // The limiter itself is not responsible for normalizing -- that
        // is the caller's job (AuthController, using the same
        // CredentialPolicy.NormalizeEmail the rest of the app uses). This
        // proves the limiter's own keying is exact-string-based, so
        // supplying the same normalized form from anywhere consumes the
        // same budget.
        var limiter = CreateLimiter(new ManualTimeProvider(), maxAttemptsPerEmail: 2);

        limiter.CheckAndConsume("person@example.com", "203.0.113.1");
        limiter.CheckAndConsume("person@example.com", "203.0.113.2");

        var third = limiter.CheckAndConsume("person@example.com", "203.0.113.3");

        Assert.That(
            third.IsAllowed,
            Is.False,
            "the same email bucket must be shared across different callers/IPs");
    }

    [Test]
    public void CheckAndConsume_DifferentClientIps_HaveIndependentIpBuckets()
    {
        var limiter = CreateLimiter(
            new ManualTimeProvider(),
            maxAttemptsPerEmail: 100,
            maxAttemptsPerIp: 1);

        var fromFirstIp = limiter.CheckAndConsume("a@example.com", "203.0.113.1");
        var fromSecondIp = limiter.CheckAndConsume("b@example.com", "203.0.113.2");

        Assert.Multiple(() =>
        {
            Assert.That(fromFirstIp.IsAllowed, Is.True);
            Assert.That(fromSecondIp.IsAllowed, Is.True, "a different IP must not share the first IP's budget");
        });
    }

    [Test]
    public void CheckAndConsume_TheSameEmailFromDifferentIps_IsStillLimitedByItsEmailBucket()
    {
        // The IP budget is generous here on purpose: this isolates the
        // email bucket as the thing doing the limiting, proving that
        // switching source IP does not reset or bypass it.
        var limiter = CreateLimiter(
            new ManualTimeProvider(),
            maxAttemptsPerEmail: 2,
            maxAttemptsPerIp: 100);

        limiter.CheckAndConsume("person@example.com", "203.0.113.1");
        limiter.CheckAndConsume("person@example.com", "203.0.113.2");

        var thirdFromAThirdIp = limiter.CheckAndConsume("person@example.com", "203.0.113.3");

        Assert.That(thirdFromAThirdIp.IsAllowed, Is.False);
    }

    [Test]
    public void CheckAndConsume_ManyDistinctEmailsFromOneIp_AreCappedByTheIpBucket()
    {
        var limiter = CreateLimiter(
            new ManualTimeProvider(),
            maxAttemptsPerEmail: 100,
            maxAttemptsPerIp: 2);

        limiter.CheckAndConsume("a@example.com", "203.0.113.1");
        limiter.CheckAndConsume("b@example.com", "203.0.113.1");

        var thirdDistinctEmailSameIp = limiter.CheckAndConsume("c@example.com", "203.0.113.1");

        Assert.That(
            thirdDistinctEmailSameIp.IsAllowed,
            Is.False,
            "a single IP hammering many distinct target emails must still be capped");
    }
}
