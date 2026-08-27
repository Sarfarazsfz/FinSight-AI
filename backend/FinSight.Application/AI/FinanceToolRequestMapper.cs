using System.Text.Json;

namespace FinSight.Application.AI;

public static class FinanceToolRequestMapper
{
    public static bool TryMap(
        IReadOnlyDictionary<string, JsonElement> arguments,
        out FinanceToolRequest request,
        out string? error)
    {
        request = new FinanceToolRequest();
        error = null;

        if (!TryGetGuid(
                arguments,
                "runId",
                out var runId,
                out error))
        {
            return false;
        }

        if (!TryGetNullableGuid(
                arguments,
                "exceptionId",
                out var exceptionId,
                out error))
        {
            return false;
        }

        if (!TryGetNullableGuid(
                arguments,
                "resultId",
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
