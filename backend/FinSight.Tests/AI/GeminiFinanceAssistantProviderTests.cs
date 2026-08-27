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
    public async Task AskAsync_WithPreviousToolResults_SendsThreeConversationTurns()
    {
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

        var response =
            await provider.AskAsync(
                new FinanceAssistantProviderRequest
                {
                    RunId = runId,
                    Question =
                        "Summarize the reconciliation.",
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

        Assert.That(
            response.Answer,
            Is.EqualTo(
                "The reconciliation has 95 matched records."));

        Assert.That(
            fakeClient.LastContents,
            Has.Count.EqualTo(3));

        Assert.That(
            fakeClient.LastContents,
            Has.Count.EqualTo(3));

        var initialContent =
            fakeClient.LastContents[0];

        var modelContent =
            fakeClient.LastContents[1];

        var toolResultContent =
            fakeClient.LastContents[2];

        Assert.That(
            modelContent.Parts,
            Is.Not.Null);

        Assert.That(
            toolResultContent.Parts,
            Is.Not.Null);

        var modelPart =
            modelContent.Parts!.Single();

        var toolResultPart =
            toolResultContent.Parts!.Single();

        Assert.Multiple(() =>
        {
            Assert.That(
                initialContent.Role,
                Is.EqualTo("user"));

            Assert.That(
                modelContent.Role,
                Is.EqualTo("model"));

            Assert.That(
                toolResultContent.Role,
                Is.EqualTo("user"));

            Assert.That(
                modelPart.FunctionCall,
                Is.Not.Null);

            Assert.That(
                modelPart.FunctionCall!
                    .Name,
                Is.EqualTo(
                    "getReconciliationSummary"));

            Assert.That(
                toolResultPart.FunctionResponse,
                Is.Not.Null);

            Assert.That(
                toolResultPart.FunctionResponse!
                    .Id,
                Is.EqualTo("call-456"));

            Assert.That(
                toolResultPart.FunctionResponse!
                    .Name,
                Is.EqualTo(
                    "getReconciliationSummary"));
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

        public Task<GenerateContentResponse>
            GenerateContentAsync(
                string model,
                List<Content> contents,
                GenerateContentConfig config,
                CancellationToken cancellationToken = default)
        {
            Calls++;
            LastContents = contents;

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
