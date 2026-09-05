using FinSight.Infrastructure.AI;

namespace FinSight.Tests.AI;

/// <summary>
/// Tests the generic fallback engine in complete isolation from any real
/// provider -- fake "providers" here are plain strings, and "requests"/
/// "responses" are plain ints, proving the chain genuinely knows nothing
/// about Gemini/NVIDIA/OpenAI/exception-explanation/Finance-Assistant.
/// </summary>
[TestFixture]
public sealed class ProviderFallbackChainTests
{
    [Test]
    public async Task FirstProviderSucceeds_StopsImmediately_LaterProvidersNeverInvoked()
    {
        var calls = new List<string>();

        var chain = CreateChain(
            new[] { "A", "B", "C" },
            (name, _, _) =>
            {
                calls.Add(name);
                return Task.FromResult(name == "A" ? 100 : -1);
            });

        var result = await chain.ExecuteAsync(0);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(100));
            Assert.That(calls, Is.EqualTo(new[] { "A" }));
        });
    }

    [Test]
    public async Task FirstFails_SecondSucceeds()
    {
        var calls = new List<string>();

        var chain = CreateChain(
            new[] { "A", "B", "C" },
            (name, _, _) =>
            {
                calls.Add(name);

                if (name == "A")
                {
                    throw new InvalidOperationException("A failed");
                }

                return Task.FromResult(200);
            });

        var result = await chain.ExecuteAsync(0);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(200));
            Assert.That(calls, Is.EqualTo(new[] { "A", "B" }));
        });
    }

    [Test]
    public async Task SecondFails_ThirdSucceeds()
    {
        var calls = new List<string>();

        var chain = CreateChain(
            new[] { "A", "B", "C" },
            (name, _, _) =>
            {
                calls.Add(name);

                if (name is "A" or "B")
                {
                    throw new InvalidOperationException($"{name} failed");
                }

                return Task.FromResult(300);
            });

        var result = await chain.ExecuteAsync(0);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(300));
            Assert.That(calls, Is.EqualTo(new[] { "A", "B", "C" }));
        });
    }

    [Test]
    public void AllFail_InvokesAllFailedExceptionFactory_WithEveryNamedFailureInOrder()
    {
        var calls = new List<string>();

        IReadOnlyList<(string Name, Exception Error)>? capturedFailures = null;
        IReadOnlyList<string>? capturedExcluded = null;

        var chain =
            new ProviderFallbackChain<string, int, int>(
                new[] { ("A", "A"), ("B", "B"), ("C", "C") },
                (name, _, _) =>
                {
                    calls.Add(name);
                    return Task.FromException<int>(
                        new InvalidOperationException($"{name} failed"));
                },
                singleFailureExceptionFactory: (name, ex, _) =>
                    new InvalidOperationException($"single:{name}", ex),
                allFailedExceptionFactory: (failures, excluded) =>
                {
                    capturedFailures = failures;
                    capturedExcluded = excluded;
                    return new InvalidOperationException("all failed");
                });

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await chain.ExecuteAsync(0));

        Assert.Multiple(() =>
        {
            Assert.That(calls, Is.EqualTo(new[] { "A", "B", "C" }));
            Assert.That(capturedFailures, Is.Not.Null);
            Assert.That(capturedFailures!.Select(f => f.Name), Is.EqualTo(new[] { "A", "B", "C" }));
            Assert.That(capturedExcluded, Is.Empty);
        });
    }

    [Test]
    public void OperationCanceled_ImmediatelyRethrows_NeverInvokesLaterProviders()
    {
        var calls = new List<string>();

        var chain =
            new ProviderFallbackChain<string, int, int>(
                new[] { ("A", "A"), ("B", "B") },
                (name, _, _) =>
                {
                    calls.Add(name);
                    return Task.FromException<int>(new OperationCanceledException());
                },
                singleFailureExceptionFactory: (name, ex, _) => ex,
                allFailedExceptionFactory: (failures, _) => new AggregateException());

        Assert.ThrowsAsync<OperationCanceledException>(
            async () => await chain.ExecuteAsync(0));

        Assert.That(calls, Is.EqualTo(new[] { "A" }));
    }

    [Test]
    public async Task EachProvider_IsInvokedAtMostOnce()
    {
        var callCounts = new Dictionary<string, int>();

        var chain = CreateChain(
            new[] { "A", "B" },
            (name, _, _) =>
            {
                callCounts[name] = callCounts.GetValueOrDefault(name) + 1;

                if (name == "A")
                {
                    throw new InvalidOperationException("A failed");
                }

                return Task.FromResult(1);
            });

        await chain.ExecuteAsync(0);

        Assert.Multiple(() =>
        {
            Assert.That(callCounts["A"], Is.EqualTo(1));
            Assert.That(callCounts["B"], Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ConfiguredOrder_IsRespectedExactly_NotAlphabetical()
    {
        var calls = new List<string>();

        var chain = CreateChain(
            new[] { "Zeta", "Alpha", "Middle" },
            (name, _, _) =>
            {
                calls.Add(name);
                return name == "Zeta"
                    ? Task.FromResult(1)
                    : Task.FromException<int>(new InvalidOperationException());
            });

        await chain.ExecuteAsync(0);

        Assert.That(calls, Is.EqualTo(new[] { "Zeta" }));
    }

    [Test]
    public async Task PreflightUnavailable_ExcludesCandidateWithoutInvokingIt()
    {
        var calls = new List<string>();

        var chain =
            new ProviderFallbackChain<string, int, int>(
                new[] { ("A", "A"), ("B", "B") },
                (name, _, _) =>
                {
                    calls.Add(name);
                    return Task.FromResult(name == "B" ? 42 : -1);
                },
                singleFailureExceptionFactory: (name, ex, _) => ex,
                allFailedExceptionFactory: (failures, _) => new AggregateException(),
                isAvailable: provider => provider != "A");

        var result = await chain.ExecuteAsync(0);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(42));
            // "A" is never invoked at all -- filtered out before any call.
            Assert.That(calls, Is.EqualTo(new[] { "B" }));
        });
    }

    [Test]
    public void PreflightExcludesEverything_ThrowsNoProviderConfiguredFactory_WithoutInvokingAnything()
    {
        var calls = new List<string>();

        var chain =
            new ProviderFallbackChain<string, int, int>(
                new[] { ("A", "A"), ("B", "B") },
                (name, _, _) =>
                {
                    calls.Add(name);
                    return Task.FromResult(1);
                },
                singleFailureExceptionFactory: (name, ex, _) => ex,
                allFailedExceptionFactory: (failures, _) => new AggregateException(),
                isAvailable: _ => false,
                noProviderConfiguredExceptionFactory: () =>
                    new InvalidOperationException("nothing configured"));

        var thrown =
            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await chain.ExecuteAsync(0));

        Assert.Multiple(() =>
        {
            Assert.That(thrown!.Message, Is.EqualTo("nothing configured"));
            Assert.That(calls, Is.Empty);
        });
    }

    [Test]
    public void SingleEffectiveCandidateFails_PassesExcludedNamesToSingleFailureFactory()
    {
        // Proves the "excluded by preflight" names are surfaced even when
        // the chain never got to try them -- lets a caller build a message
        // like "primary failed and the fallback is unavailable" without
        // the chain knowing what that sentence means.
        IReadOnlyList<string>? capturedExcluded = null;

        var chain =
            new ProviderFallbackChain<string, int, int>(
                new[] { ("A", "A"), ("B", "B") },
                (_, _, _) => Task.FromException<int>(new InvalidOperationException("A failed")),
                singleFailureExceptionFactory: (name, ex, excluded) =>
                {
                    capturedExcluded = excluded;
                    return new InvalidOperationException($"single:{name}", ex);
                },
                allFailedExceptionFactory: (failures, _) => new AggregateException(),
                isAvailable: provider => provider != "B");

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await chain.ExecuteAsync(0));

        Assert.That(capturedExcluded, Is.EqualTo(new[] { "B" }));
    }

    // ------------------------------------------------------- P-1I-FIX-2:
    // per-provider bounded timeout. These use a small injected timeout
    // (milliseconds, via the constructor's optional override) and a fake
    // provider built on Task.Delay(Timeout.Infinite, cancellationToken)
    // -- a call that never completes on its own, only via cancellation --
    // so the timeout behavior itself is proven deterministically and
    // fast, never by actually waiting out the real 30-second production
    // default.

    [Test]
    public async Task ProviderNeverResponds_TimesOutAndFallsThroughToTheNextProvider()
    {
        var calls = new List<string>();

        var chain =
            new ProviderFallbackChain<string, int, int>(
                new[] { ("A", "A"), ("B", "B") },
                invoke: async (name, _, cancellationToken) =>
                {
                    calls.Add(name);

                    if (name == "A")
                    {
                        // Never completes on its own -- only the chain's
                        // own bounded timeout (via the linked token) ever
                        // ends this call, exactly like a real provider
                        // that never responds.
                        await Task.Delay(Timeout.Infinite, cancellationToken);
                        return -1;
                    }

                    return 42;
                },
                singleFailureExceptionFactory: (name, ex, _) => ex,
                allFailedExceptionFactory: (failures, _) => new AggregateException(),
                perProviderCallTimeout: TimeSpan.FromMilliseconds(30));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await chain.ExecuteAsync(0);
        stopwatch.Stop();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(42));
            Assert.That(calls, Is.EqualTo(new[] { "A", "B" }));

            // The whole point of the fix: bounded, not merely "eventually
            // finishes". Generous margin over the 30ms configured timeout
            // so this never flakes under CI load, while still being
            // orders of magnitude short of a real unbounded hang.
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)));
        });
    }

    [Test]
    public void EveryProviderTimesOut_ReportsABoundedFailure_RecordedAsTimeoutException()
    {
        IReadOnlyList<(string Name, Exception Error)>? capturedFailures = null;

        var chain =
            new ProviderFallbackChain<string, int, int>(
                new[] { ("A", "A"), ("B", "B") },
                invoke: async (_, _, cancellationToken) =>
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                    return -1;
                },
                singleFailureExceptionFactory: (name, ex, _) =>
                    new InvalidOperationException($"single:{name}", ex),
                allFailedExceptionFactory: (failures, _) =>
                {
                    capturedFailures = failures;
                    return new InvalidOperationException("all timed out");
                },
                perProviderCallTimeout: TimeSpan.FromMilliseconds(20));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await chain.ExecuteAsync(0));

        stopwatch.Stop();

        Assert.Multiple(() =>
        {
            // Never hangs -- both candidates time out and the chain
            // still reports a normal bounded failure well within budget.
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)));

            Assert.That(capturedFailures, Is.Not.Null);
            Assert.That(
                capturedFailures!.Select(f => f.Name),
                Is.EqualTo(new[] { "A", "B" }));

            Assert.That(
                capturedFailures!.All(f => f.Error is TimeoutException),
                Is.True,
                "a timed-out provider's recorded failure must be a TimeoutException");
        });
    }

    [Test]
    public void GenuineCallerCancellation_StillPropagatesImmediately_EvenWithATimeoutConfigured()
    {
        // Proves the new per-call timeout machinery never swallows or
        // delays a genuine caller-driven cancellation (e.g. the real HTTP
        // request being aborted) -- it must still propagate immediately
        // and never be treated as a per-provider failure, exactly as
        // before this fix.
        using var cts = new CancellationTokenSource();

        var chain =
            new ProviderFallbackChain<string, int, int>(
                new[] { ("A", "A"), ("B", "B") },
                invoke: async (_, _, cancellationToken) =>
                {
                    cts.Cancel();
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                    return -1;
                },
                singleFailureExceptionFactory: (name, ex, _) => ex,
                allFailedExceptionFactory: (failures, _) => new AggregateException(),
                // Deliberately much larger than the cancellation above,
                // to prove this is the CALLER's cancellation winning, not
                // a coincidentally-short timeout.
                perProviderCallTimeout: TimeSpan.FromSeconds(30));

        Assert.CatchAsync<OperationCanceledException>(
            async () => await chain.ExecuteAsync(0, cts.Token));
    }

    private static ProviderFallbackChain<string, int, int> CreateChain(
        IEnumerable<string> order,
        Func<string, int, CancellationToken, Task<int>> invoke)
    {
        return new ProviderFallbackChain<string, int, int>(
            order.Select(name => (name, name)).ToList(),
            invoke,
            singleFailureExceptionFactory: (name, ex, _) =>
                new InvalidOperationException($"'{name}' failed.", ex),
            allFailedExceptionFactory: (failures, _) =>
                new InvalidOperationException(
                    $"All {failures.Count} configured providers failed."));
    }
}
