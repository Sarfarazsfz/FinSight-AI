namespace FinSight.Infrastructure.AI;

public sealed class AiProviderOptions
{
    public GeminiOptions Gemini { get; init; } = new();

    public OpenAiOptions OpenAI { get; init; } = new();

    public string DefaultProvider { get; init; } = "Gemini";

    public bool FallbackEnabled { get; init; } = true;

    public sealed class GeminiOptions
    {
        public string ApiKey { get; init; } = string.Empty;

        public string Model { get; init; } = "gemini-2.5-flash";
    }

    public sealed class OpenAiOptions
    {
        public string ApiKey { get; init; } = string.Empty;

        public string Model { get; init; } = "gpt-5-mini";
    }
}