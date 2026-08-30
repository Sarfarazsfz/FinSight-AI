namespace FinSight.Infrastructure.AI;

/// <summary>
/// Global AI provider configuration, shared by every AI surface (F9
/// exception explanation, F10 Finance Assistant, and any future surface).
/// Provider credentials/model/base-URL exist exactly once here --
/// <see cref="Providers"/> -- never duplicated per surface. Each surface
/// owns only its own provider chain order and fallback toggle
/// (<see cref="ExceptionExplanation"/>/<see cref="FinanceAssistant"/>),
/// since different surfaces may reasonably want different chains even
/// from the same shared provider pool.
///
/// Bound in DependencyInjection.cs, which also owns backward-compatible
/// translation from the legacy flat `AI:DefaultProvider`/
/// `AI:FallbackEnabled` keys when a surface's new nested keys are absent
/// -- this class itself carries no legacy-vs-new branching, only the
/// final resolved shape.
/// </summary>
public sealed class AiProviderOptions
{
    public ProvidersOptions Providers { get; init; } = new();

    public SurfaceOptions ExceptionExplanation { get; init; } = new();

    public SurfaceOptions FinanceAssistant { get; init; } = new();

    public sealed class ProvidersOptions
    {
        public GeminiOptions Gemini { get; init; } = new();

        public OpenAiOptions OpenAI { get; init; } = new();

        public NvidiaOptions Nvidia { get; init; } = new();
    }

    /// <summary>
    /// One AI surface's provider chain. Defaults to the pre-NVIDIA
    /// ["Gemini","OpenAI"] order and fallback-on, matching both F9's and
    /// F10's original behavior exactly when nothing is configured.
    /// </summary>
    public sealed class SurfaceOptions
    {
        public IReadOnlyList<string> ProviderOrder { get; init; } =
            new[] { "Gemini", "OpenAI" };

        public bool FallbackEnabled { get; init; } = true;
    }

    public sealed class GeminiOptions
    {
        /// <summary>
        /// Explicit false excludes this provider from every surface's
        /// chain regardless of ProviderOrder. Absent (the default, true)
        /// preserves pre-existing behavior -- every surface using this
        /// provider today keeps using it.
        /// </summary>
        public bool Enabled { get; init; } = true;

        public string ApiKey { get; init; } = string.Empty;

        public string Model { get; init; } = "gemini-2.5-flash";
    }

    public sealed class OpenAiOptions
    {
        public bool Enabled { get; init; } = true;

        public string ApiKey { get; init; } = string.Empty;

        public string Model { get; init; } = "gpt-5-mini";

        /// <summary>
        /// Optional -- OpenAI's own default endpoint is used when unset,
        /// exactly as before this refactor. Present here (unlike Gemini,
        /// which has no BaseUrl at all) because OpenAI's client
        /// construction already supports a custom endpoint via the
        /// installed SDK's OpenAIClientOptions.
        /// </summary>
        public string? BaseUrl { get; init; }
    }

    public sealed class NvidiaOptions
    {
        public bool Enabled { get; init; } = true;

        public string ApiKey { get; init; } = string.Empty;

        public string Model { get; init; } = "openai/gpt-oss-120b";

        public string BaseUrl { get; init; } =
            "https://integrate.api.nvidia.com/v1";
    }
}
