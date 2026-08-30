using System.Text.Json;
using FinSight.Application.Abstractions.Persistence;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;

namespace FinSight.Application.AI;

public sealed class FinanceAssistantService
    : IFinanceAssistantService
{
    private readonly IFinanceAssistantProvider _provider;
    private readonly IFinanceToolRegistry _registry;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IUnitOfWork _unitOfWork;

    public FinanceAssistantService(
        IFinanceAssistantProvider provider,
        IFinanceToolRegistry registry,
        IAuditLogWriter auditLogWriter,
        IUnitOfWork unitOfWork)
    {
        _provider = provider;
        _registry = registry;
        _auditLogWriter = auditLogWriter;
        _unitOfWork = unitOfWork;
    }

    public async Task<FinanceAssistantResponse> AskAsync(
        FinanceAssistantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(
                nameof(request));
        }

        if (request.RunId == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid runId is required.",
                nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Question))
        {
            throw new ArgumentException(
                "Question is required.",
                nameof(request));
        }

        FinanceAssistantResponse response;

        try
        {
            response =
                await ExecuteAsync(
                    request,
                    cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var failedPayload =
                JsonSerializer.Serialize(
                    new
                    {
                        run_id =
                            request.RunId,

                        requested_provider =
                            _provider.ProviderName,

                        error_type =
                            ex.GetType().Name,

                        error_message =
                            ex.Message
                    });

            await _auditLogWriter.AddAsync(
                new AuditLog(
                    AuditEventType.AiAssistantFailed,
                    failedPayload,
                    request.RunId),
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            throw;
        }

        var successPayload =
            JsonSerializer.Serialize(
                new
                {
                    run_id =
                        request.RunId,

                    requested_provider =
                        _provider.ProviderName,

                    tools_used =
                        response.ToolsUsed,

                    // Deliberately not the raw question text -- see
                    // FinanceAssistantService class-level reasoning: F9's
                    // AiExplanationService audit payloads never persist
                    // free-text user input either, only structured
                    // metadata. Length alone is enough to be operationally
                    // useful without storing user-authored content.
                    question_length =
                        request.Question.Trim().Length
                });

        await _auditLogWriter.AddAsync(
            new AuditLog(
                AuditEventType.AiQuestionAsked,
                successPayload,
                request.RunId),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return response;
    }

    /// <summary>
    /// The original two-call tool-orchestration flow, unchanged in
    /// behavior -- extracted verbatim so AskAsync can wrap it once with
    /// audit logging at a single success exit point and a single failure
    /// catch, instead of duplicating both at every return statement.
    /// </summary>
    private async Task<FinanceAssistantResponse> ExecuteAsync(
        FinanceAssistantRequest request,
        CancellationToken cancellationToken)
    {
        var toolDefinitions =
            BuildToolDefinitions();

        var toolsUsed =
            new List<string>();

        /*
         * Finance Assistant intentionally uses only two model calls:
         *
         * 1. Gemini decides which authoritative tools are needed.
         * 2. Gemini receives the collected evidence and produces
         *    the final answer with tool calling disabled.
         *
         * This prevents recursive tool loops and dramatically
         * reduces Gemini quota consumption.
         */

        var firstProviderRequest =
            new FinanceAssistantProviderRequest
            {
                RunId =
                    request.RunId,

                Question =
                    request.Question.Trim(),

                Tools =
                    toolDefinitions,

                PreviousToolCalls =
                    Array.Empty<FinanceToolCall>(),

                ToolResults =
                    Array.Empty<FinanceToolResultMessage>()
            };

        var providerResponse =
            await _provider.AskAsync(
                firstProviderRequest,
                cancellationToken);

        if (!providerResponse.RequiresToolExecution)
        {
            return new FinanceAssistantResponse
            {
                Answer =
                    providerResponse.Answer,

                ToolsUsed =
                    Array.Empty<string>()
            };
        }

        if (providerResponse.ToolCalls.Count == 0)
        {
            throw new InvalidOperationException(
                "Gemini requested tool execution " +
                "but returned no tool calls.");
        }

        var toolResults =
            new List<FinanceToolResultMessage>();

        foreach (
            var toolCall
            in providerResponse.ToolCalls)
        {
            if (!_registry.TryGet(
                    toolCall.Name,
                    out var tool))
            {
                throw new InvalidOperationException(
                    $"Tool '{toolCall.Name}' is not allowed.");
            }

            if (!FinanceToolRequestMapper.TryMap(
                    toolCall.Name,
                    toolCall.Arguments,
                    out var toolRequest,
                    out var mappingError))
            {
                toolResults.Add(
                    new FinanceToolResultMessage
                    {
                        ToolCallId =
                            toolCall.Id,

                        ToolName =
                            toolCall.Name,

                        Success = false,

                        ErrorCode =
                            "INVALID_ARGUMENT",

                        ResultJson =
                            JsonSerializer.Serialize(
                                new
                                {
                                    error =
                                        mappingError
                                })
                    });

                continue;
            }

            var toolResult =
                await tool!.ExecuteAsync(
                    toolRequest,
                    cancellationToken);

            toolsUsed.Add(
                tool.Name);

            toolResults.Add(
                new FinanceToolResultMessage
                {
                    ToolCallId =
                        toolCall.Id,

                    ToolName =
                        tool.Name,

                    Success =
                        toolResult.Success,

                    ErrorCode =
                        toolResult.ErrorCode,

                    ResultJson =
                        toolResult.Success
                            ? toolResult.DataJson
                            : JsonSerializer.Serialize(
                                new
                                {
                                    error =
                                        toolResult.ErrorMessage,

                                    errorCode =
                                        toolResult.ErrorCode
                                })
                });
        }

        var evidence =
            toolResults
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
                        })
                .ToArray();

        var finalQuestion =
            $"""
            You are the final finance-analysis stage.

            Reconciliation Run ID:
            {request.RunId}

            User question:
            {request.Question.Trim()}

            The backend has already executed the authoritative
            finance tools. Do NOT call any tools and do NOT invent
            tool names.

            Use ONLY the following authoritative backend evidence:

            {JsonSerializer.Serialize(
                evidence,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                })}

            Give a clear, concise finance-operations answer.
            Include the important counts, reconciliation issues,
            affected records, and the most important exceptions
            supported by the evidence.
            """;

        var finalProviderRequest =
            new FinanceAssistantProviderRequest
            {
                RunId =
                    request.RunId,

                Question =
                    finalQuestion,

                Tools =
                    Array.Empty<FinanceToolDefinition>(),

                PreviousToolCalls =
                    providerResponse.ToolCalls,

                ToolResults =
                    toolResults
            };

        var finalResponse =
            await _provider.AskAsync(
                finalProviderRequest,
                cancellationToken);

        if (finalResponse.RequiresToolExecution)
        {
            throw new InvalidOperationException(
                "Gemini attempted a tool call during " +
                "final synthesis.");
        }

        if (string.IsNullOrWhiteSpace(
                finalResponse.Answer))
        {
            throw new InvalidOperationException(
                "Gemini returned an empty final answer.");
        }

        return new FinanceAssistantResponse
        {
            Answer =
                finalResponse.Answer.Trim(),

            ToolsUsed =
                toolsUsed
                    .Distinct()
                    .ToArray()
        };
    }

    private IReadOnlyCollection<FinanceToolDefinition>
        BuildToolDefinitions()
    {
        return new[]
        {
            new FinanceToolDefinition
            {
                Name = "getReconciliationSummary",
                Description =
                    "Returns the authoritative summary of a reconciliation run.",
                Parameters =
                    new Dictionary<string, FinanceToolParameter>
                    {
                        ["runId"] =
                            new()
                            {
                                Type = "string",
                                Description =
                                    "Reconciliation run GUID.",
                                Required = true
                            }
                    }
            },

            new FinanceToolDefinition
            {
                Name = "getUnmatchedRecords",
                Description =
                    "Returns every non-matched reconciliation result for a run.",
                Parameters =
                    new Dictionary<string, FinanceToolParameter>
                    {
                        ["runId"] =
                            new()
                            {
                                Type = "string",
                                Description =
                                    "Reconciliation run GUID.",
                                Required = true
                            }
                    }
            },

            new FinanceToolDefinition
            {
                Name = "getTransactionDetails",
                Description =
                    "Returns authoritative payment, bank and settlement details for a reconciliation result.",
                Parameters =
                    new Dictionary<string, FinanceToolParameter>
                    {
                        ["runId"] =
                            new()
                            {
                                Type = "string",
                                Description =
                                    "Reconciliation run GUID.",
                                Required = true
                            },
                        ["resultId"] =
                            new()
                            {
                                Type = "string",
                                Description =
                                    "Reconciliation result GUID.",
                                Required = true
                            }
                    }
            },

            new FinanceToolDefinition
            {
                Name = "getExceptionDetails",
                Description =
                    "Returns authoritative details for a reconciliation exception.",
                Parameters =
                    new Dictionary<string, FinanceToolParameter>
                    {
                        ["exceptionId"] =
                            new()
                            {
                                Type = "string",
                                Description =
                                    "Reconciliation exception GUID.",
                                Required = true
                            }
                    }
            },

        };
    }
}
