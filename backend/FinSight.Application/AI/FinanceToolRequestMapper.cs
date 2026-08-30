using System.Text.Json;

namespace FinSight.Application.AI;

public static class FinanceToolRequestMapper
{
    /// <summary>
    /// The top-level GUID argument(s) each registered tool actually needs.
    /// getExceptionDetails deliberately requires only "exceptionId" -- its
    /// FinanceToolDefinition (see FinanceAssistantService.BuildToolDefinitions)
    /// never declares "runId" as a parameter, so a real model call to it
    /// never supplies one; requiring it here unconditionally (the prior
    /// behavior) made every real getExceptionDetails call fail before
    /// ExceptionDetailsTool ever ran. A tool name absent from this table
    /// requires nothing -- unreachable in production since
    /// FinanceAssistantService checks FinanceToolRegistry.TryGet before
    /// ever calling TryMap, but kept fail-safe (never fail-open on
    /// financial data) rather than throwing here.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string[]> RequiredArgumentsByTool =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["getReconciliationSummary"] = new[] { "runId" },
            ["getUnmatchedRecords"] = new[] { "runId" },
            ["getTransactionDetails"] = new[] { "runId", "resultId" },
            ["getExceptionDetails"] = new[] { "exceptionId" }
        };

    public static bool TryMap(
        string toolName,
        IReadOnlyDictionary<string, JsonElement> arguments,
        out FinanceToolRequest request,
        out string? error)
    {
        request = new FinanceToolRequest();
        error = null;

        var required =
            RequiredArgumentsByTool.TryGetValue(toolName, out var requiredForTool)
                ? requiredForTool
                : Array.Empty<string>();

        if (!TryGetGuidArgument(
                arguments,
                "runId",
                required.Contains("runId"),
                out var runId,
                out error))
        {
            return false;
        }

        if (!TryGetGuidArgument(
                arguments,
                "exceptionId",
                required.Contains("exceptionId"),
                out var exceptionId,
                out error))
        {
            return false;
        }

        if (!TryGetGuidArgument(
                arguments,
                "resultId",
                required.Contains("resultId"),
                out var resultId,
                out error))
        {
            return false;
        }

        if (!TryGetNullableString(
                arguments,
                "transactionReference",
                out var transactionReference,
                out error))
        {
            return false;
        }

        var pageNumber = 1;

        if (arguments.TryGetValue(
                "pageNumber",
                out var pageNumberElement))
        {
            if (pageNumberElement.ValueKind != JsonValueKind.Number ||
                !pageNumberElement.TryGetInt32(out pageNumber) ||
                pageNumber < 1)
            {
                error =
                    "pageNumber must be an integer greater than or equal to 1.";

                return false;
            }
        }

        var pageSize = 20;

        if (arguments.TryGetValue(
                "pageSize",
                out var pageSizeElement))
        {
            if (pageSizeElement.ValueKind != JsonValueKind.Number ||
                !pageSizeElement.TryGetInt32(out pageSize) ||
                pageSize < 1 ||
                pageSize > 100)
            {
                error =
                    "pageSize must be an integer between 1 and 100.";

                return false;
            }
        }

        request = new FinanceToolRequest
        {
            RunId = runId,
            ExceptionId = exceptionId,
            ResultId = resultId,
            TransactionReference = transactionReference,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        return true;
    }

    /// <summary>
    /// Reads one nullable-GUID argument. When <paramref name="isRequired"/>
    /// is true, a missing/null/malformed value fails with a structured
    /// error (the prior unconditional-runId behavior, now applied per
    /// argument per tool). When false, the argument is still validated as
    /// a GUID *if present* -- a tool that doesn't need it may still
    /// tolerate it being supplied, but a present-and-malformed value is
    /// never silently ignored.
    /// </summary>
    private static bool TryGetGuidArgument(
        IReadOnlyDictionary<string, JsonElement> arguments,
        string name,
        bool isRequired,
        out Guid? value,
        out string? error)
    {
        if (isRequired)
        {
            return TryGetGuid(arguments, name, out value, out error);
        }

        return TryGetNullableGuid(arguments, name, out value, out error);
    }

    private static bool TryGetGuid(
        IReadOnlyDictionary<string, JsonElement> arguments,
        string name,
        out Guid? value,
        out string? error)
    {
        value = null;

        if (!arguments.TryGetValue(name, out var element))
        {
            error = $"Required argument '{name}' is missing.";
            return false;
        }

        if (element.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(element.GetString(), out var parsed) ||
            parsed == Guid.Empty)
        {
            error = $"Argument '{name}' must be a valid GUID.";
            return false;
        }

        value = parsed;
        error = null;
        return true;
    }

    private static bool TryGetNullableGuid(
        IReadOnlyDictionary<string, JsonElement> arguments,
        string name,
        out Guid? value,
        out string? error)
    {
        value = null;

        if (!arguments.TryGetValue(name, out var element))
        {
            error = null;
            return true;
        }

        if (element.ValueKind == JsonValueKind.Null)
        {
            error = null;
            return true;
        }

        if (element.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(element.GetString(), out var parsed) ||
            parsed == Guid.Empty)
        {
            error = $"Argument '{name}' must be a valid GUID or null.";
            return false;
        }

        value = parsed;
        error = null;
        return true;
    }

    private static bool TryGetNullableString(
        IReadOnlyDictionary<string, JsonElement> arguments,
        string name,
        out string? value,
        out string? error)
    {
        value = null;

        if (!arguments.TryGetValue(name, out var element))
        {
            error = null;
            return true;
        }

        if (element.ValueKind == JsonValueKind.Null)
        {
            error = null;
            return true;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            error = $"Argument '{name}' must be a string or null.";
            return false;
        }

        value = element.GetString();

        error = null;
        return true;
    }
}
