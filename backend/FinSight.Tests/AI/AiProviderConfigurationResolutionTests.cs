using FinSight.Infrastructure;
using FinSight.Infrastructure.AI;
using Microsoft.Extensions.Configuration;

namespace FinSight.Tests.AI;

/// <summary>
/// Global AI Provider Architecture Refactor: the legacy-configuration
/// translation (AI:DefaultProvider + AI:FallbackEnabled -> an equivalent
/// ExceptionExplanation.ProviderOrder/FallbackEnabled, and the new
/// FinanceAssistant surface falling back to the same legacy
/// AI:FallbackEnabled when its own key is absent) lives in
/// DependencyInjection's ResolveExceptionExplanationOptions/
/// ResolveFinanceAssistantOptions helpers. Those are exercised directly
/// here (via [InternalsVisibleTo], see AssemblyInfo.cs) against a real
/// in-memory IConfiguration, rather than through a full DI container
/// build -- avoiding any dependency on a database connection string.
///
/// AiProviderRouterTests/FinanceAssistantProviderRouterTests separately
/// prove the routers behave correctly given an already-resolved
/// SurfaceOptions; these tests prove the resolution step itself.
/// </summary>
[TestFixture]
public sealed class AiProviderConfigurationResolutionTests
{
    [Test]
    public void ExceptionExplanation_NewKeyPresent_TakesPrecedenceOverLegacy()
    {
        var configuration =
            BuildConfiguration(
                new Dictionary<string, string?>
                {
                    ["AI:DefaultProvider"] = "OpenAI",
                    ["AI:FallbackEnabled"] = "false",
                    ["AI:ExceptionExplanation:ProviderOrder:0"] = "NVIDIA",
                    ["AI:ExceptionExplanation:ProviderOrder:1"] = "Gemini",
                    ["AI:ExceptionExplanation:FallbackEnabled"] = "true"
                });

        var resolved =
            DependencyInjection.ResolveExceptionExplanationOptions(
                configuration);

        Assert.Multiple(() =>
        {
            Assert.That(
                resolved.ProviderOrder,
                Is.EqualTo(new[] { "NVIDIA", "Gemini" }));

            Assert.That(resolved.FallbackEnabled, Is.True);
        });
    }

    [Test]
    public void ExceptionExplanation_NewKeyAbsent_LegacyDefaultGemini_TranslatesToGeminiThenOpenAi()
    {
        var configuration =
            BuildConfiguration(
                new Dictionary<string, string?>
                {
                    ["AI:DefaultProvider"] = "Gemini",
                    ["AI:FallbackEnabled"] = "true"
                });

        var resolved =
            DependencyInjection.ResolveExceptionExplanationOptions(
                configuration);

        Assert.Multiple(() =>
        {
            Assert.That(
                resolved.ProviderOrder,
                Is.EqualTo(new[] { "Gemini", "OpenAI" }));

            Assert.That(resolved.FallbackEnabled, Is.True);
        });
    }

    [Test]
    public void ExceptionExplanation_NewKeyAbsent_LegacyDefaultOpenAi_TranslatesToOpenAiThenGemini()
    {
        var configuration =
            BuildConfiguration(
                new Dictionary<string, string?>
                {
                    ["AI:DefaultProvider"] = "OpenAI",
                    ["AI:FallbackEnabled"] = "true"
                });

        var resolved =
            DependencyInjection.ResolveExceptionExplanationOptions(
                configuration);

        Assert.That(
            resolved.ProviderOrder,
            Is.EqualTo(new[] { "OpenAI", "Gemini" }));
    }

    [Test]
    public void ExceptionExplanation_LegacyFallbackDisabled_TranslatesToSingleEntryOrder()
    {
        var configuration =
            BuildConfiguration(
                new Dictionary<string, string?>
                {
                    ["AI:DefaultProvider"] = "OpenAI",
                    ["AI:FallbackEnabled"] = "false"
                });

        var resolved =
            DependencyInjection.ResolveExceptionExplanationOptions(
                configuration);

        Assert.Multiple(() =>
        {
            Assert.That(
                resolved.ProviderOrder,
                Is.EqualTo(new[] { "OpenAI" }));

            Assert.That(resolved.FallbackEnabled, Is.False);
        });
    }

    [Test]
    public void ExceptionExplanation_LegacyUnrecognizedDefaultProvider_TranslatesToEmptyOrder()
    {
        // Reproduces the pre-refactor UnsupportedProvider_Throws scenario:
        // an unrecognized AI:DefaultProvider value resolves to an empty
        // order, which AiProviderRouter turns into
        // "No configured AI provider is available."
        var configuration =
            BuildConfiguration(
                new Dictionary<string, string?>
                {
                    ["AI:DefaultProvider"] = "SomethingElse",
                    ["AI:FallbackEnabled"] = "true"
                });

        var resolved =
            DependencyInjection.ResolveExceptionExplanationOptions(
                configuration);

        Assert.That(resolved.ProviderOrder, Is.Empty);
    }

    [Test]
    public void ExceptionExplanation_NothingConfigured_DefaultsToGeminiThenOpenAi()
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>());

        var resolved =
            DependencyInjection.ResolveExceptionExplanationOptions(
                configuration);

        Assert.Multiple(() =>
        {
            Assert.That(
                resolved.ProviderOrder,
                Is.EqualTo(new[] { "Gemini", "OpenAI" }));

            Assert.That(resolved.FallbackEnabled, Is.True);
        });
    }

    [Test]
    public void FinanceAssistant_NewKeyPresent_TakesPrecedence()
    {
        var configuration =
            BuildConfiguration(
                new Dictionary<string, string?>
                {
                    ["AI:FinanceAssistant:ProviderOrder"] = "NVIDIA,OpenAI",
                    ["AI:FinanceAssistant:FallbackEnabled"] = "false"
                });

        var resolved =
            DependencyInjection.ResolveFinanceAssistantOptions(
                configuration);

        Assert.Multiple(() =>
        {
            Assert.That(
                resolved.ProviderOrder,
                Is.EqualTo(new[] { "NVIDIA", "OpenAI" }));

            Assert.That(resolved.FallbackEnabled, Is.False);
        });
    }

    [Test]
    public void FinanceAssistant_OwnFallbackKeyAbsent_FallsBackToLegacySharedFlatKey()
    {
        // F10 previously shared AI:FallbackEnabled with F9 before this
        // refactor gave it its own AI:FinanceAssistant:FallbackEnabled key
        // -- an existing deployment that only set the old flat key must
        // keep behaving the same way for F10 too.
        var configuration =
            BuildConfiguration(
                new Dictionary<string, string?>
                {
                    ["AI:FallbackEnabled"] = "false"
                });

        var resolved =
            DependencyInjection.ResolveFinanceAssistantOptions(
                configuration);

        Assert.That(resolved.FallbackEnabled, Is.False);
    }

    [Test]
    public void FinanceAssistant_NothingConfigured_DefaultsToGeminiThenOpenAiWithFallbackEnabled()
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>());

        var resolved =
            DependencyInjection.ResolveFinanceAssistantOptions(
                configuration);

        Assert.Multiple(() =>
        {
            Assert.That(
                resolved.ProviderOrder,
                Is.EqualTo(new[] { "Gemini", "OpenAI" }));

            Assert.That(resolved.FallbackEnabled, Is.True);
        });
    }

    private static IConfiguration BuildConfiguration(
        Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
