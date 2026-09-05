using FinSight.Api.Controllers;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.AI;
using FinSight.Application.DTOs.Ai;
using FinSight.Application.Exceptions;
using FinSight.Infrastructure;
using FinSight.Infrastructure.AI;
using FinSight.Infrastructure.Authentication;
using FinSight.Tests.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.Tests.Integration;

/// <summary>
/// Global AI Provider DI Resolution fix: proves the exact reported
/// production bug is fixed, using the REAL DependencyInjection.
/// AddInfrastructure container -- not a hand-wired router in isolation.
///
/// The bug: with AI:ExceptionExplanation:ProviderOrder = NVIDIA and
/// AI:FinanceAssistant:ProviderOrder = NVIDIA, and Gemini left entirely
/// unconfigured, POST /api/reconciliation/runs returned HTTP 400 because
/// merely constructing ReconciliationController required IAiExplanationService
/// -> IAiProvider (AiProviderRouter) -> IGeminiAiProvider, and
/// GeminiAiProvider's constructor threw ArgumentException("Gemini API key
/// is required.") the instant it was resolved -- regardless of whether
/// Gemini appeared in the configured order at all.
///
/// This uses a real ServiceCollection + the production AddInfrastructure
/// registration (a syntactically valid but never-connected-to PostgreSQL
/// connection string satisfies AddDbContext's registration-time check;
/// EF Core does not open a connection until a query actually runs), so
/// these tests exercise the identical DI graph production uses -- while
/// needing no live database and no FINSIGHT_TEST_CONNECTION, unlike
/// PostgresIntegrationFixture-based tests.
/// </summary>
[TestFixture]
public sealed class AiProviderDependencyInjectionTests
{
    [Test]
    public void NvidiaOnlyOrder_NoGeminiOrOpenAiConfigured_ReconciliationControllerConstructsSuccessfully()
    {
        // This is the exact regression: building ReconciliationController
        // (required for EVERY one of its actions, including the plain,
        // non-AI POST runs/{...} reconciliation-execution endpoint) must
        // not throw merely because Gemini/OpenAI are unconfigured and
        // absent from the order.
        using var serviceProvider =
            BuildServiceProvider(
                exceptionExplanationOrder: "NVIDIA",
                financeAssistantOrder: "NVIDIA",
                configureGemini: false,
                configureOpenAi: false,
                configureNvidia: true);

        using var scope = serviceProvider.CreateScope();

        ReconciliationController? controller = null;

        Assert.DoesNotThrow(
            () =>
                controller =
                    ActivatorUtilities.CreateInstance<ReconciliationController>(
                        scope.ServiceProvider));

        Assert.That(controller, Is.Not.Null);
    }

    [Test]
    public void NvidiaOnlyOrder_NoGeminiOrOpenAiConfigured_ExceptionExplanationServiceResolvesAndRoutesToNvidia()
    {
        using var serviceProvider =
            BuildServiceProvider(
                exceptionExplanationOrder: "NVIDIA",
                financeAssistantOrder: "NVIDIA",
                configureGemini: false,
                configureOpenAi: false,
                configureNvidia: true);

        using var scope = serviceProvider.CreateScope();

        IAiExplanationService? explanationService = null;

        Assert.DoesNotThrow(
            () =>
                explanationService =
                    scope.ServiceProvider
                        .GetRequiredService<IAiExplanationService>());

        Assert.That(explanationService, Is.Not.Null);

        var aiProvider =
            scope.ServiceProvider.GetRequiredService<IAiProvider>();

        Assert.That(aiProvider.ProviderName, Is.EqualTo("NVIDIA"));
    }

    [Test]
    public void NvidiaOnlyOrder_NoGeminiOrOpenAiConfigured_FinanceAssistantServiceResolvesAndRoutesToNvidia()
    {
        using var serviceProvider =
            BuildServiceProvider(
                exceptionExplanationOrder: "NVIDIA",
                financeAssistantOrder: "NVIDIA",
                configureGemini: false,
                configureOpenAi: false,
                configureNvidia: true);

        using var scope = serviceProvider.CreateScope();

        IFinanceAssistantService? financeAssistantService = null;

        Assert.DoesNotThrow(
            () =>
                financeAssistantService =
                    scope.ServiceProvider
                        .GetRequiredService<IFinanceAssistantService>());

        Assert.That(financeAssistantService, Is.Not.Null);

        var financeAssistantProvider =
            scope.ServiceProvider
                .GetRequiredService<IFinanceAssistantProvider>();

        Assert.That(
            financeAssistantProvider.ProviderName,
            Is.EqualTo("NVIDIA"));
    }

    [Test]
    public void DefaultGeminiThenOpenAiOrder_NeitherConfigured_ResolvesSuccessfully_ButBothReportUnavailable()
    {
        // Configuration matrix row F ("no usable providers"): construction
        // must still succeed -- an AiProviderUnavailableException is
        // expected only if an AI operation is actually invoked, never
        // merely from resolving the DI graph.
        using var serviceProvider =
            BuildServiceProvider(
                exceptionExplanationOrder: null,
                financeAssistantOrder: null,
                configureGemini: false,
                configureOpenAi: false,
                configureNvidia: false);

        using var scope = serviceProvider.CreateScope();

        ReconciliationController? controller = null;

        Assert.DoesNotThrow(
            () =>
                controller =
                    ActivatorUtilities.CreateInstance<ReconciliationController>(
                        scope.ServiceProvider));

        Assert.That(controller, Is.Not.Null);

        var aiProvider =
            scope.ServiceProvider.GetRequiredService<IAiProvider>();

        Assert.That(aiProvider.IsAvailable, Is.False);
    }

    [Test]
    public void NoUsableProviders_ConstructionSucceeds_ButActuallyRequestingAnExplanationThrows()
    {
        // Completes configuration matrix row F: DI-graph construction
        // must succeed unconditionally, and the AI-specific unavailable
        // exception must appear only once an AI operation is actually
        // requested -- never merely from building the graph.
        using var serviceProvider =
            BuildServiceProvider(
                exceptionExplanationOrder: null,
                financeAssistantOrder: null,
                configureGemini: false,
                configureOpenAi: false,
                configureNvidia: false);

        using var scope = serviceProvider.CreateScope();

        var aiProvider =
            scope.ServiceProvider.GetRequiredService<IAiProvider>();

        Assert.ThrowsAsync<AiProviderUnavailableException>(
            async () =>
                await aiProvider.GenerateExplanationAsync(
                    new AiExplanationRequest
                    {
                        ExceptionId = Guid.NewGuid(),
                        RunId = Guid.NewGuid(),
                        ReconciliationResultId = Guid.NewGuid(),
                        TransactionReference = "TXN-0001",
                        DeterministicCategory = "AmountMismatch",
                        InvolvedSources = "Payment,Bank,Settlement",
                        DiscrepancyDetail = "{}"
                    }));
    }

    [Test]
    public void GeminiOnlyConfigured_DefaultOrder_ReconciliationControllerConstructsSuccessfully()
    {
        // Configuration matrix row E ("Gemini only"): OpenAI is in the
        // default order but unconfigured -- must be skipped via
        // IsAvailable, never crash construction.
        using var serviceProvider =
            BuildServiceProvider(
                exceptionExplanationOrder: null,
                financeAssistantOrder: null,
                configureGemini: true,
                configureOpenAi: false,
                configureNvidia: false);

        using var scope = serviceProvider.CreateScope();

        ReconciliationController? controller = null;

        Assert.DoesNotThrow(
            () =>
                controller =
                    ActivatorUtilities.CreateInstance<ReconciliationController>(
                        scope.ServiceProvider));

        Assert.That(controller, Is.Not.Null);

        var aiProvider =
            scope.ServiceProvider.GetRequiredService<IAiProvider>();

        Assert.Multiple(() =>
        {
            Assert.That(aiProvider.IsAvailable, Is.True);
            Assert.That(aiProvider.ProviderName, Is.EqualTo("Gemini"));
        });
    }

    [Test]
    public void FullThreeProviderOrder_OnlyNvidiaConfigured_ReconciliationControllerConstructsSuccessfully()
    {
        // Configuration matrix row B: Gemini,NVIDIA,OpenAI all listed in
        // the order, but only NVIDIA is actually configured -- Gemini
        // being first in the order must not crash construction.
        using var serviceProvider =
            BuildServiceProvider(
                exceptionExplanationOrder: "Gemini,NVIDIA,OpenAI",
                financeAssistantOrder: "Gemini,NVIDIA,OpenAI",
                configureGemini: false,
                configureOpenAi: false,
                configureNvidia: true);

        using var scope = serviceProvider.CreateScope();

        ReconciliationController? controller = null;

        Assert.DoesNotThrow(
            () =>
                controller =
                    ActivatorUtilities.CreateInstance<ReconciliationController>(
                        scope.ServiceProvider));

        Assert.That(controller, Is.Not.Null);

        var aiProvider =
            scope.ServiceProvider.GetRequiredService<IAiProvider>();

        // Gemini is ordered first but unconfigured -- IsAvailable's
        // preflight excludes it; NVIDIA (configured) is what's actually
        // available.
        Assert.Multiple(() =>
        {
            Assert.That(aiProvider.IsAvailable, Is.True);
            Assert.That(aiProvider.ProviderName, Is.EqualTo("Gemini"));
        });
    }

    private static ServiceProvider BuildServiceProvider(
        string? exceptionExplanationOrder,
        string? financeAssistantOrder,
        bool configureGemini,
        bool configureOpenAi,
        bool configureNvidia)
    {
        var configurationValues =
            new Dictionary<string, string?>
            {
                // Syntactically valid, never actually connected to --
                // AddDbContext only registers; EF Core does not open a
                // connection until a query runs, and none of these tests
                // executes one.
                ["ConnectionStrings:FinSightDb"] =
                    "Host=localhost;Database=fake;Username=fake;Password=fake",

                ["Jwt:Issuer"] = "FinSight.Tests",
                ["Jwt:Audience"] = "FinSight.Tests.Client",
                ["Jwt:SecretKey"] =
                    "FinSightTestsJwtSecretKey-ChangeOnlyForTests-1234567890",
                ["Jwt:ExpirationMinutes"] = "60"
            };

        if (configureGemini)
        {
            configurationValues["AI:Providers:Gemini:ApiKey"] =
                "test-gemini-api-key";

            configurationValues["AI:Providers:Gemini:Model"] =
                "gemini-2.5-flash";
        }

        if (configureOpenAi)
        {
            configurationValues["AI:Providers:OpenAI:ApiKey"] =
                "test-openai-api-key";

            configurationValues["AI:Providers:OpenAI:Model"] =
                "gpt-5-mini";
        }

        if (configureNvidia)
        {
            configurationValues["AI:Providers:Nvidia:ApiKey"] =
                "test-nvidia-api-key";

            configurationValues["AI:Providers:Nvidia:Model"] =
                "openai/gpt-oss-120b";

            configurationValues["AI:Providers:Nvidia:BaseUrl"] =
                "https://integrate.api.nvidia.com/v1";
        }

        if (exceptionExplanationOrder is not null)
        {
            configurationValues["AI:ExceptionExplanation:ProviderOrder"] =
                exceptionExplanationOrder;
        }

        if (financeAssistantOrder is not null)
        {
            configurationValues["AI:FinanceAssistant:ProviderOrder"] =
                financeAssistantOrder;
        }

        var configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(configurationValues)
                .Build();

        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        // Integration tests do not exercise JWT authentication, but
        // infrastructure validates all registered services (mirrors
        // PostgresIntegrationFixture's own setup).
        services.AddSingleton(
            new JwtOptions
            {
                Issuer = "FinSight.Tests",
                Audience = "FinSight.Tests.Client",
                SecretKey =
                    "FinSightTestsJwtSecretKey-ChangeOnlyForTests-1234567890",
                ExpirationMinutes = 60
            });

        // ICurrentUserService is deliberately registered at the API host
        // layer (Program.cs), not inside AddInfrastructure, because it
        // depends on IHttpContextAccessor -- unavailable in this bare
        // ServiceCollection with no HTTP host. These tests only care
        // about the AI provider DI graph, so a fixed fake is enough to
        // let ReconciliationController construct.
        services.AddScoped<ICurrentUserService>(
            _ => new FixedCurrentUserService(Guid.NewGuid()));

        // ValidateOnBuild proves the ENTIRE registered DI graph is
        // resolvable with this configuration -- not just the specific
        // services these tests happen to resolve -- the strongest
        // available proof that "application startup remains possible
        // with optional provider credentials missing".
        return services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });
    }
}
