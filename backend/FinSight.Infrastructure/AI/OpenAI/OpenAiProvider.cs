using System.ClientModel;
using System.Text.Json;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Ai;
using OpenAI.Chat;

namespace FinSight.Infrastructure.AI.OpenAI;

public sealed class OpenAiProvider : IOpenAiProvider
{
    private readonly ChatClient _client;

    public OpenAiProvider(
        string apiKey,
        string model)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException(
                "OpenAI API key is required.",
                nameof(apiKey));
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException(
                "OpenAI model is required.",
                nameof(model));
        }

        _client = new ChatClient(
            model,
            new ApiKeyCredential(apiKey));
    }

    public string ProviderName => "OpenAI";

    public bool IsAvailable => true;

    public async Task<AiExplanationResponse>
        GenerateExplanationAsync(
            AiExplanationRequest request,
            CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var prompt = BuildPrompt(request);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(
                "You are a financial reconciliation explanation assistant. " +
                "The deterministic reconciliation engine is authoritative. " +
                "Never override its category. " +
                "Use only the supplied facts."),

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
            await _client.CompleteChatAsync(
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
                "OpenAI returned an empty response.");
        }

        var parsed =
            JsonSerializer.Deserialize<OpenAiResult>(
                text,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

        if (parsed is null ||
            string.IsNullOrWhiteSpace(
                parsed.Explanation))
        {
            throw new InvalidOperationException(
                "OpenAI returned an invalid explanation response.");
        }

        return new AiExplanationResponse
        {
            Provider = ProviderName,

            Explanation =
                parsed.Explanation.Trim(),

            SuggestedCategory =
                string.IsNullOrWhiteSpace(
                    parsed.SuggestedCategory)
                    ? null
                    : parsed.SuggestedCategory.Trim(),

            GeneratedAtUtc =
                DateTime.UtcNow
        };
    }

    private static string BuildPrompt(
        AiExplanationRequest request)
    {
        return $"""
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

    private sealed class OpenAiResult
    {
        public string Explanation { get; init; }
            = string.Empty;

        public string? SuggestedCategory { get; init; }
    }
}