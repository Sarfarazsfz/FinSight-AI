using System.Text.Json;
using FinSight.Application.AI;

namespace FinSight.Tests.AI;

/// <summary>
/// Phase F10.1 (Finding A): FinanceToolRequestMapper.TryMap has no
/// dedicated test file up to this point -- coverage previously only came
/// indirectly through FinanceAssistantServiceTests. These tests exercise
/// the mapper directly, proving each tool's per-tool required-argument set
/// -- in particular that getExceptionDetails succeeds with only
/// exceptionId and never requires runId, the confirmed root cause of the
/// original bug.
/// </summary>
[TestFixture]
public sealed class FinanceToolRequestMapperTests
{
    private static readonly Guid SampleRunId = Guid.NewGuid();
    private static readonly Guid SampleExceptionId = Guid.NewGuid();
    private static readonly Guid SampleResultId = Guid.NewGuid();

    [Test]
    public void TryMap_GetReconciliationSummary_RequiresRunId()
    {
        var success =
            FinanceToolRequestMapper.TryMap(
                "getReconciliationSummary",
                GuidArgs(("runId", SampleRunId)),
                out var request,
                out var error);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.True);
            Assert.That(error, Is.Null);
            Assert.That(request.RunId, Is.EqualTo(SampleRunId));
        });

        var missing =
            FinanceToolRequestMapper.TryMap(
                "getReconciliationSummary",
                new Dictionary<string, JsonElement>(),
                out _,
                out var missingError);

        Assert.Multiple(() =>
        {
            Assert.That(missing, Is.False);
            Assert.That(
                missingError,
                Is.EqualTo("Required argument 'runId' is missing."));
        });
    }

    [Test]
    public void TryMap_GetUnmatchedRecords_RequiresRunId()
    {
        var success =
            FinanceToolRequestMapper.TryMap(
                "getUnmatchedRecords",
                GuidArgs(("runId", SampleRunId)),
                out var request,
                out var error);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.True);
            Assert.That(error, Is.Null);
            Assert.That(request.RunId, Is.EqualTo(SampleRunId));
        });

        var missing =
            FinanceToolRequestMapper.TryMap(
                "getUnmatchedRecords",
                new Dictionary<string, JsonElement>(),
                out _,
                out var missingError);

        Assert.Multiple(() =>
        {
            Assert.That(missing, Is.False);
            Assert.That(
                missingError,
                Is.EqualTo("Required argument 'runId' is missing."));
        });
    }

    [Test]
    public void TryMap_GetTransactionDetails_RequiresRunIdAndResultId()
    {
        var success =
            FinanceToolRequestMapper.TryMap(
                "getTransactionDetails",
                GuidArgs(
                    ("runId", SampleRunId),
                    ("resultId", SampleResultId)),
                out var request,
                out var error);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.True);
            Assert.That(error, Is.Null);
            Assert.That(request.RunId, Is.EqualTo(SampleRunId));
            Assert.That(request.ResultId, Is.EqualTo(SampleResultId));
        });

        // runId present, resultId missing -- structured error, not a
        // silent partial success.
        var missingResultId =
            FinanceToolRequestMapper.TryMap(
                "getTransactionDetails",
                GuidArgs(("runId", SampleRunId)),
                out _,
                out var resultIdError);

        Assert.Multiple(() =>
        {
            Assert.That(missingResultId, Is.False);
            Assert.That(
                resultIdError,
                Is.EqualTo("Required argument 'resultId' is missing."));
        });

        // resultId present, runId missing.
        var missingRunId =
            FinanceToolRequestMapper.TryMap(
                "getTransactionDetails",
                GuidArgs(("resultId", SampleResultId)),
                out _,
                out var runIdError);

        Assert.Multiple(() =>
        {
            Assert.That(missingRunId, Is.False);
            Assert.That(
                runIdError,
                Is.EqualTo("Required argument 'runId' is missing."));
        });
    }

    [Test]
    public void TryMap_GetExceptionDetails_SucceedsWithExceptionIdOnly()
    {
        // The exact regression scenario: no "runId" key in the arguments
        // at all -- matching what a real model call produces, since
        // getExceptionDetails' FinanceToolDefinition never declares runId
        // as a parameter.
        var success =
            FinanceToolRequestMapper.TryMap(
                "getExceptionDetails",
                GuidArgs(("exceptionId", SampleExceptionId)),
                out var request,
                out var error);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.True);
            Assert.That(error, Is.Null);
            Assert.That(request.ExceptionId, Is.EqualTo(SampleExceptionId));
        });
    }

    [Test]
    public void TryMap_GetExceptionDetails_DoesNotRequireRunId()
    {
        var success =
            FinanceToolRequestMapper.TryMap(
                "getExceptionDetails",
                GuidArgs(("exceptionId", SampleExceptionId)),
                out var request,
                out _);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.True);
            Assert.That(request.RunId, Is.Null);
        });
    }

    [Test]
    public void TryMap_GetExceptionDetails_WithMalformedExceptionId_StillFails()
    {
        var arguments =
            new Dictionary<string, JsonElement>
            {
                ["exceptionId"] =
                    JsonSerializer.Deserialize<JsonElement>("\"not-a-guid\"")
            };

        var success =
            FinanceToolRequestMapper.TryMap(
                "getExceptionDetails",
                arguments,
                out _,
                out var error);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.False);
            Assert.That(
                error,
                Is.EqualTo("Argument 'exceptionId' must be a valid GUID."));
        });
    }

    [Test]
    public void TryMap_GetExceptionDetails_WithMissingExceptionId_ReturnsStructuredError()
    {
        var success =
            FinanceToolRequestMapper.TryMap(
                "getExceptionDetails",
                new Dictionary<string, JsonElement>(),
                out _,
                out var error);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.False);
            Assert.That(
                error,
                Is.EqualTo("Required argument 'exceptionId' is missing."));
        });
    }

    [Test]
    public void TryMap_WithUnknownToolName_ImposesNoRequirements()
    {
        // Unreachable in production -- FinanceAssistantService checks
        // FinanceToolRegistry.TryGet before ever calling TryMap, so an
        // unrecognized tool name never reaches here. Verified directly
        // anyway: an unknown name must not crash or silently invent an
        // argument requirement it can't justify.
        var success =
            FinanceToolRequestMapper.TryMap(
                "deleteTransactions",
                new Dictionary<string, JsonElement>(),
                out var request,
                out var error);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.True);
            Assert.That(error, Is.Null);
            Assert.That(request.RunId, Is.Null);
            Assert.That(request.ExceptionId, Is.Null);
            Assert.That(request.ResultId, Is.Null);
        });
    }

    [Test]
    public void TryMap_OptionalArgumentSuppliedAnyway_IsStillValidatedIfMalformed()
    {
        // getReconciliationSummary doesn't need exceptionId, but if a
        // model supplies one anyway it must still be a valid GUID or null
        // -- optional never means "ignore malformed input silently".
        var arguments =
            new Dictionary<string, JsonElement>
            {
                ["runId"] =
                    JsonSerializer.Deserialize<JsonElement>(
                        $"\"{SampleRunId}\""),

                ["exceptionId"] =
                    JsonSerializer.Deserialize<JsonElement>("\"not-a-guid\"")
            };

        var success =
            FinanceToolRequestMapper.TryMap(
                "getReconciliationSummary",
                arguments,
                out _,
                out var error);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.False);
            Assert.That(
                error,
                Is.EqualTo("Argument 'exceptionId' must be a valid GUID or null."));
        });
    }

    private static Dictionary<string, JsonElement> GuidArgs(
        params (string Name, Guid Value)[] values)
    {
        return values.ToDictionary(
            x => x.Name,
            x => JsonSerializer.Deserialize<JsonElement>($"\"{x.Value}\""));
    }
}
