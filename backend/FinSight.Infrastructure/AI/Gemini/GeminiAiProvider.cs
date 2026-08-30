using System.Text.Json;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Ai;
using Google.GenAI;
using Google.GenAI.Types;

namespace FinSight.Infrastructure.AI.Gemini;

public sealed class GeminiAiProvider : IGeminiAiProvider
{
    private readonly string _apiKey;
    private readonly string _model;
    private readonly Lazy<Client?> _client;

    // Deliberately LAZY validation (was eager-throw-in-constructor before
    // the Global AI Provider DI Resolution fix): Gemini is one of
    // potentially several configured providers in AiProviderOptions.
    // ExceptionExplanation.ProviderOrder, and AiProviderRouter's chain
    // uses IsAvailable as a preflight to skip an unconfigured provider
    // without ever calling it. An eager throw here made Gemini mandatory
    // for every deployment the moment this class was constructed --
    // which broke any order that still lists Gemini (e.g. the default
    // [Gemini, OpenAI]) whenever only a different provider (e.g. NVIDIA)
    // is actually configured. Mirrors NvidiaAiProvider's established
    // pattern exactly.
    public GeminiAiProvider(
        string apiKey,
        string model)
    {
        _apiKey = apiKey ?? string.Empty;
        _model = model ?? string.Empty;

        _client =
            new Lazy<Client?>(
                () =>
                    IsConfigured()
                        ? new Client(apiKey: _apiKey)
                        : null);
    }

    public string ProviderName => "Gemini";

    /// <summary>
    /// Real, computed configuredness (was hardcoded `true`) -- this is
    /// what lets AiProviderRouter's chain exclude an unconfigured Gemini
    /// from the effective chain without ever attempting a call.
    /// </summary>
    public bool IsAvailable => IsConfigured();

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

        var client = _client.Value;

        if (client is null)
        {
            throw new InvalidOperationException(
                "Gemini AI provider is not configured.");
        }

        var prompt = BuildPrompt(request);

        var schema = new Schema
        {
            Type = Google.GenAI.Types.Type.Object,

            Properties = new Dictionary<string, Schema>
            {
                ["explanation"] = new Schema
                {
                    Type =
                        Google.GenAI.Types.Type.String
                },

                ["suggestedCategory"] = new Schema
                {
                    Type =
                        Google.GenAI.Types.Type.String
                }
            },

            Required = new List<string>
            {
                "explanation",
                "suggestedCategory"
            },

            PropertyOrdering = new List<string>
            {
                "explanation",
                "suggestedCategory"
            }
        };

        var response =
            await client.Models.GenerateContentAsync(
                model: _model,
                contents: prompt,
                config: new GenerateContentConfig
                {
                    Temperature = 0.1,
                    ResponseMimeType =
                        "application/json",
                    ResponseSchema = schema
                },
                cancellationToken:
                    cancellationToken);

        var text =
            response.Candidates?
                .FirstOrDefault()?
                .Content?
                .Parts?
                .FirstOrDefault()?
                .Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException(
                "Gemini returned an empty response.");
        }

        var result =
            JsonSerializer.Deserialize<GeminiResult>(
                text,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

        if (result is null ||
            string.IsNullOrWhiteSpace(
                result.Explanation))
        {
            throw new InvalidOperationException(
                "Gemini returned an invalid explanation response.");
        }

        return new AiExplanationResponse
        {
            Provider = ProviderName,

            Explanation =
                result.Explanation.Trim(),

            SuggestedCategory =
                string.IsNullOrWhiteSpace(
                    result.SuggestedCategory)
                    ? null
                    : result.SuggestedCategory.Trim(),

            GeneratedAtUtc =
                DateTime.UtcNow
        };
    }

    private bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(_apiKey) &&
               !string.IsNullOrWhiteSpace(_model);
    }

    private static string BuildPrompt(
        AiExplanationRequest request)
    {
        return $"""
You are an AI assistant for a financial reconciliation system.

The deterministic reconciliation engine is authoritative.
Do not override or change its category.

Explain the detected reconciliation exception
clearly and briefly using only the supplied facts.

You may suggest a category, but it is only a suggestion.

Transaction reference:
{request.TransactionReference}

Deterministic category:
{request.DeterministicCategory}

Involved sources:
{request.InvolvedSources}

Discrepancy detail:
{request.DiscrepancyDetail}
""";
    }

    private sealed class GeminiResult
    {
        public string Explanation { get; init; }
            = string.Empty;

        public string? SuggestedCategory { get; init; }
    }
}