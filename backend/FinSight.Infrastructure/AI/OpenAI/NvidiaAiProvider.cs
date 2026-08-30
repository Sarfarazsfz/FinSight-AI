using System.ClientModel;
using System.Text.Json;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Ai;
using OpenAI;
using OpenAI.Chat;

namespace FinSight.Infrastructure.AI.OpenAI;

/// <summary>
/// F9 exception-explanation adapter for NVIDIA's hosted, OpenAI-compatible
/// Chat Completions endpoint -- mirrors OpenAiProvider's exact request/
/// response shape (single JSON-schema-constrained call, no tools, same
/// AiExplanationRequest/Response contract) so F9's structured explanation
/// behavior is identical regardless of which provider answers.
///
/// Deliberately LAZY validation (unlike GeminiAiProvider/OpenAiProvider,
/// which throw eagerly in their constructors): NVIDIA is an optional
/// third provider, and AiProviderRouter's chain uses IsAvailable as a
/// preflight to skip an unconfigured provider without ever calling it.
/// An eager throw here would make NVIDIA configuration mandatory for
/// every deployment, breaking existing Gemini+OpenAI-only setups the
/// moment this class is constructed at DI-resolution time.
/// </summary>
public sealed class NvidiaAiProvider : INvidiaAiProvider
{
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _baseUrl;
    private readonly Lazy<ChatClient?> _client;

    public NvidiaAiProvider(
        string apiKey,
        string model,
        string baseUrl)
    {
        _apiKey = apiKey ?? string.Empty;
        _model = model ?? string.Empty;
        _baseUrl = baseUrl ?? string.Empty;

        _client =
            new Lazy<ChatClient?>(
                () =>
                {
                    if (!IsConfigured(out var endpoint))
                    {
                        return null;
                    }

                    return new ChatClient(
                        _model,
                        new ApiKeyCredential(_apiKey),
                        new OpenAIClientOptions { Endpoint = endpoint });
                });
    }

    public string ProviderName => "NVIDIA";

    /// <summary>
    /// Real, computed configuredness (unlike Gemini/OpenAI's hardcoded
    /// `true`) -- this is what lets AiProviderRouter's chain exclude an
    /// unconfigured NVIDIA from the effective chain without ever
    /// attempting a call.
    /// </summary>
    public bool IsAvailable => IsConfigured(out _);

    public async Task<AiExplanationResponse> GenerateExplanationAsync(
        AiExplanationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var client = _client.Value;

        if (client is null)
        {
            throw new InvalidOperationException(
                "NVIDIA AI provider is not configured.");
        }

        var prompt = BuildPrompt(request);

        var messages = new List<ChatMessage>
        {
            new UserChatMessage(prompt)
        };

        var options =
            new ChatCompletionOptions
            {
                ResponseFormat =
                    ChatResponseFormat.CreateJsonSchemaFormat(
                        jsonSchemaFormatName:
                            "reconciliation_explanation",

                        jsonSchema:
                            BinaryData.FromString(
                                """
                                {
                                  "type": "object",
                                  "properties": {
                                    "explanation": {
                                      "type": "string"
                                    },
                                    "suggestedCategory": {
                                      "type": ["string", "null"]
                                    }
                                  },
                                  "required": [
                                    "explanation",
                                    "suggestedCategory"
                                  ],
                                  "additionalProperties": false
                                }
                                """),

                        jsonSchemaIsStrict: true)
            };

        var result =
            await client.CompleteChatAsync(
                messages,
                options,
                cancellationToken);

        var completion = result.Value;

        var text =
            completion.Content
                .FirstOrDefault()?
                .Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException(
                "NVIDIA returned an empty response.");
        }

        var parsed =
            JsonSerializer.Deserialize<NvidiaResult>(
                text,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

        if (parsed is null ||
            string.IsNullOrWhiteSpace(parsed.Explanation))
        {
            throw new InvalidOperationException(
                "NVIDIA returned an invalid explanation response.");
        }

        return new AiExplanationResponse
        {
            Provider = ProviderName,

            Explanation = parsed.Explanation.Trim(),

            SuggestedCategory =
                string.IsNullOrWhiteSpace(parsed.SuggestedCategory)
                    ? null
                    : parsed.SuggestedCategory.Trim(),

            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private bool IsConfigured(out Uri? endpoint)
    {
        endpoint = null;

        if (string.IsNullOrWhiteSpace(_apiKey) ||
            string.IsNullOrWhiteSpace(_model) ||
            string.IsNullOrWhiteSpace(_baseUrl))
        {
            return false;
        }

        return Uri.TryCreate(_baseUrl, UriKind.Absolute, out endpoint);
    }

    private static string BuildPrompt(AiExplanationRequest request)
    {
        return $"""
You are a financial reconciliation explanation assistant.

The deterministic reconciliation engine is authoritative.
Never override its category.
Use only the supplied facts.

Transaction reference:
{request.TransactionReference}

Deterministic category:
{request.DeterministicCategory}

Involved sources:
{request.InvolvedSources}

Discrepancy detail:
{request.DiscrepancyDetail}

Explain what happened in clear financial
reconciliation terms.

The deterministic category is authoritative.
Return only the requested structured result.
""";
    }

    private sealed class NvidiaResult
    {
        public string Explanation { get; init; } = string.Empty;

        public string? SuggestedCategory { get; init; }
    }
}
