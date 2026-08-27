namespace FinSight.Application.Exceptions;

public sealed class AiProviderUnavailableException
    : InvalidOperationException
{
    public AiProviderUnavailableException(
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
    }
}