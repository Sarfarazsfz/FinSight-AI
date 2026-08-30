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
            // Final synthesis turn. Omitting Tools/ToolConfig entirely
            // (the prior implementation) is NOT sufficient here: `contents`
            // below still replays the FIRST turn's function-call/
            // function-response history (model + user turns), and a live
            // run proved Gemini can still emit another function call in
            // that context even with no tool declarations offered this
            // turn -- surfacing as FinanceAssistantService's
            // "Gemini attempted a tool call during final synthesis" safety
            // exception. FunctionCallingConfigMode.None is the SDK's own
            // explicit, documented way to force no function-call
            // predictions regardless of conversation history; ToolConfig
            // does not require Tools to be set (confirmed from the
            // Google.GenAI package's own XML docs). Verified by
            // GeminiFinanceAssistantProviderTests -- do not revert to an
            // empty config on the assumption that omission alone is
            // equivalent.
            config =
                new GenerateContentConfig
                {
                    ToolConfig =
                        new ToolConfig
                        {
                            FunctionCallingConfig =
                                new FunctionCallingConfig
                                {
                                    Mode =
                                        FunctionCallingConfigMode.None
                                }
                        }
                };
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

        // Final synthesis deliberately sends a SINGLE clean user turn (the
        // userPrompt above) rather than replaying the first call's raw
        // function-call/function-response conversation as native Gemini
        // turns. FinanceAssistantService already flattens every tool
        // result into the plain-text evidence block embedded in
        // `request.Question` (see `finalQuestion` in
        // FinanceAssistantService.ExecuteAsync) -- replaying the same
        // evidence a second time as structured model/user turns was
        // redundant, and a live run (a second question, after a first one
        // succeeded) proved that ending the conversation on a
        // function-response turn still prompted Gemini to continue the
        // tool-calling pattern, even with FunctionCallingConfigMode.None
        // set on this exact call. OpenAiFinanceAssistantProvider already
        // takes this no-replay approach for its own final synthesis --
        // this brings Gemini into parity with an already-correct sibling,
        // not a new invented design.
        //
        // The structural invariant (every prior tool call got exactly one
        // result) is still enforced even though the history itself is no
        // longer replayed to the model.
        if (request.PreviousToolCalls.Count !=
            request.ToolResults.Count)
        {
            throw new InvalidOperationException(
                "The number of tool results must match " +
                "the number of previous tool calls.");
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
