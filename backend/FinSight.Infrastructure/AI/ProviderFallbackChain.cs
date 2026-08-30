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
/// </summary>
public sealed class ProviderFallbackChain<TProvider, TRequest, TResponse>
{
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
        Func<Exception>? noProviderConfiguredExceptionFactory = null)
    {
        _candidates = candidates;
        _invoke = invoke;
        _singleFailureExceptionFactory = singleFailureExceptionFactory;
        _allFailedExceptionFactory = allFailedExceptionFactory;
        _isAvailable = isAvailable;
        _noProviderConfiguredExceptionFactory = noProviderConfiguredExceptionFactory;
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

            try
            {
                return await _invoke(provider, request, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failures.Add((name, ex));

                var isLast = i == effective.Count - 1;

                if (!isLast)
                {
                    continue;
                }

                if (failures.Count == 1)
                {
                    throw _singleFailureExceptionFactory(name, ex, excluded);
                }

                throw _allFailedExceptionFactory(failures, excluded);
            }
        }

        // Unreachable: the loop above always returns or throws.
        throw new InvalidOperationException(
            "Provider fallback chain produced no result.");
    }
}
