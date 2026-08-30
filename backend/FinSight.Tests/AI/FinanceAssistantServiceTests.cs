using System.Text.Json;
using FinSight.Application.AI;
using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Exceptions;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;

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

        var auditWriter =
            new FakeAuditLogWriter();

        var unitOfWork =
            new FakeUnitOfWork();

        var runId = Guid.NewGuid();

        var service =
            new FinanceAssistantService(
                provider,
                registry,
                auditWriter,
                unitOfWork);

        var response =
            await service.AskAsync(
                new FinanceAssistantRequest
                {
                    RunId = runId,
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

            // Exactly one audit event -- no duplicate, no failure event
            // alongside the success event.
            Assert.That(
                auditWriter.Events,
                Has.Count.EqualTo(1));

            Assert.That(
                auditWriter.Events[0].EventType,
                Is.EqualTo(AuditEventType.AiQuestionAsked));

            Assert.That(
                auditWriter.Events[0].RunId,
                Is.EqualTo(runId));

            Assert.That(
                auditWriter.Events[0].DetailPayload,
                Does.Contain("\"tools_used\":[]"));

            // The raw question text is never persisted -- only its length.
            Assert.That(
                auditWriter.Events[0].DetailPayload,
                Does.Not.Contain("reconciliation run"));

            Assert.That(
                unitOfWork.SaveChangesCalls,
                Is.EqualTo(1));
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

        var auditWriter =
            new FakeAuditLogWriter();

        var unitOfWork =
            new FakeUnitOfWork();

        var service =
            new FinanceAssistantService(
                provider,
                registry,
                auditWriter,
                unitOfWork);

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

            Assert.That(
                auditWriter.Events,
                Has.Count.EqualTo(1));

            Assert.That(
                auditWriter.Events[0].EventType,
                Is.EqualTo(AuditEventType.AiQuestionAsked));

            Assert.That(
                auditWriter.Events[0].DetailPayload,
                Does.Contain("\"getReconciliationSummary\""));

            Assert.That(
                unitOfWork.SaveChangesCalls,
                Is.EqualTo(1));
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

        var auditWriter =
            new FakeAuditLogWriter();

        var unitOfWork =
            new FakeUnitOfWork();

        var service =
            new FinanceAssistantService(
                provider,
                registry,
                auditWriter,
                unitOfWork);

        var runId = Guid.NewGuid();

        Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await service.AskAsync(
                    new FinanceAssistantRequest
                    {
                        RunId = runId,
                        Question = "Delete transactions."
                    }));

        Assert.Multiple(() =>
        {
            Assert.That(
                auditWriter.Events,
                Has.Count.EqualTo(1));

            Assert.That(
                auditWriter.Events[0].EventType,
                Is.EqualTo(AuditEventType.AiAssistantFailed));

            Assert.That(
                auditWriter.Events[0].RunId,
                Is.EqualTo(runId));

            Assert.That(
                auditWriter.Events[0].DetailPayload,
                Does.Contain("\"error_type\":\"InvalidOperationException\""));

            Assert.That(
                unitOfWork.SaveChangesCalls,
                Is.EqualTo(1));
        });
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

        var auditWriter =
            new FakeAuditLogWriter();

        var unitOfWork =
            new FakeUnitOfWork();

        var service =
            new FinanceAssistantService(
                provider,
                registry,
                auditWriter,
                unitOfWork);

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

            // A tool-level INVALID_ARGUMENT that the model still recovers
            // from is a successful AskAsync call overall -- AiQuestionAsked,
            // not AiAssistantFailed.
            Assert.That(
                auditWriter.Events,
                Has.Count.EqualTo(1));

            Assert.That(
                auditWriter.Events[0].EventType,
                Is.EqualTo(AuditEventType.AiQuestionAsked));
        });
    }

    [Test]
    public void AskAsync_WithEmptyRunId_Throws()
    {
        var auditWriter = new FakeAuditLogWriter();

        var service =
            new FinanceAssistantService(
                new FakeFinanceAssistantProvider(),
                new FakeFinanceToolRegistry(),
                auditWriter,
                new FakeUnitOfWork());

        Assert.ThrowsAsync<ArgumentException>(
            async () =>
                await service.AskAsync(
                    new FinanceAssistantRequest
                    {
                        Question = "test"
                    }));

        // Basic argument validation never reaches the AI layer, so nothing
        // is audit-logged for it -- matches AiExplanationService, which
        // never logs its own equivalent up-front ArgumentException checks.
        Assert.That(
            auditWriter.Events,
            Is.Empty);
    }

    [Test]
    public void AskAsync_WithEmptyQuestion_Throws()
    {
        var auditWriter = new FakeAuditLogWriter();

        var service =
            new FinanceAssistantService(
                new FakeFinanceAssistantProvider(),
                new FakeFinanceToolRegistry(),
                auditWriter,
                new FakeUnitOfWork());

        Assert.ThrowsAsync<ArgumentException>(
            async () =>
                await service.AskAsync(
                    new FinanceAssistantRequest
                    {
                        RunId = Guid.NewGuid()
                    }));

        Assert.That(
            auditWriter.Events,
            Is.Empty);
    }

    [Test]
    public async Task AskAsync_WhenBothProvidersUnavailable_WritesAiAssistantFailedAndRethrows()
    {
        var provider =
            new FakeFinanceAssistantProvider(
                new[]
                {
                    new FinanceAssistantProviderResponse()
                },
                throwOnFirstCall:
                    new FinanceAssistantProviderUnavailableException(
                        "Both Finance Assistant AI providers failed."));

        var registry =
            new FakeFinanceToolRegistry();

        var auditWriter =
            new FakeAuditLogWriter();

        var unitOfWork =
            new FakeUnitOfWork();

        var service =
            new FinanceAssistantService(
                provider,
                registry,
                auditWriter,
                unitOfWork);

        var runId = Guid.NewGuid();

        var thrown =
            Assert.ThrowsAsync<FinanceAssistantProviderUnavailableException>(
                async () =>
                    await service.AskAsync(
                        new FinanceAssistantRequest
                        {
                            RunId = runId,
                            Question = "What is the match rate?"
                        }));

        Assert.Multiple(() =>
        {
            Assert.That(
                thrown!.Message,
                Is.EqualTo("Both Finance Assistant AI providers failed."));

            Assert.That(
                auditWriter.Events,
                Has.Count.EqualTo(1));

            Assert.That(
                auditWriter.Events[0].EventType,
                Is.EqualTo(AuditEventType.AiAssistantFailed));

            Assert.That(
                auditWriter.Events[0].RunId,
                Is.EqualTo(runId));

            Assert.That(
                auditWriter.Events[0].DetailPayload,
                Does.Contain(
                    "\"error_type\":\"FinanceAssistantProviderUnavailableException\""));

            Assert.That(
                auditWriter.Events[0].DetailPayload,
                Does.Not.Contain("match rate"));

            Assert.That(
                unitOfWork.SaveChangesCalls,
                Is.EqualTo(1));
        });
    }

    [Test]
    public void AskAsync_WhenCancelled_PropagatesWithoutAuditLog()
    {
        var provider =
            new FakeFinanceAssistantProvider(
                throwOnFirstCall:
                    new OperationCanceledException());

        var registry =
            new FakeFinanceToolRegistry();

        var auditWriter =
            new FakeAuditLogWriter();

        var service =
            new FinanceAssistantService(
                provider,
                registry,
                auditWriter,
                new FakeUnitOfWork());

        Assert.ThrowsAsync<OperationCanceledException>(
            async () =>
                await service.AskAsync(
                    new FinanceAssistantRequest
                    {
                        RunId = Guid.NewGuid(),
                        Question = "test"
                    }));

        Assert.That(
            auditWriter.Events,
            Is.Empty);
    }

    [Test]
    public async Task AskAsync_CalledTwiceInSequenceOnSameServiceInstance_BothRequestsSucceed()
    {
        // Regression test for the reported "second question fails" bug
        // class. The confirmed root cause was Gemini-provider content
        // construction (see GeminiFinanceAssistantProviderTests), not
        // service-level state -- this proves that on the SAME
        // FinanceAssistantService instance, a second, independent
        // question succeeds exactly like the first, with no leaked state
        // between requests.
        var runId = Guid.NewGuid();

        var summaryTool =
            new FakeFinanceTool(
                "getReconciliationSummary",
                new FinanceToolResult
                {
                    ToolName = "getReconciliationSummary",
                    Success = true,
                    DataJson = """{"matched":70}"""
                });

        var exceptionTool =
            new FakeFinanceTool(
                "getExceptionDetails",
                new FinanceToolResult
                {
                    ToolName = "getExceptionDetails",
                    Success = true,
                    DataJson = """{"category":"AmountMismatch"}"""
                });

        var registry =
            new FakeFinanceToolRegistry(summaryTool, exceptionTool);

        var provider =
            new FakeFinanceAssistantProvider(
                new[]
                {
                    // Question 1: tool-selection, then final synthesis.
                    new FinanceAssistantProviderResponse
                    {
                        RequiresToolExecution = true,
                        ToolCalls = new[]
                        {
                            new FinanceToolCall
                            {
                                Id = "c1",
                                Name = "getReconciliationSummary",
                                Arguments = new Dictionary<string, JsonElement>
                                {
                                    ["runId"] = JsonSerializer.Deserialize<JsonElement>(
                                        $"\"{runId}\"")
                                }
                            }
                        }
                    },
                    new FinanceAssistantProviderResponse
                    {
                        Answer = "The match rate is 91.5%.",
                        RequiresToolExecution = false
                    },

                    // Question 2: tool-selection, then final synthesis.
                    new FinanceAssistantProviderResponse
                    {
                        RequiresToolExecution = true,
                        ToolCalls = new[]
                        {
                            new FinanceToolCall
                            {
                                Id = "c2",
                                Name = "getExceptionDetails",
                                Arguments = new Dictionary<string, JsonElement>
                                {
                                    ["exceptionId"] = JsonSerializer.Deserialize<JsonElement>(
                                        $"\"{Guid.NewGuid()}\"")
                                }
                            }
                        }
                    },
                    new FinanceAssistantProviderResponse
                    {
                        Answer = "TXN-0098 had an amount mismatch.",
                        RequiresToolExecution = false
                    }
                });

        var auditWriter =
            new FakeAuditLogWriter();

        var unitOfWork =
            new FakeUnitOfWork();

        var service =
            new FinanceAssistantService(
                provider,
                registry,
                auditWriter,
                unitOfWork);

        var firstResponse =
            await service.AskAsync(
                new FinanceAssistantRequest
                {
                    RunId = runId,
                    Question = "What is the match rate for this run?"
                });

        var secondResponse =
            await service.AskAsync(
                new FinanceAssistantRequest
                {
                    RunId = runId,
                    Question = "What happened with TXN-0098?"
                });

        Assert.Multiple(() =>
        {
            Assert.That(
                firstResponse.Answer,
                Is.EqualTo("The match rate is 91.5%."));

            Assert.That(
                secondResponse.Answer,
                Is.EqualTo("TXN-0098 had an amount mismatch."));

            Assert.That(
                firstResponse.ToolsUsed,
                Is.EqualTo(new[] { "getReconciliationSummary" }));

            Assert.That(
                secondResponse.ToolsUsed,
                Is.EqualTo(new[] { "getExceptionDetails" }));

            // No leaked/duplicate failure audit -- exactly one
            // AiQuestionAsked per successful question, zero AiAssistantFailed.
            Assert.That(
                auditWriter.Events,
                Has.Count.EqualTo(2));

            Assert.That(
                auditWriter.Events.All(
                    e => e.EventType == AuditEventType.AiQuestionAsked),
                Is.True);
        });
    }

    [Test]
    public async Task AskAsync_CalledThreeTimesInSequence_AllThreeRequestsReachFinalSynthesisSuccessfully()
    {
        var runId = Guid.NewGuid();

        var summaryTool =
            new FakeFinanceTool(
                "getReconciliationSummary",
                new FinanceToolResult
                {
                    ToolName = "getReconciliationSummary",
                    Success = true,
                    DataJson = """{"matched":70}"""
                });

        var unmatchedTool =
            new FakeFinanceTool(
                "getUnmatchedRecords",
                new FinanceToolResult
                {
                    ToolName = "getUnmatchedRecords",
                    Success = true,
                    DataJson = """{"totalUnmatched":3,"items":[]}"""
                });

        var exceptionTool =
            new FakeFinanceTool(
                "getExceptionDetails",
                new FinanceToolResult
                {
                    ToolName = "getExceptionDetails",
                    Success = true,
                    DataJson = """{"category":"AmountMismatch"}"""
                });

        var registry =
            new FakeFinanceToolRegistry(summaryTool, unmatchedTool, exceptionTool);

        var provider =
            new FakeFinanceAssistantProvider(
                new[]
                {
                    // Q1: "What is the match rate?"
                    new FinanceAssistantProviderResponse
                    {
                        RequiresToolExecution = true,
                        ToolCalls = new[]
                        {
                            new FinanceToolCall
                            {
                                Id = "c1",
                                Name = "getReconciliationSummary",
                                Arguments = new Dictionary<string, JsonElement>
                                {
                                    ["runId"] = JsonSerializer.Deserialize<JsonElement>(
                                        $"\"{runId}\"")
                                }
                            }
                        }
                    },
                    new FinanceAssistantProviderResponse
                    {
                        Answer = "The match rate is 91.5%.",
                        RequiresToolExecution = false
                    },

                    // Q2: "Which transactions are unmatched?"
                    new FinanceAssistantProviderResponse
                    {
                        RequiresToolExecution = true,
                        ToolCalls = new[]
                        {
                            new FinanceToolCall
                            {
                                Id = "c2",
                                Name = "getUnmatchedRecords",
                                Arguments = new Dictionary<string, JsonElement>
                                {
                                    ["runId"] = JsonSerializer.Deserialize<JsonElement>(
                                        $"\"{runId}\"")
                                }
                            }
                        }
                    },
                    new FinanceAssistantProviderResponse
                    {
                        Answer = "3 transactions are unmatched.",
                        RequiresToolExecution = false
                    },

                    // Q3: "Explain TXN-0098"
                    new FinanceAssistantProviderResponse
                    {
                        RequiresToolExecution = true,
                        ToolCalls = new[]
                        {
                            new FinanceToolCall
                            {
                                Id = "c3",
                                Name = "getExceptionDetails",
                                Arguments = new Dictionary<string, JsonElement>
                                {
                                    ["exceptionId"] = JsonSerializer.Deserialize<JsonElement>(
                                        $"\"{Guid.NewGuid()}\"")
                                }
                            }
                        }
                    },
                    new FinanceAssistantProviderResponse
                    {
                        Answer = "TXN-0098 had an amount mismatch.",
                        RequiresToolExecution = false
                    }
                });

        var auditWriter =
            new FakeAuditLogWriter();

        var service =
            new FinanceAssistantService(
                provider,
                registry,
                auditWriter,
                new FakeUnitOfWork());

        var responses = new List<FinanceAssistantResponse>
        {
            await service.AskAsync(
                new FinanceAssistantRequest { RunId = runId, Question = "What is the match rate?" }),

            await service.AskAsync(
                new FinanceAssistantRequest
                {
                    RunId = runId,
                    Question = "Which transactions are unmatched?"
                }),

            await service.AskAsync(
                new FinanceAssistantRequest { RunId = runId, Question = "Explain TXN-0098" })
        };

        Assert.Multiple(() =>
        {
            Assert.That(responses[0].Answer, Is.EqualTo("The match rate is 91.5%."));
            Assert.That(responses[1].Answer, Is.EqualTo("3 transactions are unmatched."));
            Assert.That(responses[2].Answer, Is.EqualTo("TXN-0098 had an amount mismatch."));

            Assert.That(responses[0].ToolsUsed, Is.EqualTo(new[] { "getReconciliationSummary" }));
            Assert.That(responses[1].ToolsUsed, Is.EqualTo(new[] { "getUnmatchedRecords" }));
            Assert.That(responses[2].ToolsUsed, Is.EqualTo(new[] { "getExceptionDetails" }));

            Assert.That(auditWriter.Events, Has.Count.EqualTo(3));

            Assert.That(
                auditWriter.Events.All(
                    e => e.EventType == AuditEventType.AiQuestionAsked),
                Is.True);
        });
    }

    [Test]
    public void AskAsync_WhenFinalSynthesisStillRequiresToolExecution_ThrowsAndWritesAiAssistantFailed()
    {
        // The safety guard for the live bug: if the second/final-synthesis
        // provider call still comes back requesting a tool call (a genuine
        // SDK/model misbehavior, independent of the Gemini-provider-level
        // fix), the service must throw rather than silently continue or
        // fabricate an answer. This is defense-in-depth, kept regardless
        // of the provider-level fix -- see
        // GeminiFinanceAssistantProviderTests for the actual root-cause fix.
        var runId = Guid.NewGuid();

        var tool =
            new FakeFinanceTool(
                "getReconciliationSummary",
                new FinanceToolResult
                {
                    ToolName = "getReconciliationSummary",
                    Success = true,
                    DataJson = """{"matched":70}"""
                });

        var registry =
            new FakeFinanceToolRegistry(tool);

        var firstCall =
            new FinanceToolCall
            {
                Id = "call-1",
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
                        ToolCalls = new[] { firstCall }
                    },

                    // Illegal: the final-synthesis call still requests a
                    // tool call instead of returning text.
                    new FinanceAssistantProviderResponse
                    {
                        RequiresToolExecution = true,
                        ToolCalls =
                            new[]
                            {
                                new FinanceToolCall
                                {
                                    Id = "call-2",
                                    Name = "getReconciliationSummary"
                                }
                            }
                    }
                });

        var auditWriter =
            new FakeAuditLogWriter();

        var unitOfWork =
            new FakeUnitOfWork();

        var service =
            new FinanceAssistantService(
                provider,
                registry,
                auditWriter,
                unitOfWork);

        var thrown =
            Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                    await service.AskAsync(
                        new FinanceAssistantRequest
                        {
                            RunId = runId,
                            Question = "Summarize the run."
                        }));

        Assert.Multiple(() =>
        {
            Assert.That(
                thrown!.Message,
                Is.EqualTo(
                    "Gemini attempted a tool call during final synthesis."));

            Assert.That(
                auditWriter.Events,
                Has.Count.EqualTo(1));

            Assert.That(
                auditWriter.Events[0].EventType,
                Is.EqualTo(AuditEventType.AiAssistantFailed));

            Assert.That(
                auditWriter.Events[0].RunId,
                Is.EqualTo(runId));

            Assert.That(
                unitOfWork.SaveChangesCalls,
                Is.EqualTo(1));
        });
    }

    [Test]
    public async Task AskAsync_WithGetExceptionDetailsToolCall_NeverSuppliesRunId_StillSucceeds()
    {
        // Regression test for Finding A: getExceptionDetails' declared
        // FinanceToolDefinition never includes "runId", so a real model
        // call never supplies one. Before the fix, FinanceToolRequestMapper
        // required runId unconditionally and this call would have failed
        // with INVALID_ARGUMENT before ExceptionDetailsTool ever ran.
        var runId = Guid.NewGuid();
        var exceptionId = Guid.NewGuid();

        var tool =
            new FakeFinanceTool(
                "getExceptionDetails",
                new FinanceToolResult
                {
                    ToolName = "getExceptionDetails",
                    Success = true,
                    DataJson = """{"category":"AmountMismatch"}"""
                });

        var registry =
            new FakeFinanceToolRegistry(tool);

        var firstCall =
            new FinanceToolCall
            {
                Name = "getExceptionDetails",
                Arguments =
                    new Dictionary<string, JsonElement>
                    {
                        // Deliberately no "runId" key at all.
                        ["exceptionId"] =
                            JsonSerializer.Deserialize<JsonElement>(
                                $"\"{exceptionId}\"")
                    }
            };

        var provider =
            new FakeFinanceAssistantProvider(
                new[]
                {
                    new FinanceAssistantProviderResponse
                    {
                        RequiresToolExecution = true,
                        ToolCalls = new[] { firstCall }
                    },
                    new FinanceAssistantProviderResponse
                    {
                        Answer = "This exception is an amount mismatch.",
                        RequiresToolExecution = false
                    }
                });

        var service =
            new FinanceAssistantService(
                provider,
                registry,
                new FakeAuditLogWriter(),
                new FakeUnitOfWork());

        var response =
            await service.AskAsync(
                new FinanceAssistantRequest
                {
                    RunId = runId,
                    Question = "Explain this exception."
                });

        Assert.Multiple(() =>
        {
            Assert.That(
                response.Answer,
                Is.EqualTo("This exception is an amount mismatch."));

            Assert.That(
                tool.Calls,
                Is.EqualTo(1));

            Assert.That(
                tool.LastRequest.ExceptionId,
                Is.EqualTo(exceptionId));

            Assert.That(
                tool.LastRequest.RunId,
                Is.Null);

            Assert.That(
                response.ToolsUsed,
                Is.EqualTo(new[] { "getExceptionDetails" }));
        });
    }

    private sealed class FakeFinanceAssistantProvider
        : IFinanceAssistantProvider
    {
        private readonly Queue<
            FinanceAssistantProviderResponse> _responses;

        private readonly Exception? _throwOnFirstCall;

        public FakeFinanceAssistantProvider(
            IEnumerable<FinanceAssistantProviderResponse>?
                responses = null,
            Exception? throwOnFirstCall = null)
        {
            _responses =
                new Queue<
                    FinanceAssistantProviderResponse>(
                    responses ??
                    Array.Empty<FinanceAssistantProviderResponse>());

            _throwOnFirstCall = throwOnFirstCall;
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

            if (Calls == 1 && _throwOnFirstCall is not null)
            {
                return Task.FromException<FinanceAssistantProviderResponse>(
                    _throwOnFirstCall);
            }

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

    private sealed class FakeAuditLogWriter
        : IAuditLogWriter
    {
        public List<AuditLog> Events { get; } = new();

        public Task AddAsync(
            AuditLog auditLog,
            CancellationToken cancellationToken = default)
        {
            Events.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(
            IReadOnlyCollection<AuditLog> auditLogs,
            CancellationToken cancellationToken = default)
        {
            Events.AddRange(auditLogs);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork
        : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return Task.FromResult(1);
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
