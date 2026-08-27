using FinSight.Application.AI;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.Tests.Integration;

[TestFixture]
[NonParallelizable]
public sealed class FinanceToolRegistryTests
{
    private PostgresIntegrationFixture _fixture = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _fixture = new PostgresIntegrationFixture();
    }

    [Test]
    public async Task Registry_ExposesExactlyFourAllowedTools()
    {
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var registry =
            scope.ServiceProvider
                .GetRequiredService<IFinanceToolRegistry>();

        var names =
            registry.ToolNames
                .OrderBy(x => x)
                .ToArray();

        Assert.That(
            names,
            Is.EqualTo(
                new[]
                {
                    "getExceptionDetails",
                    "getReconciliationSummary",
                    "getTransactionDetails",
                    "getUnmatchedRecords"
                }));
    }

    [Test]
    public async Task Registry_ResolvesEveryAllowedTool()
    {
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var registry =
            scope.ServiceProvider
                .GetRequiredService<IFinanceToolRegistry>();

        foreach (var toolName in new[]
        {
            "getReconciliationSummary",
            "getUnmatchedRecords",
            "getTransactionDetails",
            "getExceptionDetails"
        })
        {
            var found =
                registry.TryGet(
                    toolName,
                    out var tool);

            Assert.Multiple(() =>
            {
                Assert.That(found, Is.True);
                Assert.That(tool, Is.Not.Null);
                Assert.That(
                    tool!.Name,
                    Is.EqualTo(toolName));
            });
        }
    }

    [Test]
    public async Task Registry_RejectsUnknownAndBlankToolNames()
    {
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var registry =
            scope.ServiceProvider
                .GetRequiredService<IFinanceToolRegistry>();

        Assert.Multiple(() =>
        {
            Assert.That(
                registry.TryGet(
                    "deleteTransactions",
                    out _),
                Is.False);

            Assert.That(
                registry.TryGet(
                    "executePayment",
                    out _),
                Is.False);

            Assert.That(
                registry.TryGet(
                    "",
                    out _),
                Is.False);

            Assert.That(
                registry.TryGet(
                    "   ",
                    out _),
                Is.False);

            Assert.That(
                registry.TryGet(
                    null!,
                    out _),
                Is.False);
        });
    }
}
