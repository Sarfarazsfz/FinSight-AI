namespace FinSight.Infrastructure.AI;

/// <summary>
/// Generic, capability-agnostic "try providers in order, fall through on
/// failure" engine shared by AiProviderRouter (F9) and
/// FinanceAssistantProviderRouter (F10). Knows nothing about Gemini,
/// NVIDIA, OpenAI, exception explanation, Finance Assistant, tools, or the
/// database -- every provider-specific behavior (which providers exist,
/// what "available" means, what exception/message shape to produce) is
/// supplied by the caller via the constructor delegates.
///
/// Algorithm:
/// 1. Optionally filter out candidates <paramref name="isAvailable"/>
///    reports unavailable, *before* ever invoking them (no call is made
///    to a provider that fails this preflight check).
/// 2. If nothing is left after that filter, throw via
///    <paramref name="noProviderConfiguredExceptionFactory"/> (or a
///    generic default if the caller doesn't supply one).
/// 3. Try each remaining candidate in order via
///    <paramref name="invoke"/>. On success, return immediately -- no
///    later candidate is ever touched.
/// 4. <see cref="OperationCanceledException"/> always propagates
///    immediately, never counted as a "failure" and never triggers
///    fallback.
/// 5. Any other exception is recorded and the chain moves to the next
///    candidate. When the last candidate fails: if it was the *only*
///    one actually invoked, the caller's single-failure factory is used
///    (this is where the preflight-excluded names are surfaced, so a
///    caller can still say "the fallback was unavailable" even though
///    that fallback was never invoked); otherwise the all-failed factory
///    is used with every recorded (name, error) pair.
///
/// No provider is ever invoked more than once. No retries, no recursion.
///
/// P-1I-FIX-2: every individual provider call is wrapped in its own
/// bounded timeout (<see cref="DefaultPerProviderCallTimeout"/>). Before this
/// fix, nothing anywhere in the AI call path -- not this chain, not
/// either router, not any of the six provider implementations, not the
/// ASP.NET Core host -- ever bounded how long a single provider call
/// could take; a provider that never responded left the whole HTTP
/// request pending indefinitely, with no fallback, no failure response,
/// and no audit event ever written (the existing failure-audit code
/// is correct but was simply unreachable, since the awaited call never
/// threw and never returned). A provider that merely times out is
/// treated exactly like any other per-provider failure -- the chain
/// still falls through to the next candidate, or reports a normal
/// bounded failure if it was the last one. The caller's own
/// <paramref name="cancellationToken"/> (e.g. the HTTP request being
/// aborted) is still distinguished from this internal timeout and
/// always propagates immediately, unchanged from before.
/// </summary>
public sealed class ProviderFallbackChain<TProvider, TRequest, TResponse>
{
    /// <summary>
    /// Generous enough for a real Gemini/OpenAI/NVIDIA chat completion
    /// (including tool-call turns), but finite -- the one property this
    /// class had none of at all before this fix. Every production caller
    /// (AiProviderRouter, FinanceAssistantProviderRouter) uses this
    /// default and shares it uniformly; it is not exposed as application
    /// configuration, since it is a defensive upper bound on a single
    /// call, not a tunable product setting. The constructor's optional
    /// override exists solely so tests can prove the timeout behavior
    /// itself deterministically and quickly, without waiting out a real
    /// 30-second clock.
    /// </summary>
    private static readonly TimeSpan DefaultPerProviderCallTimeout =
        TimeSpan.FromSeconds(30);

    private readonly TimeSpan _perProviderCallTimeout;

    private readonly IReadOnlyList<(string Name, TProvider Provider)> _candidates;
    private readonly Func<TProvider, TRequest, CancellationToken, Task<TResponse>> _invoke;
    private readonly Func<TProvider, bool>? _isAvailable;

    private readonly Func<string, Exception, IReadOnlyList<string>, Exception>
        _singleFailureExceptionFactory;

    private readonly Func<IReadOnlyList<(string Name, Exception Error)>, IReadOnlyList<string>, Exception>
        _allFailedExceptionFactory;

    private readonly Func<Exception>? _noProviderConfiguredExceptionFactory;

    public ProviderFallbackChain(
        IReadOnlyList<(string Name, TProvider Provider)> candidates,
        Func<TProvider, TRequest, CancellationToken, Task<TResponse>> invoke,
        Func<string, Exception, IReadOnlyList<string>, Exception> singleFailureExceptionFactory,
        Func<IReadOnlyList<(string Name, Exception Error)>, IReadOnlyList<string>, Exception> allFailedExceptionFactory,
        Func<TProvider, bool>? isAvailable = null,
        Func<Exception>? noProviderConfiguredExceptionFactory = null,
        TimeSpan? perProviderCallTimeout = null)
    {
        _candidates = candidates;
        _invoke = invoke;
        _singleFailureExceptionFactory = singleFailureExceptionFactory;
        _allFailedExceptionFactory = allFailedExceptionFactory;
        _isAvailable = isAvailable;
        _noProviderConfiguredExceptionFactory = noProviderConfiguredExceptionFactory;
        _perProviderCallTimeout = perProviderCallTimeout ?? DefaultPerProviderCallTimeout;
    }

    public async Task<TResponse> ExecuteAsync(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        var excluded = new List<string>();
        var effective = new List<(string Name, TProvider Provider)>();

        foreach (var candidate in _candidates)
        {
            if (_isAvailable is not null && !_isAvailable(candidate.Provider))
            {
                excluded.Add(candidate.Name);
                continue;
            }

            effective.Add(candidate);
        }

        if (effective.Count == 0)
        {
            throw _noProviderConfiguredExceptionFactory?.Invoke()
                ?? new InvalidOperationException(
                    "No provider is configured or available.");
        }

        var failures = new List<(string Name, Exception Error)>();

        for (var i = 0; i < effective.Count; i++)
        {
            var (name, provider) = effective[i];

            Exception failure;

            using (var timeoutCts =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeoutCts.CancelAfter(_perProviderCallTimeout);

                try
                {
                    return await _invoke(provider, request, timeoutCts.Token);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // The CALLER's own token fired (e.g. the HTTP request
                    // was aborted) -- not a provider failure. Always
                    // propagates immediately, exactly as before this fix.
                    throw;
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                {
                    // Only this call's own bounded timeout fired -- the
                    // caller's token is still live. Treated as an
                    // ordinary provider failure -- never left pending,
                    // never a special "hung" state -- so the chain can
                    // fall through to the next candidate (or report a
                    // normal bounded failure) instead of the request
                    // hanging indefinitely.
                    failure =
                        new TimeoutException(
                            $"Provider '{name}' did not respond within " +
                            $"{_perProviderCallTimeout}.");
                }
                catch (OperationCanceledException)
                {
                    // Neither the caller's token nor our own timeout is
                    // actually canceled -- the provider threw/observed
                    // cancellation for its own reasons (e.g. a test
                    // double, or an internal token unrelated to either of
                    // ours). Preserves this class's original, pre-fix
                    // contract exactly: always propagates immediately,
                    // never counted as a failure, never triggers
                    // fallback.
                    throw;
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            }

            failures.Add((name, failure));

            var isLast = i == effective.Count - 1;

            if (!isLast)
            {
                continue;
            }

            if (failures.Count == 1)
            {
                throw _singleFailureExceptionFactory(name, failure, excluded);
            }

            throw _allFailedExceptionFactory(failures, excluded);
        }

        // Unreachable: the loop above always returns or throws.
        throw new InvalidOperationException(
            "Provider fallback chain produced no result.");
    }
}
