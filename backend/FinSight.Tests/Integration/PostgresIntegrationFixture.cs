using FinSight.Infrastructure;
using FinSight.Infrastructure.Authentication;
using FinSight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.Tests.Integration;

public sealed class PostgresIntegrationFixture
{
    private const string ConnectionEnvironmentVariable =
        "FINSIGHT_TEST_CONNECTION";

    private readonly ServiceProvider _serviceProvider;

    /// <summary>
    /// Whether a dedicated test database has been configured for this run.
    /// </summary>
    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable(
                ConnectionEnvironmentVariable));

    public PostgresIntegrationFixture()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                ConnectionEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Ignore, not fail.
            //
            // These tests need a real PostgreSQL database that the fixture
            // drops and re-migrates, so they cannot run on a machine that
            // has not opted in by pointing FINSIGHT_TEST_CONNECTION at a
            // throwaway database. That is a missing *environment*, not a
            // product regression -- reporting it as a failure made a
            // healthy checkout look broken to anyone running a plain
            // `dotnet test`.
            //
            // Thrown from [OneTimeSetUp], NUnit's IgnoreException marks
            // every test in the fixture Skipped with the reason below.
            // Nothing is silently swallowed: no assertion is bypassed, and
            // when the variable IS set these tests run exactly as before.
            Assert.Ignore(
                $"Skipped: {ConnectionEnvironmentVariable} is not configured. " +
                "Set it to a dedicated throwaway PostgreSQL database to run " +
                "the database-backed integration tests -- the fixture deletes " +
                "and re-migrates that database on every test.");
        }

        var configuration =
            new ConfigurationManager();

        configuration[
            "ConnectionStrings:FinSightDb"] =
            connectionString;

        // Global AI Provider Architecture Refactor moved provider
        // credentials from AI:Gemini:*/AI:OpenAI:*/AI:Nvidia:* to
        // AI:Providers:Gemini:*/etc. -- these must track that shape or
        // DI resolves Gemini as unconfigured, breaking every DB-backed
        // test that expects the default Gemini-first behavior.
        configuration[
            "AI:Providers:Gemini:ApiKey"] =
            "test-gemini-api-key";

        configuration[
            "AI:Providers:Gemini:Model"] =
            "gemini-2.5-flash";

        var services =
            new ServiceCollection();

        services.AddInfrastructure(
            configuration);

        // Integration tests do not exercise JWT authentication,
        // but infrastructure validates all registered services.
        // Provide deterministic test-only JWT options so DI can build.
        services.AddSingleton(
            new JwtOptions
            {
                Issuer = "FinSight.Tests",
                Audience = "FinSight.Tests.Client",
                SecretKey =
                    "FinSightTestsJwtSecretKey-ChangeOnlyForTests-1234567890",
                ExpirationMinutes = 60
            });

        _serviceProvider =
            services.BuildServiceProvider(
                new ServiceProviderOptions
                {
                    ValidateScopes = true,
                    ValidateOnBuild = true
                });
    }

    public async Task ResetDatabaseAsync(
        CancellationToken cancellationToken = default)
    {
        await using var scope =
            _serviceProvider.CreateAsyncScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        await dbContext.Database.EnsureDeletedAsync(
            cancellationToken);

        await dbContext.Database.MigrateAsync(
            cancellationToken);
    }

    public AsyncServiceScope CreateScope()
    {
        return _serviceProvider.CreateAsyncScope();
    }
}
