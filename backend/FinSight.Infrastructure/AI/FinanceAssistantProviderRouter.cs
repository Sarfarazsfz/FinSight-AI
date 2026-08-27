using FinSight.Application.AI;

namespace FinSight.Infrastructure.AI;

public sealed class FinanceAssistantProviderRouter
    : IFinanceAssistantProvider
{
    private readonly IFinanceAssistantProvider _gemini;
    private readonly IFinanceAssistantProvider _openAi;
    private readonly AiProviderOptions _options;

    public FinanceAssistantProviderRouter(
        IFinanceAssistantProvider gemini,
        IFinanceAssistantProvider openAi,
        AiProviderOptions options)
    {
        _gemini = gemini;
        _openAi = openAi;
        _options = options;
    }

    public string ProviderName =>
        ResolvePrimary().ProviderName;

    public async Task<FinanceAssistantProviderResponse> AskAsync(
        FinanceAssistantProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var primary =
            ResolvePrimary();

        try
        {
            return await primary.AskAsync(
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
                throw new InvalidOperationException(
                    $"Finance Assistant provider '{primary.ProviderName}' failed.",
                    primaryException);
            }

            var fallback =
                ResolveFallback(primary);

            try
            {
                return await fallback.AskAsync(
                    request,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception fallbackException)
            {
                throw new InvalidOperationException(
                    "Both Finance Assistant AI providers failed.",
                    new AggregateException(
                        primaryException,
                        fallbackException));
            }
        }
    }

    private IFinanceAssistantProvider ResolvePrimary()
    {
        var configured =
            _options.DefaultProvider
                .Trim()
                .ToLowerInvariant();

        if (configured == "openai")
        {
            return _openAi;
        }

        return _gemini;
    }

    private IFinanceAssistantProvider ResolveFallback(
        IFinanceAssistantProvider primary)
    {
        return ReferenceEquals(
            primary,
            _gemini)
                ? _openAi
                : _gemini;
    }
}
