using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Ai;
using FinSight.Application.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.Infrastructure.AI;

/// <summary>
/// F9 exception-explanation router. Generalized from a hardcoded
/// Gemini/OpenAI binary choice to an ordered N-provider chain (NVIDIA
/// added as a third, optional provider), built on the shared, generic
/// ProviderFallbackChain -- the actual try/fallback/cancellation
/// mechanics live there, not here.
///
/// This class's own responsibility is entirely F9-specific: resolving
/// AiProviderOptions.ExceptionExplanation.ProviderOrder into concrete
/// providers, honoring FallbackEnabled (false truncates the chain to just
/// the first configured provider, exactly as before), using each
/// provider's own IsAvailable as the chain's preflight (a provider that
/// reports unavailable is never invoked at all -- this is F9-specific
/// richness F10 doesn't have, since IFinanceAssistantProvider carries no
/// such concept), and reproducing the four distinct message shapes the
/// original hand-written router produced: a single genuine failure with
/// nothing excluded, a single genuine failure with the fallback excluded
/// by IsAvailable, two genuine failures (the exact legacy "Both AI
/// providers failed..." wording), and three-or-more genuine failures
/// (new, NVIDIA-only wording, never reachable with only two providers).
///
/// Global AI Provider DI Resolution fix: this constructor used to take
/// IGeminiAiProvider/INvidiaAiProvider/IOpenAiProvider directly, which
/// forced ASP.NET Core's DI container to construct all three concrete
/// providers the instant *anything* needed an IAiProvider -- regardless
/// of whether a given provider even appeared in ProviderOrder. Since
/// AiExplanationService (and therefore ReconciliationController, which
/// also exposes an AI-explanation endpoint) took IAiProvider as an
/// unconditional constructor dependency, this meant an unrelated,
/// non-AI reconciliation request could fail purely because an
/// out-of-order, unconfigured provider's constructor threw. This
/// constructor now takes IServiceProvider and resolves each named
/// provider from a keyed DI registration ("Gemini"/"NVIDIA"/"OpenAI",
/// registered in DependencyInjection.cs) -- and does so *only* for names
/// that survive the Enabled + ProviderOrder filter below. A provider
/// absent from the order, or disabled, is never asked for and therefore
/// never constructed.
/// </summary>
public sealed class AiProviderRouter : IAiProvider
{
    private readonly IReadOnlyDictionary<string, IAiProvider> _providersByName;
    private readonly IReadOnlyList<string> _orderNames;

    private readonly ProviderFallbackChain<IAiProvider, AiExplanationRequest, AiExplanationResponse>
        _chain;

    public AiProviderRouter(
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
            options.ExceptionExplanation.ProviderOrder
                .Where(enabledNames.Contains)
                .ToList();

        // FallbackEnabled=false means "attempt only the resolved primary,
        // never fall through" -- exactly the pre-refactor behavior,
        // expressed here as truncating the chain to one candidate.
        _orderNames =
            options.ExceptionExplanation.FallbackEnabled
                ? configuredOrder
                : configuredOrder.Take(1).ToList();

        // Resolved by name, on demand, from a keyed DI registration --
        // only for names that survived the filter above. A provider
        // excluded from the effective order (disabled, or simply not
        // listed) is never resolved and therefore never constructed.
        _providersByName =
            _orderNames
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    name => name,
                    name =>
                        serviceProvider
                            .GetRequiredKeyedService<IAiProvider>(name),
                    StringComparer.OrdinalIgnoreCase);

        var candidates =
            _orderNames
                .Select(name => (name, _providersByName[name]))
                .ToList();

        _chain =
            new ProviderFallbackChain<IAiProvider, AiExplanationRequest, AiExplanationResponse>(
                candidates,
                invoke: (provider, request, cancellationToken) =>
                    provider.GenerateExplanationAsync(request, cancellationToken),
                singleFailureExceptionFactory: (name, ex, excluded) =>
                    excluded.Count == 0
                        ? new AiProviderUnavailableException(
                            $"AI provider '{name}' failed to generate an explanation.",
                            ex)
                        : new AiProviderUnavailableException(
                            $"AI provider '{name}' failed and the fallback AI provider " +
                            $"'{excluded[0]}' is unavailable.",
                            ex),
                allFailedExceptionFactory: (failures, _) =>
                    failures.Count == 2
                        ? new AiProviderUnavailableException(
                            $"Both AI providers failed. Primary provider '{failures[0].Name}' " +
                            $"and fallback provider '{failures[1].Name}' were unavailable.",
                            new AggregateException(failures.Select(f => f.Error)))
                        : new AiProviderUnavailableException(
                            $"All {failures.Count} configured AI providers failed.",
                            new AggregateException(failures.Select(f => f.Error))),
                isAvailable: provider => provider.IsAvailable,
                noProviderConfiguredExceptionFactory: () =>
                    new AiProviderUnavailableException(
                        "No configured AI provider is available."));
    }

    public string ProviderName =>
        _orderNames.Count > 0 ? _orderNames[0] : "None";

    public bool IsAvailable =>
        _providersByName.Values.Any(provider => provider.IsAvailable);

    public Task<AiExplanationResponse> GenerateExplanationAsync(
        AiExplanationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return _chain.ExecuteAsync(request, cancellationToken);
    }
}
