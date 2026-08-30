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

    public PostgresIntegrationFixture()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                ConnectionEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Environment variable '{ConnectionEnvironmentVariable}' " +
                "is required for PostgreSQL integration tests. " +
                "Use a dedicated test database.");
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
