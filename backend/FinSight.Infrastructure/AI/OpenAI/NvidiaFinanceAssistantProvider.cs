using System.ClientModel;
using System.Text.Json;
using FinSight.Application.AI;
using OpenAI;
using OpenAI.Chat;

namespace FinSight.Infrastructure.AI.OpenAI;

/// <summary>
/// Finance Assistant provider for NVIDIA's hosted, OpenAI-compatible Chat
/// Completions endpoint (integrate.api.nvidia.com/v1), serving models such
/// as openai/gpt-oss-120b. Deliberately a standalone class, not a shared
/// base with OpenAiFinanceAssistantProvider (Option A from the F10 NVIDIA
/// audit) -- the two are independent so neither can be broken by a change
/// aimed at the other, at the accepted cost of duplicated logic. Uses the
/// same installed `OpenAI` SDK package (2.13.0) as OpenAiFinanceAssistantProvider,
/// pointed at a different endpoint via OpenAIClientOptions.Endpoint -- a
/// first-class, SDK-documented mechanism for OpenAI-compatible third-party
/// APIs, not an unsupported hack.
/// </summary>
public sealed class NvidiaFinanceAssistantProvider
    : IFinanceAssistantProvider
{
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _baseUrl;
    private readonly Lazy<ChatClient?> _client;

    private const string SystemPrompt =
        """
        You are FinSight AI, a financial reconciliation assistant.

        The deterministic reconciliation engine and backend tool
        results are authoritative.

        Never invent reconciliation data.
        Never change deterministic categories.
        Use only the supplied evidence.

        The application exposes only these tools:
        getReconciliationSummary
        getUnmatchedRecords
        getTransactionDetails
        getExceptionDetails
        """;

    public NvidiaFinanceAssistantProvider(
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
                    if (string.IsNullOrWhiteSpace(_apiKey))
                    {
                        return null;
                    }

                    if (string.IsNullOrWhiteSpace(_model))
                    {
                        return null;
                    }

                    if (string.IsNullOrWhiteSpace(_baseUrl) ||
                        !Uri.TryCreate(_baseUrl, UriKind.Absolute, out var endpoint))
                    {
                        return null;
                    }

                    var options =
                        new OpenAIClientOptions
                        {
                            Endpoint = endpoint
                        };

                    return new ChatClient(
                        _model,
                        new ApiKeyCredential(_apiKey),
                        options);
                });
    }

    public string ProviderName =>
        "NVIDIA";

    public async Task<FinanceAssistantProviderResponse> AskAsync(
        FinanceAssistantProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            throw new ArgumentException(
                "Question is required.",
                nameof(request));
        }

        var messages =
            new List<ChatMessage>
            {
                new SystemChatMessage(
                    SystemPrompt)
            };

        var hasTools =
            request.Tools.Count > 0;

        if (hasTools)
        {
            var prompt =
                $"""
                Reconciliation Run ID:
                {request.RunId}

                User question:
                {request.Question}

                Use the available authoritative tools when
                necessary. Always use the exact Run ID above.
                """;

            messages.Add(
                new UserChatMessage(prompt));
        }
        else
        {
            var evidence =
                request.ToolResults
                    .Select(
                        result =>
                            new
                            {
                                tool =
                                    result.ToolName,

                                success =
                                    result.Success,

                                result =
                                    result.ResultJson,

                                errorCode =
                                    result.ErrorCode
                            });

            var prompt =
                $"""
                FINAL SYNTHESIS.

                Reconciliation Run ID:
                {request.RunId}

                User question:
                {request.Question}

                Do not call tools.
                Use only the authoritative evidence below.

                Evidence:
                {JsonSerializer.Serialize(
                    evidence,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    })}

                Provide a clear final answer containing:
                - main reconciliation issues,
                - affected record counts,
                - most important exceptions,
                - important observations.

                Do not invent unsupported facts.
                """;

            messages.Add(
                new UserChatMessage(prompt));
        }

        var options =
            new ChatCompletionOptions();

        if (hasTools)
        {
            options.ToolChoice =
                ChatToolChoice.CreateAutoChoice();

            options.AllowParallelToolCalls =
                true;

            foreach (var toolDefinition in request.Tools)
            {
                options.Tools.Add(
                    CreateTool(toolDefinition));
            }
        }
        else
        {
            // Final synthesis: explicitly disable tool calling via the
            // SDK's own "none" choice rather than relying on an empty
            // Tools collection alone -- the same defensive pattern used
            // for Gemini's final-synthesis turn and already proven
            // correct for OpenAiFinanceAssistantProvider.
            options.ToolChoice =
                ChatToolChoice.CreateNoneChoice();
        }

        var client = _client.Value;

        if (client is null)
        {
            throw new InvalidOperationException(
                "NVIDIA Finance Assistant provider is not configured.");
        }

        var result =
            await client.CompleteChatAsync(
                messages,
                options,
                cancellationToken);

        var completion =
            result.Value;

        if (completion.ToolCalls.Count > 0)
        {
            var calls =
                completion.ToolCalls
                    .Where(
                        call =>
                            call.Kind ==
                            ChatToolCallKind.Function)
                    .Select(
                        call =>
                            new FinanceToolCall
                            {
                                Id = call.Id,

                                Name =
                                    call.FunctionName,

                                Arguments =
                                    ParseArguments(
                                        call.FunctionArguments.ToString())
                            })
                    .ToList();

            return new FinanceAssistantProviderResponse
            {
                Answer =
                    string.Empty,

                ToolCalls =
                    calls,

                RequiresToolExecution =
                    calls.Count > 0
            };
        }

        var answer =
            completion.Content
                .FirstOrDefault()?
                .Text;

        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new InvalidOperationException(
                "NVIDIA returned an empty Finance Assistant response.");
        }

        return new FinanceAssistantProviderResponse
        {
            Answer =
                answer.Trim(),

            ToolCalls =
                Array.Empty<FinanceToolCall>(),

            RequiresToolExecution =
                false
        };
    }

    private static ChatTool CreateTool(
        FinanceToolDefinition definition)
    {
        var properties =
            definition.Parameters.ToDictionary(
                pair =>
                    pair.Key,
                pair =>
                    new
                    {
                        type =
                            pair.Value.Type,

                        description =
                            pair.Value.Description
                    });

        var required =
            definition.Parameters
                .Where(
                    pair =>
                        pair.Value.Required)
                .Select(
                    pair =>
                        pair.Key)
                .ToArray();

        var schema =
            JsonSerializer.Serialize(
                new
                {
                    type = "object",
                    properties,
                    required,
                    additionalProperties = false
                });

        return ChatTool.CreateFunctionTool(
            definition.Name,
            definition.Description,
            BinaryData.FromString(schema),
            true);
    }

    private static IReadOnlyDictionary<
        string,
        JsonElement> ParseArguments(
            string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return new Dictionary<
                string,
                JsonElement>();
        }

        using var document =
            JsonDocument.Parse(arguments);

        return document.RootElement
            .EnumerateObject()
            .ToDictionary(
                property =>
                    property.Name,
                property =>
                    property.Value.Clone());
    }
}
