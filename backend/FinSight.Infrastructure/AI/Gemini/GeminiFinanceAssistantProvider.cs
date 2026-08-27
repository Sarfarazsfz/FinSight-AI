using System.Text.Json;
using FinSight.Application.AI;
using Google.GenAI.Types;

namespace FinSight.Infrastructure.AI.Gemini;

public sealed class GeminiFinanceAssistantProvider
    : IFinanceAssistantProvider
{
    private readonly IFinanceAssistantModelClient _modelClient;
    private readonly string _model;

    public GeminiFinanceAssistantProvider(
        IFinanceAssistantModelClient modelClient,
        string model)
    {
        _modelClient = modelClient;
        _model = model;
    }

    public string ProviderName =>
        "Gemini";

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

        GenerateContentConfig config;

        if (request.Tools.Count == 0)
        {
            // Final synthesis turn:
            // no tools are exposed so Gemini must produce
            // a final natural-language answer from the
            // conversation and collected tool results.
            config =
                new GenerateContentConfig();
        }
        else
        {
            var declarations =
                request.Tools
                    .Select(CreateFunctionDeclaration)
                    .ToList();

            var tool =
                new Tool
                {
                    FunctionDeclarations =
                        declarations
                };

            config =
                new GenerateContentConfig
                {
                    Tools =
                        new List<Tool>
                        {
                            tool
                        },
                    ToolConfig =
                        new ToolConfig
                        {
                            FunctionCallingConfig =
                                new FunctionCallingConfig
                                {
                                    Mode =
                                        FunctionCallingConfigMode.Validated
                                }
                        }
                };
        }

        var contents =
            new List<Content>();

        // First user turn.
        // The authoritative reconciliation run ID must be explicitly
        // provided to Gemini so it does not invent a value such as "latest".
        var userPrompt =
            request.Tools.Count == 0
                ? $"""
                You are now in the final answer phase.

                Reconciliation Run ID:
                {request.RunId}

                Do not call tools.
                Do not request any tool.
                Do not invent tool names.
                Use only the authoritative evidence included
                in this prompt.

                {request.Question}
                """
                : $"""
                You are analyzing reconciliation data for exactly this run:

                Reconciliation Run ID:
                {request.RunId}

                Use this exact run ID whenever a tool requires a runId.
                Do not invent, substitute, or use values such as "latest".

                You may use ONLY these tools:
                - getReconciliationSummary
                - getUnmatchedRecords
                - getTransactionDetails
                - getExceptionDetails

                Do not invent or request any other tool names.

                User question:
                {request.Question}
                """;

        contents.Add(
            new Content
            {
                Role = "user",
                Parts =
                    new List<Part>
                    {
                        Part.FromText(userPrompt)
                    }
            });

        // If this is the second provider call, reproduce
        // the model's previous function-call turn.
        if (request.PreviousToolCalls.Count > 0)
        {
            var previousFunctionCallParts =
                request.PreviousToolCalls
                    .Select(
                        call =>
                        {
                            if (!string.IsNullOrWhiteSpace(
                                    call.ModelPartJson))
                            {
                                var originalPart =
                                    Part.FromJson(
                                        call.ModelPartJson);

                                if (originalPart is null)
                                {
                                    throw new InvalidOperationException(
                                        $"Unable to restore original Gemini " +
                                        $"model part for tool '{call.Name}'.");
                                }

                                return originalPart;
                            }

                            return
                                Part.FromFunctionCall(
                                    call.Name,
                                    call.Arguments.ToDictionary(
                                        x => x.Key,
                                        x => ConvertJsonElement(x.Value)));
                        })
                    .ToList();

            contents.Add(
                new Content
                {
                    Role = "model",
                    Parts = previousFunctionCallParts
                });

            if (request.ToolResults.Count !=
                request.PreviousToolCalls.Count)
            {
                throw new InvalidOperationException(
                    "The number of tool results must match " +
                    "the number of previous tool calls.");
            }

            var functionResponseParts =
                request.ToolResults
                    .Select(
                        result =>
                            new Part
                            {
                                FunctionResponse =
                                    CreateFunctionResponse(
                                        result)
                            })
                    .ToList();

            contents.Add(
                new Content
                {
                    Role = "user",
                    Parts = functionResponseParts
                });
        }

        var response =
            await _modelClient.GenerateContentAsync(
                _model,
                contents,
                config,
                cancellationToken);

var functionCallParts =
            response.Candidates?
                .SelectMany(
                    candidate =>
                        candidate.Content?.Parts
                        ?? new List<Part>())
                .Where(
                    part =>
                        part.FunctionCall is not null &&
                        !string.IsNullOrWhiteSpace(
                            part.FunctionCall.Name))
                .ToList()
                ?? new List<Part>();

        if (functionCallParts.Count > 0)
        {
            var calls =
                functionCallParts
                    .Select(
                        part =>
                        {
                            var functionCall =
                                part.FunctionCall!;

                            return new FinanceToolCall
                            {
                                Id = functionCall.Id,
                                Name = functionCall.Name!,
                                Arguments =
                                    ConvertArguments(
                                        functionCall.Args),
                                ModelPartJson =
                                    JsonSerializer.Serialize(part)
                            };
                        })
                    .ToList();

            return new FinanceAssistantProviderResponse
            {
                Answer = string.Empty,
                ToolCalls = calls,
                RequiresToolExecution =
                    calls.Count > 0
            };
        }

        return new FinanceAssistantProviderResponse
        {
            Answer =
                response.Text ?? string.Empty,
            ToolCalls =
                Array.Empty<FinanceToolCall>(),
            RequiresToolExecution = false
        };
    }

    private static object ConvertJsonElement(
        JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String =>
                element.GetString() ?? string.Empty,

            JsonValueKind.Number =>
                element.TryGetInt64(out var integer)
                    ? integer
                    : element.GetDecimal(),

            JsonValueKind.True =>
                true,

            JsonValueKind.False =>
                false,

            JsonValueKind.Null =>
                string.Empty,

            JsonValueKind.Array =>
                element.EnumerateArray()
                    .Select(ConvertJsonElement)
                    .ToList(),

            JsonValueKind.Object =>
                element.EnumerateObject()
                    .ToDictionary(
                        x => x.Name,
                        x => ConvertJsonElement(x.Value)),

            _ =>
                element.GetRawText()
        };
    }

    private static FunctionResponse CreateFunctionResponse(
        FinanceToolResultMessage result)
    {
        var responseObject =
            new Dictionary<string, object>();

        if (result.Success)
        {
            responseObject["output"] =
                ParseResultJson(result.ResultJson);
        }
        else
        {
            responseObject["error"] =
                new
                {
                    code =
                        result.ErrorCode ?? "TOOL_ERROR",
                    message =
                        ParseResultJson(result.ResultJson)
                };
        }

        var json =
            JsonSerializer.Serialize(
                new
                {
                    id = result.ToolCallId,
                    name = result.ToolName,
                    response = responseObject
                });

        var functionResponse =
            FunctionResponse.FromJson(json);

        if (functionResponse is null)
        {
            throw new InvalidOperationException(
                $"Unable to create Gemini function response " +
                $"for tool '{result.ToolName}'.");
        }

        return functionResponse;
    }

    private static object ParseResultJson(
        string resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
        {
            return new Dictionary<string, object>();
        }

        using var document =
            JsonDocument.Parse(resultJson);

        return ConvertJsonElement(
            document.RootElement);
    }
    private static FunctionDeclaration
        CreateFunctionDeclaration(
            FinanceToolDefinition definition)
    {
        var properties =
            definition.Parameters.ToDictionary(
                x => x.Key,
                x => new
                {
                    type = x.Value.Type,
                    description = x.Value.Description
                });

        var required =
            definition.Parameters
                .Where(x => x.Value.Required)
                .Select(x => x.Key)
                .ToArray();

        var json =
            JsonSerializer.Serialize(
                new
                {
                    name = definition.Name,
                    description = definition.Description,
                    parameters = new
                    {
                        type = "object",
                        properties,
                        required
                    }
                });

        var declaration =
            FunctionDeclaration.FromJson(
                json);

        if (declaration is null)
        {
            throw new InvalidOperationException(
                $"Unable to create Gemini function declaration " +
                $"for '{definition.Name}'.");
        }

        return declaration;
    }

    private static IReadOnlyDictionary<
        string,
        JsonElement> ConvertArguments(
            Dictionary<string, object>? arguments)
    {
        if (arguments is null ||
            arguments.Count == 0)
        {
            return
                new Dictionary<
                    string,
                    JsonElement>();
        }

        var json =
            JsonSerializer.Serialize(arguments);

        var document =
            JsonDocument.Parse(json);

        return document.RootElement
            .EnumerateObject()
            .ToDictionary(
                x => x.Name,
                x => x.Value.Clone());
    }
}
