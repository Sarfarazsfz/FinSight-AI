using System.Text.Json;
using FinSight.Application.AI;
using FinSight.Infrastructure.AI.Gemini;
using Google.GenAI.Types;

namespace FinSight.Tests.AI;

[TestFixture]
public sealed class GeminiFinanceAssistantProviderTests
{
    [Test]
    public async Task AskAsync_WithTextResponse_ReturnsAnswer()
    {
        var fakeClient =
            new FakeFinanceAssistantModelClient(
                CreateTextResponse(
                    "Reconciliation is complete."));

        var provider =
            new GeminiFinanceAssistantProvider(
                fakeClient,
                "gemini-2.5-flash");

        var response =
            await provider.AskAsync(
                new FinanceAssistantProviderRequest
                {
                    RunId = Guid.NewGuid(),
                    Question = "Give me a status update."
                });

        Assert.Multiple(() =>
        {
            Assert.That(
                response.RequiresToolExecution,
                Is.False);

            Assert.That(
                response.Answer,
                Is.EqualTo(
                    "Reconciliation is complete."));

            Assert.That(
                response.ToolCalls,
                Is.Empty);

            Assert.That(
                fakeClient.Calls,
                Is.EqualTo(1));
        });
    }

    [Test]
    public async Task AskAsync_WithFunctionCall_ReturnsNameIdAndArguments()
    {
        var runId = Guid.NewGuid();

        var fakeClient =
            new FakeFinanceAssistantModelClient(
                CreateFunctionCallResponse(
                    "getReconciliationSummary",
                    "call-123",
                    new Dictionary<string, object>
                    {
                        ["runId"] = runId.ToString()
                    }));

        var provider =
            new GeminiFinanceAssistantProvider(
                fakeClient,
                "gemini-2.5-flash");

        var response =
            await provider.AskAsync(
                new FinanceAssistantProviderRequest
                {
                    RunId = runId,
                    Question =
                        "Summarize this reconciliation."
                });

        Assert.That(
            response.RequiresToolExecution,
            Is.True);

        Assert.That(
            response.ToolCalls,
            Has.Count.EqualTo(1));

        var call =
            response.ToolCalls.Single();

        Assert.Multiple(() =>
        {
            Assert.That(
                call.Id,
                Is.EqualTo("call-123"));

            Assert.That(
                call.Name,
                Is.EqualTo(
                    "getReconciliationSummary"));

            Assert.That(
                call.Arguments,
                Does.ContainKey("runId"));

            Assert.That(
                call.Arguments["runId"].GetString(),
                Is.EqualTo(runId.ToString()));
        });
    }

    [Test]
    public async Task AskAsync_DuringFinalSynthesis_SendsOnlyOneCleanUserTurn_NoConversationReplay()
    {
        // Regression test for the live second-question bug: this used to
        // assert a 3-turn replay (user + model/functionCall +
        // user/functionResponse). That replay is exactly what a live run
        // proved still prompted Gemini to continue calling tools even with
        // FunctionCallingConfigMode.None set. FinanceAssistantService
        // already flattens the tool result into the text passed as
        // `Question` -- the provider must not additionally reconstruct a
        // native function-call/function-response conversation on top of
        // it. Exactly one turn, and no FunctionCall/FunctionResponse part
        // anywhere, proves that.
        var runId = Guid.NewGuid();

        var fakeClient =
            new FakeFinanceAssistantModelClient(
                CreateTextResponse(
                    "The reconciliation has 95 matched records."));

        var provider =
            new GeminiFinanceAssistantProvider(
                fakeClient,
                "gemini-2.5-flash");

        var previousToolCall =
            new FinanceToolCall
            {
                Id = "call-456",
                Name =
                    "getReconciliationSummary",
                Arguments =
                    new Dictionary<string, JsonElement>
                    {
                        ["runId"] =
                            JsonSerializer.Deserialize<JsonElement>(
                                $"\"{runId}\"")
                    }
            };

        var toolResult =
            new FinanceToolResultMessage
            {
                ToolCallId = "call-456",
                ToolName =
                    "getReconciliationSummary",
                Success = true,
                ResultJson =
                    """{"matched":95,"mismatched":5}"""
            };

        // Mirrors what FinanceAssistantService actually sends as
        // `Question` for the final call: the original question plus the
        // tool result already flattened into text.
        var flattenedQuestion =
            """
            User question:
            Summarize the reconciliation.

            Use ONLY the following authoritative backend evidence:
            [{"tool":"getReconciliationSummary","success":true,"result":"{\"matched\":95,\"mismatched\":5}","errorCode":null}]
            """;

        var response =
            await provider.AskAsync(
                new FinanceAssistantProviderRequest
                {
                    RunId = runId,
                    Question = flattenedQuestion,
                    Tools = Array.Empty<FinanceToolDefinition>(),
                    PreviousToolCalls =
                        new[]
                        {
                            previousToolCall
                        },
                    ToolResults =
                        new[]
                        {
                            toolResult
                        }
                });

        Assert.Multiple(() =>
        {
            Assert.That(
                response.Answer,
                Is.EqualTo(
                    "The reconciliation has 95 matched records."));

            Assert.That(
                response.RequiresToolExecution,
                Is.False);

            // Exactly one turn -- no replayed model/functionCall or
            // user/functionResponse turns.
            Assert.That(
                fakeClient.LastContents,
                Has.Count.EqualTo(1));

            var onlyContent =
                fakeClient.LastContents[0];

            Assert.That(onlyContent.Role, Is.EqualTo("user"));

            // No part anywhere in the sent contents represents a
            // function call or function response -- the model receives
            // the grounded evidence as plain text only.
            var allParts =
                fakeClient.LastContents
                    .SelectMany(c => c.Parts ?? new List<Part>())
                    .ToList();

            Assert.That(
                allParts.All(p => p.FunctionCall is null),
                Is.True);

            Assert.That(
                allParts.All(p => p.FunctionResponse is null),
                Is.True);

            // The flattened evidence text actually reached the model.
            Assert.That(
                onlyContent.Parts!.Single().Text,
                Does.Contain("Summarize the reconciliation."));

            Assert.That(
                onlyContent.Parts!.Single().Text,
                Does.Contain("matched"));
        });
    }

    [Test]
    public async Task AskAsync_WithToolsDeclared_SendsFunctionDeclarationsInValidatedMode()
    {
        var fakeClient =
            new FakeFinanceAssistantModelClient(
                CreateTextResponse("unused"));

        var provider =
            new GeminiFinanceAssistantProvider(
                fakeClient,
                "gemini-2.5-flash");

        await provider.AskAsync(
            new FinanceAssistantProviderRequest
            {
                RunId = Guid.NewGuid(),
                Question = "Summarize this run.",
                Tools = new[]
                {
                    new FinanceToolDefinition
                    {
                        Name = "getReconciliationSummary",
                        Description = "Returns the authoritative summary of a run.",
                        Parameters = new Dictionary<string, FinanceToolParameter>
                        {
                            ["runId"] = new()
                            {
                                Type = "string",
                                Description = "Reconciliation run GUID.",
                                Required = true
                            }
                        }
                    }
                }
            });

        Assert.Multiple(() =>
        {
            Assert.That(fakeClient.LastConfig.Tools, Is.Not.Null);
            Assert.That(fakeClient.LastConfig.Tools!, Has.Count.EqualTo(1));

            var declaration =
                fakeClient.LastConfig.Tools![0].FunctionDeclarations!.Single();

            Assert.That(declaration.Name, Is.EqualTo("getReconciliationSummary"));

            Assert.That(
                fakeClient.LastConfig.ToolConfig,
                Is.Not.Null);

            Assert.That(
                fakeClient.LastConfig.ToolConfig!.FunctionCallingConfig!.Mode,
                Is.EqualTo(FunctionCallingConfigMode.Validated));
        });
    }

    [Test]
    public async Task AskAsync_DuringFinalSynthesis_ExplicitlyDisablesFunctionCallingAndParsesTextNormally()
    {
        // Regression test for the live bug: the FIRST turn's function-call/
        // function-response history is still replayed into `contents`
        // (asserted below and in SendsThreeConversationTurns), so merely
        // omitting Tools/ToolConfig on this second call was not sufficient
        // -- Gemini could still attempt another function call. The fix
        // must set FunctionCallingConfigMode.None explicitly.
        var runId = Guid.NewGuid();

        var fakeClient =
            new FakeFinanceAssistantModelClient(
                CreateTextResponse("The reconciliation has 95 matched records."));

        var provider =
            new GeminiFinanceAssistantProvider(
                fakeClient,
                "gemini-2.5-flash");

        var previousToolCall =
            new FinanceToolCall
            {
                Id = "call-456",
                Name = "getReconciliationSummary",
                Arguments = new Dictionary<string, JsonElement>
                {
                    ["runId"] =
                        JsonSerializer.Deserialize<JsonElement>($"\"{runId}\"")
                }
            };

        var toolResult =
            new FinanceToolResultMessage
            {
                ToolCallId = "call-456",
                ToolName = "getReconciliationSummary",
                Success = true,
                ResultJson = """{"matched":95,"mismatched":5}"""
            };

        var response =
            await provider.AskAsync(
                new FinanceAssistantProviderRequest
                {
                    RunId = runId,
                    Question = "Summarize the reconciliation.",
                    Tools = Array.Empty<FinanceToolDefinition>(),
                    PreviousToolCalls = new[] { previousToolCall },
                    ToolResults = new[] { toolResult }
                });

        Assert.Multiple(() =>
        {
            // The provider-level config actually sent must explicitly
            // disable function calling -- not merely omit Tools.
            Assert.That(
                fakeClient.LastConfig.Tools,
                Is.Null.Or.Empty);

            Assert.That(
                fakeClient.LastConfig.ToolConfig,
                Is.Not.Null);

            Assert.That(
                fakeClient.LastConfig.ToolConfig!.FunctionCallingConfig,
                Is.Not.Null);

            Assert.That(
                fakeClient.LastConfig.ToolConfig!.FunctionCallingConfig!.Mode,
                Is.EqualTo(FunctionCallingConfigMode.None));

            // Normal text response still parses correctly under the fix.
            Assert.That(response.RequiresToolExecution, Is.False);
            Assert.That(
                response.Answer,
                Is.EqualTo("The reconciliation has 95 matched records."));
        });
    }

    [Test]
    public async Task AskAsync_WhenModelStillReturnsFunctionCallDuringFinalSynthesis_ProviderStillReportsIt()
    {
        // Defense-in-depth: even with Mode=None now set, the provider must
        // faithfully report a function-call response rather than silently
        // discarding or misinterpreting it -- this is what lets
        // FinanceAssistantService's own safety guard actually catch a
        // genuine SDK/model misbehavior instead of fabricating an answer.
        var fakeClient =
            new FakeFinanceAssistantModelClient(
                CreateFunctionCallResponse(
                    "getReconciliationSummary",
                    "call-999",
                    new Dictionary<string, object>
                    {
                        ["runId"] = Guid.NewGuid().ToString()
                    }));

        var provider =
            new GeminiFinanceAssistantProvider(
                fakeClient,
                "gemini-2.5-flash");

        var response =
            await provider.AskAsync(
                new FinanceAssistantProviderRequest
                {
                    RunId = Guid.NewGuid(),
                    Question = "Summarize the reconciliation.",
                    Tools = Array.Empty<FinanceToolDefinition>()
                });

        Assert.Multiple(() =>
        {
            Assert.That(
                fakeClient.LastConfig.ToolConfig!.FunctionCallingConfig!.Mode,
                Is.EqualTo(FunctionCallingConfigMode.None));

            Assert.That(response.RequiresToolExecution, Is.True);
            Assert.That(response.ToolCalls, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void AskAsync_WithEmptyQuestion_Throws()
    {
        var provider =
            new GeminiFinanceAssistantProvider(
                new FakeFinanceAssistantModelClient(
                    CreateTextResponse("unused")),
                "gemini-2.5-flash");

        Assert.ThrowsAsync<ArgumentException>(
            async () =>
                await provider.AskAsync(
                    new FinanceAssistantProviderRequest
                    {
                        RunId = Guid.NewGuid(),
                        Question = "   "
                    }));
    }

    [Test]
    public void AskAsync_WithMismatchedToolResults_Throws()
    {
        var provider =
            new GeminiFinanceAssistantProvider(
                new FakeFinanceAssistantModelClient(
                    CreateTextResponse("unused")),
                "gemini-2.5-flash");

        Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await provider.AskAsync(
                    new FinanceAssistantProviderRequest
                    {
                        RunId = Guid.NewGuid(),
                        Question = "Summarize.",
                        PreviousToolCalls =
                            new[]
                            {
                                new FinanceToolCall
                                {
                                    Id = "call-1",
                                    Name =
                                        "getReconciliationSummary"
                                }
                            },
                        ToolResults =
                            Array.Empty<
                                FinanceToolResultMessage>()
                    }));
    }

    private static GenerateContentResponse
        CreateTextResponse(string text)
    {
        var json =
            JsonSerializer.Serialize(
                new
                {
                    candidates = new[]
                    {
                        new
                        {
                            content = new
                            {
                                role = "model",
                                parts = new[]
                                {
                                    new
                                    {
                                        text
                                    }
                                }
                            }
                        }
                    }
                });

        var response =
            GenerateContentResponse.FromJson(json);

        if (response is null)
        {
            throw new InvalidOperationException(
                "Unable to create fake Gemini text response.");
        }

        return response;
    }

    private static GenerateContentResponse
        CreateFunctionCallResponse(
            string name,
            string id,
            Dictionary<string, object> arguments)
    {
        var json =
            JsonSerializer.Serialize(
                new
                {
                    candidates = new[]
                    {
                        new
                        {
                            content = new
                            {
                                role = "model",
                                parts = new[]
                                {
                                    new
                                    {
                                        functionCall =
                                            new
                                            {
                                                id,
                                                name,
                                                args = arguments
                                            }
                                    }
                                }
                            }
                        }
                    }
                });

        var response =
            GenerateContentResponse.FromJson(json);

        if (response is null)
        {
            throw new InvalidOperationException(
                "Unable to create fake Gemini function-call response.");
        }

        return response;
    }

    private sealed class FakeFinanceAssistantModelClient
        : IFinanceAssistantModelClient
    {
        private readonly Queue<
            GenerateContentResponse> _responses;

        public FakeFinanceAssistantModelClient(
            params GenerateContentResponse[] responses)
        {
            _responses =
                new Queue<
                    GenerateContentResponse>(
                    responses);
        }

        public int Calls { get; private set; }

        public List<Content> LastContents { get; private set; }
            = new();

        public GenerateContentConfig LastConfig { get; private set; } = null!;

        public Task<GenerateContentResponse>
            GenerateContentAsync(
                string model,
                List<Content> contents,
                GenerateContentConfig config,
                CancellationToken cancellationToken = default)
        {
            Calls++;
            LastContents = contents;
            LastConfig = config;

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException(
                    "No fake Gemini response configured.");
            }

            return Task.FromResult(
                _responses.Dequeue());
        }
    }
}
