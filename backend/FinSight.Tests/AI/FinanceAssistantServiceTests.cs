using System.Text.Json;
using FinSight.Application.AI;

namespace FinSight.Tests.AI;

[TestFixture]
public sealed class FinanceAssistantServiceTests
{
    [Test]
    public async Task AskAsync_WhenProviderReturnsText_DoesNotExecuteTools()
    {
        var provider =
            new FakeFinanceAssistantProvider(
                new[]
                {
                    new FinanceAssistantProviderResponse
                    {
                        Answer = "No backend lookup was required.",
                        RequiresToolExecution = false
                    }
                });

        var registry =
            new FakeFinanceToolRegistry();

        var service =
            new FinanceAssistantService(
                provider,
                registry);

        var response =
            await service.AskAsync(
                new FinanceAssistantRequest
                {
                    RunId = Guid.NewGuid(),
                    Question = "What is a reconciliation run?"
                });

        Assert.Multiple(() =>
        {
            Assert.That(
                response.Answer,
                Is.EqualTo("No backend lookup was required."));

            Assert.That(
                response.ToolsUsed,
                Is.Empty);

            Assert.That(
                provider.Calls,
                Is.EqualTo(1));

            Assert.That(
                registry.LookupCalls,
                Is.EqualTo(0));
        });
    }

    [Test]
    public async Task AskAsync_WithValidToolCall_ExecutesToolAndReturnsFinalAnswer()
    {
        var runId = Guid.NewGuid();

        var tool =
            new FakeFinanceTool(
                "getReconciliationSummary",
                new FinanceToolResult
                {
                    ToolName = "getReconciliationSummary",
                    Success = true,
                    DataJson = """{"runId":"test","matched":70,"exceptionCount":30}"""
                });

        var registry =
            new FakeFinanceToolRegistry(
                tool);

        var firstCall =
            new FinanceToolCall
            {
                Name = "getReconciliationSummary",
                Arguments =
                    new Dictionary<string, JsonElement>
                    {
                        ["runId"] =
                            JsonSerializer.Deserialize<JsonElement>(
                                $"\"{runId}\"")
                    }
            };

        var provider =
            new FakeFinanceAssistantProvider(
                new[]
                {
                    new FinanceAssistantProviderResponse
                    {
                        RequiresToolExecution = true,
                        ToolCalls =
                            new[]
                            {
                                firstCall
                            }
                    },

                    new FinanceAssistantProviderResponse
                    {
                        Answer =
                            "The reconciliation has 70 matched records and 30 exceptions.",
                        RequiresToolExecution = false
                    }
                });

        var service =
            new FinanceAssistantService(
                provider,
                registry);

        var response =
            await service.AskAsync(
                new FinanceAssistantRequest
                {
                    RunId = runId,
                    Question = "Summarize the reconciliation."
                });

        Assert.Multiple(() =>
        {
            Assert.That(
                response.Answer,
                Is.EqualTo(
                    "The reconciliation has 70 matched records and 30 exceptions."));

            Assert.That(
                response.ToolsUsed,
                Is.EqualTo(
                    new[]
                    {
                        "getReconciliationSummary"
                    }));

            Assert.That(
                tool.Calls,
                Is.EqualTo(1));

            Assert.That(
                tool.LastRequest.RunId,
                Is.EqualTo(runId));

            Assert.That(
                provider.Calls,
                Is.EqualTo(2));
        });
    }

    [Test]
    public async Task AskAsync_WithUnknownTool_RejectsTool()
    {
        var provider =
            new FakeFinanceAssistantProvider(
                new[]
                {
                    new FinanceAssistantProviderResponse
                    {
                        RequiresToolExecution = true,
                        ToolCalls =
                            new[]
                            {
                                new FinanceToolCall
                                {
                                    Name = "deleteTransactions"
                                }
                            }
                    }
                });

        var registry =
            new FakeFinanceToolRegistry();

        var service =
            new FinanceAssistantService(
                provider,
                registry);

        Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await service.AskAsync(
                    new FinanceAssistantRequest
                    {
                        RunId = Guid.NewGuid(),
                        Question = "Delete transactions."
                    }));
    }

    [Test]
    public async Task AskAsync_WithMalformedRunId_ReturnsInvalidArgumentToProvider()
    {
        var provider =
            new FakeFinanceAssistantProvider(
                new[]
                {
                    new FinanceAssistantProviderResponse
                    {
                        RequiresToolExecution = true,
                        ToolCalls =
                            new[]
                            {
                                new FinanceToolCall
                                {
                                    Name =
                                        "getReconciliationSummary",
                                    Arguments =
                                        new Dictionary<
                                            string,
                                            JsonElement>
                                        {
                                            ["runId"] =
                                                JsonSerializer.Deserialize<JsonElement>(
                                                    "\"not-a-guid\"")
                                        }
                                }
                            }
                    },

                    new FinanceAssistantProviderResponse
                    {
                        Answer =
                            "I could not use the requested reconciliation data.",
                        RequiresToolExecution = false
                    }
                });

        var tool =
            new FakeFinanceTool(
                "getReconciliationSummary",
                new FinanceToolResult
                {
                    ToolName =
                        "getReconciliationSummary",
                    Success = true,
                    DataJson = """{"matched":70}"""
                });

        var registry =
            new FakeFinanceToolRegistry(
                tool);

        var service =
            new FinanceAssistantService(
                provider,
                registry);

        var response =
            await service.AskAsync(
                new FinanceAssistantRequest
                {
                    RunId = Guid.NewGuid(),
                    Question = "Summarize the run."
                });

        Assert.Multiple(() =>
        {
            Assert.That(
                response.Answer,
                Is.EqualTo(
                    "I could not use the requested reconciliation data."));

            Assert.That(
                tool.Calls,
                Is.EqualTo(0));

            Assert.That(
                provider.LastRequest.ToolResults,
                Has.Count.EqualTo(1));

            Assert.That(
                provider.LastRequest.ToolResults
                    .Single()
                    .ErrorCode,
                Is.EqualTo("INVALID_ARGUMENT"));
        });
    }

    [Test]
    public void AskAsync_WithEmptyRunId_Throws()
    {
        var service =
            new FinanceAssistantService(
                new FakeFinanceAssistantProvider(),
                new FakeFinanceToolRegistry());

        Assert.ThrowsAsync<ArgumentException>(
            async () =>
                await service.AskAsync(
                    new FinanceAssistantRequest
                    {
                        Question = "test"
                    }));
    }

    [Test]
    public void AskAsync_WithEmptyQuestion_Throws()
    {
        var service =
            new FinanceAssistantService(
                new FakeFinanceAssistantProvider(),
                new FakeFinanceToolRegistry());

        Assert.ThrowsAsync<ArgumentException>(
            async () =>
                await service.AskAsync(
                    new FinanceAssistantRequest
                    {
                        RunId = Guid.NewGuid()
                    }));
    }

    private sealed class FakeFinanceAssistantProvider
        : IFinanceAssistantProvider
    {
        private readonly Queue<
            FinanceAssistantProviderResponse> _responses;

        public FakeFinanceAssistantProvider(
            IEnumerable<FinanceAssistantProviderResponse>?
                responses = null)
        {
            _responses =
                new Queue<
                    FinanceAssistantProviderResponse>(
                    responses ??
                    Array.Empty<FinanceAssistantProviderResponse>());
        }

        public string ProviderName =>
            "TestProvider";

        public int Calls { get; private set; }

        public FinanceAssistantProviderRequest
            LastRequest { get; private set; } = null!;

        public Task<FinanceAssistantProviderResponse> AskAsync(
            FinanceAssistantProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequest = request;

            return Task.FromResult(
                _responses.Count > 0
                    ? _responses.Dequeue()
                    : new FinanceAssistantProviderResponse
                    {
                        Answer = "test",
                        RequiresToolExecution = false
                    });
        }
    }

    private sealed class FakeFinanceToolRegistry
        : IFinanceToolRegistry
    {
        private readonly Dictionary<
            string,
            IFinanceTool> _tools;

        public FakeFinanceToolRegistry(
            params IFinanceTool[] tools)
        {
            _tools =
                tools.ToDictionary(
                    x => x.Name,
                    StringComparer.Ordinal);
        }

        public int LookupCalls { get; private set; }

        public IReadOnlyCollection<string> ToolNames =>
            _tools.Keys.ToArray();

        public bool TryGet(
            string toolName,
            out IFinanceTool? tool)
        {
            LookupCalls++;

            return _tools.TryGetValue(
                toolName,
                out tool);
        }
    }

    private sealed class FakeFinanceTool
        : IFinanceTool
    {
        private readonly FinanceToolResult _result;

        public FakeFinanceTool(
            string name,
            FinanceToolResult result)
        {
            Name = name;
            _result = result;
        }

        public string Name { get; }

        public int Calls { get; private set; }

        public FinanceToolRequest LastRequest { get; private set; } = null!;

        public Task<FinanceToolResult> ExecuteAsync(
            FinanceToolRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequest = request;

            return Task.FromResult(_result);
        }
    }
}
