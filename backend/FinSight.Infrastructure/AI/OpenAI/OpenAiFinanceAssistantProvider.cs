using System.Text.Json;
using FinSight.Application.AI;
using OpenAI.Chat;

namespace FinSight.Infrastructure.AI.OpenAI;

public sealed class OpenAiFinanceAssistantProvider
    : IFinanceAssistantProvider
{
    private readonly string _apiKey;
    private readonly string _model;
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

    public OpenAiFinanceAssistantProvider(
        string apiKey,
        string model)
    {
        _apiKey = apiKey ?? string.Empty;
        _model = model ?? string.Empty;

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

                    return new ChatClient(
                        _model,
                        new System.ClientModel.ApiKeyCredential(
                            _apiKey));
                });
    }

    public string ProviderName =>
        "OpenAI";

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
            options.ToolChoice =
                ChatToolChoice.CreateNoneChoice();
        }

        var client = _client.Value;

        if (client is null)
        {
            throw new InvalidOperationException(
                "OpenAI Finance Assistant provider is not configured.");
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
                "OpenAI returned an empty Finance Assistant response.");
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
