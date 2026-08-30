namespace FinSight.Application.Exceptions;

/// <summary>
/// Thrown by FinanceAssistantProviderRouter when every provider in its
/// configured chain (Gemini, NVIDIA, OpenAI -- or whichever subset is
/// actually configured) is unavailable/fails for one request, or when no
/// provider is configured at all.
///
/// Deliberately a distinct type from AiProviderUnavailableException (F9's
/// exception-explanation surface) rather than a shared one: the two
/// features have separate routers, separate failure semantics tests, and
/// mapping them to the same type would mean any future divergence in one
/// surface's handling risks silently changing the other's. Both are thin,
/// intentionally trivial leaf types -- no logic to deduplicate -- mapped by
/// GlobalExceptionHandler to the same 503 status, each with its own detail
/// text.
/// </summary>
public sealed class FinanceAssistantProviderUnavailableException
    : InvalidOperationException
{
    public FinanceAssistantProviderUnavailableException(
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
