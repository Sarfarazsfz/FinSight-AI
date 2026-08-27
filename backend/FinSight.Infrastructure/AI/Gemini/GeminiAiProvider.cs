using System.Text.Json;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Ai;
using Google.GenAI;
using Google.GenAI.Types;

namespace FinSight.Infrastructure.AI.Gemini;

public sealed class GeminiAiProvider : IGeminiAiProvider
{
    private readonly Client _client;
    private readonly string _model;

    public GeminiAiProvider(
        string apiKey,
        string model)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException(
                "Gemini API key is required.",
                nameof(apiKey));
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException(
                "Gemini model is required.",
                nameof(model));
        }

        _client = new Client(apiKey: apiKey);
        _model = model;
    }

    public string ProviderName => "Gemini";

    public bool IsAvailable => true;

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
            await _client.Models.GenerateContentAsync(
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