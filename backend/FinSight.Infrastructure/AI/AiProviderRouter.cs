using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Ai;
using FinSight.Application.Exceptions;

namespace FinSight.Infrastructure.AI;

public sealed class AiProviderRouter : IAiProvider
{
    private readonly IGeminiAiProvider _gemini;

    private readonly IOpenAiProvider _openAi;

    private readonly AiProviderOptions _options;

    public AiProviderRouter(
        IGeminiAiProvider gemini,
        IOpenAiProvider openAi,
        AiProviderOptions options)
    {
        _gemini = gemini;
        _openAi = openAi;
        _options = options;
    }

    public string ProviderName =>
        _options.DefaultProvider;

    public bool IsAvailable =>
        _gemini.IsAvailable ||
        _openAi.IsAvailable;

    public async Task<AiExplanationResponse>
        GenerateExplanationAsync(
            AiExplanationRequest request,
            CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(
                nameof(request));
        }

        var primary =
            ResolvePrimaryProvider();

        try
        {
            return await primary.GenerateExplanationAsync(
                request,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception primaryException)
        {
            if (!_options.FallbackEnabled)
            {
                throw new AiProviderUnavailableException(
                    $"AI provider '{primary.ProviderName}' " +
                    "failed to generate an explanation.",
                    primaryException);
            }

            var fallback =
                GetFallbackProvider(primary);

            if (!fallback.IsAvailable)
            {
                throw new AiProviderUnavailableException(
                    $"AI provider '{primary.ProviderName}' failed " +
                    $"and the fallback AI provider '{fallback.ProviderName}' " +
                    "is unavailable.",
                    primaryException);
            }

            try
            {
                return await fallback.GenerateExplanationAsync(
                    request,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception fallbackException)
            {
                throw new AiProviderUnavailableException(
                    $"Both AI providers failed. " +
                    $"Primary provider '{primary.ProviderName}' " +
                    $"and fallback provider '{fallback.ProviderName}' " +
                    "were unavailable.",
                    new AggregateException(
                        primaryException,
                        fallbackException));
            }
        }
    }

    private IAiProvider ResolvePrimaryProvider()
    {
        var configuredProvider =
            _options.DefaultProvider
                .Trim()
                .ToLowerInvariant();

        return configuredProvider switch
        {
            "gemini" when _gemini.IsAvailable =>
                _gemini,

            "openai" when _openAi.IsAvailable =>
                _openAi,

            "gemini" when _openAi.IsAvailable =>
                _openAi,

            "openai" when _gemini.IsAvailable =>
                _gemini,

            _ =>
                throw new AiProviderUnavailableException(
                    "No configured AI provider is available.")
        };
    }

    private IAiProvider GetFallbackProvider(
        IAiProvider primary)
    {
        // Must derive fallback from the ACTUAL resolved primary instance,
        // not the configured default string -- ResolvePrimaryProvider can
        // substitute the non-default provider when the default is
        // unavailable, and re-deriving fallback from the string alone
        // would then return that same already-failed provider again
        // instead of genuinely failing over. Mirrors
        // FinanceAssistantProviderRouter.ResolveFallback exactly.
        return ReferenceEquals(primary, _gemini)
            ? _openAi
            : _gemini;
    }
}