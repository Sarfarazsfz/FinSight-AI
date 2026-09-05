using FinSight.Application.AI;
using FinSight.Application.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.Infrastructure.AI;

/// <summary>
/// F10 Finance Assistant router. Built on the same shared, generic
/// ProviderFallbackChain as AiProviderRouter (F9) -- this class's own
/// responsibility is entirely F10-specific: resolving
/// AiProviderOptions.FinanceAssistant.ProviderOrder into concrete
/// providers, honoring FallbackEnabled, and reproducing the exact
/// pre-existing message shapes ("Finance Assistant provider 'X' failed."
/// for a single failure, "All N configured Finance Assistant AI providers
/// failed." for two-or-more, "No Finance Assistant provider is
/// configured." when the resolved chain is empty). IFinanceAssistantProvider
/// has no IsAvailable concept, so no preflight is used here -- every
/// candidate is genuinely invoked in order, unlike F9.
///
/// Global AI Provider DI Resolution fix: this constructor used to take
/// three IFinanceAssistantProvider parameters directly (concrete Gemini/
/// NVIDIA/OpenAI registrations), which forced construction of all three
/// -- including GeminiFinanceAssistantProvider's IFinanceAssistantModelClient
/// dependency -- the instant anything resolved IFinanceAssistantProvider,
/// regardless of whether Gemini even appeared in FinanceAssistant.
/// ProviderOrder. It now takes IServiceProvider and resolves each named
/// provider from a keyed DI registration only for names that survive the
/// Enabled + ProviderOrder filter -- see AiProviderRouter's identical
/// fix for the full rationale.
/// </summary>
public sealed class FinanceAssistantProviderRouter
    : IFinanceAssistantProvider
{
    private readonly IReadOnlyDictionary<string, IFinanceAssistantProvider> _providersByName;
    private readonly IReadOnlyList<string> _orderNames;

    private readonly ProviderFallbackChain<
        IFinanceAssistantProvider,
        FinanceAssistantProviderRequest,
        FinanceAssistantProviderResponse> _chain;

    public FinanceAssistantProviderRouter(
        IServiceProvider serviceProvider,
        AiProviderOptions options)
    {
        var enabledNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (options.Providers.Gemini.Enabled)
        {
            enabledNames.Add("Gemini");
        }

        if (options.Providers.Nvidia.Enabled)
        {
            enabledNames.Add("NVIDIA");
        }

        if (options.Providers.OpenAI.Enabled)
        {
            enabledNames.Add("OpenAI");
        }

        var configuredOrder =
            options.FinanceAssistant.ProviderOrder
                .Where(enabledNames.Contains)
                .ToList();

        _orderNames =
            options.FinanceAssistant.FallbackEnabled
                ? configuredOrder
                : configuredOrder.Take(1).ToList();

        // Resolved by name, on demand, from a keyed DI registration --
        // only for names that survived the filter above.
        _providersByName =
            _orderNames
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    name => name,
                    name =>
                        serviceProvider
                            .GetRequiredKeyedService<IFinanceAssistantProvider>(
                                name),
                    StringComparer.OrdinalIgnoreCase);

        var candidates =
            _orderNames
                .Select(name => (name, _providersByName[name]))
                .ToList();

        _chain =
            new ProviderFallbackChain<
                IFinanceAssistantProvider,
                FinanceAssistantProviderRequest,
                FinanceAssistantProviderResponse>(
                candidates,
                invoke: (provider, request, cancellationToken) =>
                    provider.AskAsync(request, cancellationToken),
                // P-1I-FIX-2: was a plain InvalidOperationException, which
                // GlobalExceptionHandler has no mapping for -- it fell
                // through to the generic 500 ("An unexpected error
                // occurred") instead of the calm, tested 503 every other
                // AI-unavailable path produces. Confirmed live: with the
                // new per-provider timeout in place, a single-effective-
                // provider Finance Assistant call that times out is
                // exactly this path. AiProviderRouter's sibling
                // single-failure factory already wraps in
                // AiProviderUnavailableException (see its own
                // singleFailureExceptionFactory); this brings
                // FinanceAssistantProviderRouter into parity with an
                // already-correct sibling, not a new invented design.
                singleFailureExceptionFactory: (name, ex, _) =>
                    new FinanceAssistantProviderUnavailableException(
                        $"Finance Assistant provider '{name}' failed.",
                        ex),
                allFailedExceptionFactory: (failures, _) =>
                    new FinanceAssistantProviderUnavailableException(
                        $"All {failures.Count} configured Finance Assistant " +
                        "AI providers failed.",
                        new AggregateException(failures.Select(f => f.Error))),
                noProviderConfiguredExceptionFactory: () =>
                    new FinanceAssistantProviderUnavailableException(
                        "No Finance Assistant provider is configured."));
    }

    public string ProviderName =>
        _orderNames.Count > 0
            ? _providersByName[_orderNames[0]].ProviderName
            : "None";

    public Task<FinanceAssistantProviderResponse> AskAsync(
        FinanceAssistantProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        return _chain.ExecuteAsync(request, cancellationToken);
    }
}
