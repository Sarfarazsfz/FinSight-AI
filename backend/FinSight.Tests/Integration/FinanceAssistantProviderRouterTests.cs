using FinSight.Application.AI;
using FinSight.Application.Exceptions;
using FinSight.Infrastructure.AI;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FinSight.Tests.Integration;

/// <summary>
/// Phase F10 (NVIDIA provider addition): FinanceAssistantProviderRouter
/// generalized from a hardcoded Gemini/OpenAI binary choice to an ordered
/// N-provider chain (AiProviderOptions.FinanceAssistantProviderOrder).
/// These tests cover both the pre-existing 2-provider behavior (still
/// exercised via a default ["Gemini","OpenAI"] order, with NVIDIA
/// registered-but-absent-from-order to prove it's never touched) and the
/// new 3-provider chain scenarios.
/// </summary>
[TestFixture]
public sealed class FinanceAssistantProviderRouterTests
{
    [Test]
    public async Task GeminiSuccess_ReturnsGeminiResponse_NvidiaAndOpenAiNotCalled()
    {
        var gemini = SucceedingProvider("Gemini", "Gemini answer");
        var nvidia = SucceedingProvider("NVIDIA", "NVIDIA answer");
        var openAi = SucceedingProvider("OpenAI", "OpenAI answer");

        var router =
            CreateRouter(
                gemini,
                nvidia,
                openAi,
                order: new[] { "Gemini", "NVIDIA", "OpenAI" },
                fallbackEnabled: true);

        var response =
            await router.AskAsync(CreateRequest());

        Assert.Multiple(() =>
        {
            Assert.That(response.Answer, Is.EqualTo("Gemini answer"));
            Assert.That(gemini.CallCount, Is.EqualTo(1));
            Assert.That(nvidia.CallCount, Is.EqualTo(0));
            Assert.That(openAi.CallCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task GeminiFailure_NvidiaIsAttempted()
    {
        var gemini = FailingProvider("Gemini", "Gemini unavailable");
        var nvidia = SucceedingProvider("NVIDIA", "NVIDIA answer");
        var openAi = SucceedingProvider("OpenAI", "OpenAI answer");

        var router =
            CreateRouter(
                gemini,
                nvidia,
                openAi,
                order: new[] { "Gemini", "NVIDIA", "OpenAI" },
                fallbackEnabled: true);

        var response =
            await router.AskAsync(CreateRequest());

        Assert.Multiple(() =>
        {
            Assert.That(response.Answer, Is.EqualTo("NVIDIA answer"));
            Assert.That(gemini.CallCount, Is.EqualTo(1));
            Assert.That(nvidia.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GeminiFailure_NvidiaSuccess_OpenAiNotCalled()
    {
        var gemini = FailingProvider("Gemini", "Gemini unavailable");
        var nvidia = SucceedingProvider("NVIDIA", "NVIDIA answer");
        var openAi = SucceedingProvider("OpenAI", "Should not be used");

        var router =
            CreateRouter(
                gemini,
                nvidia,
                openAi,
                order: new[] { "Gemini", "NVIDIA", "OpenAI" },
                fallbackEnabled: true);

        var response =
            await router.AskAsync(CreateRequest());

        Assert.Multiple(() =>
        {
            Assert.That(response.Answer, Is.EqualTo("NVIDIA answer"));
            Assert.That(openAi.CallCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task GeminiFailure_NvidiaFailure_OpenAiIsAttempted()
    {
        var gemini = FailingProvider("Gemini", "Gemini unavailable");
        var nvidia = FailingProvider("NVIDIA", "NVIDIA unavailable");
        var openAi = SucceedingProvider("OpenAI", "OpenAI fallback answer");

        var router =
            CreateRouter(
                gemini,
                nvidia,
                openAi,
                order: new[] { "Gemini", "NVIDIA", "OpenAI" },
                fallbackEnabled: true);

        var response =
            await router.AskAsync(CreateRequest());

        Assert.Multiple(() =>
        {
            Assert.That(response.Answer, Is.EqualTo("OpenAI fallback answer"));
            Assert.That(gemini.CallCount, Is.EqualTo(1));
            Assert.That(nvidia.CallCount, Is.EqualTo(1));
            Assert.That(openAi.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void AllThreeProvidersFail_ThrowsProviderCountAgnosticUnavailableException()
    {
        var gemini = FailingProvider("Gemini", "Gemini failed");
        var nvidia = FailingProvider("NVIDIA", "NVIDIA failed");
        var openAi = FailingProvider("OpenAI", "OpenAI failed");

        var router =
            CreateRouter(
                gemini,
                nvidia,
                openAi,
                order: new[] { "Gemini", "NVIDIA", "OpenAI" },
                fallbackEnabled: true);

        var exception =
            Assert.ThrowsAsync<FinanceAssistantProviderUnavailableException>(
                async () =>
                    await router.AskAsync(CreateRequest()));

        Assert.Multiple(() =>
        {
            // Provider-count-agnostic wording -- never hardcodes "Both".
            Assert.That(
                exception!.Message,
                Is.EqualTo(
                    "All 3 configured Finance Assistant AI providers failed."));

            Assert.That(
                exception.InnerException,
                Is.TypeOf<AggregateException>());

            var aggregate =
                (AggregateException)exception.InnerException!;

            Assert.That(aggregate.InnerExceptions.Count, Is.EqualTo(3));

            // No provider called more than once -- no retry loop.
            Assert.That(gemini.CallCount, Is.EqualTo(1));
            Assert.That(nvidia.CallCount, Is.EqualTo(1));
            Assert.That(openAi.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task FallbackDisabled_OnlyPrimaryProviderIsAttempted()
    {
        var gemini = FailingProvider("Gemini", "Gemini unavailable");
        var nvidia = SucceedingProvider("NVIDIA", "Should not be used");
        var openAi = SucceedingProvider("OpenAI", "Should not be used");

        var router =
            CreateRouter(
                gemini,
                nvidia,
                openAi,
                order: new[] { "Gemini", "NVIDIA", "OpenAI" },
                fallbackEnabled: false);

        var exception =
            Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                    await router.AskAsync(CreateRequest()));

        Assert.Multiple(() =>
        {
            Assert.That(
                exception!.Message,
                Does.Contain("Finance Assistant provider 'Gemini' failed."));

            // Never a FinanceAssistantProviderUnavailableException here --
            // this is the single-provider-failed shape, unchanged from
            // before NVIDIA existed.
            Assert.That(
                exception,
                Is.Not.InstanceOf<FinanceAssistantProviderUnavailableException>());

            Assert.That(gemini.CallCount, Is.EqualTo(1));
            Assert.That(nvidia.CallCount, Is.EqualTo(0));
            Assert.That(openAi.CallCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void OperationCanceled_ImmediatelyRethrows_DoesNotContinueChain()
    {
        var gemini =
            new FakeFinanceAssistantProvider(
                "Gemini",
                _ => Task.FromException<FinanceAssistantProviderResponse>(
                    new OperationCanceledException()));

        var nvidia = SucceedingProvider("NVIDIA", "Should not be used");
        var openAi = SucceedingProvider("OpenAI", "Should not be used");

        var router =
            CreateRouter(
                gemini,
                nvidia,
                openAi,
                order: new[] { "Gemini", "NVIDIA", "OpenAI" },
                fallbackEnabled: true);

        Assert.ThrowsAsync<OperationCanceledException>(
            async () =>
                await router.AskAsync(CreateRequest()));

        Assert.Multiple(() =>
        {
            Assert.That(gemini.CallCount, Is.EqualTo(1));
            Assert.That(nvidia.CallCount, Is.EqualTo(0));
            Assert.That(openAi.CallCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task ProviderOrder_IsRespectedExactly_NotHardcodedToGeminiFirst()
    {
        // A deliberately non-default order proves the router is genuinely
        // list-driven, not secretly still Gemini-first.
        var gemini = SucceedingProvider("Gemini", "Should not be used");
        var nvidia = FailingProvider("NVIDIA", "NVIDIA unavailable");
        var openAi = SucceedingProvider("OpenAI", "Should not be used");

        var router =
            CreateRouter(
                gemini,
                nvidia,
                openAi,
                order: new[] { "NVIDIA", "Gemini", "OpenAI" },
                fallbackEnabled: true);

        var response =
            await router.AskAsync(CreateRequest());

        Assert.Multiple(() =>
        {
            // NVIDIA (first in the configured order) was tried first and
            // failed; Gemini (second) was then tried and used -- OpenAI
            // (third) was never needed.
            Assert.That(response.Answer, Is.EqualTo("Should not be used"));
            Assert.That(nvidia.CallCount, Is.EqualTo(1));
            Assert.That(gemini.CallCount, Is.EqualTo(1));
            Assert.That(openAi.CallCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void EmptyProviderOrder_ThrowsProviderUnavailable_WithoutCallingAnyProvider()
    {
        var gemini = SucceedingProvider("Gemini", "Should not be used");
        var nvidia = SucceedingProvider("NVIDIA", "Should not be used");
        var openAi = SucceedingProvider("OpenAI", "Should not be used");

        var router =
            CreateRouter(
                gemini,
                nvidia,
                openAi,
                order: Array.Empty<string>(),
                fallbackEnabled: true);

        var exception =
            Assert.ThrowsAsync<FinanceAssistantProviderUnavailableException>(
                async () =>
                    await router.AskAsync(CreateRequest()));

        Assert.Multiple(() =>
        {
            Assert.That(
                exception!.Message,
                Is.EqualTo("No Finance Assistant provider is configured."));

            Assert.That(gemini.CallCount, Is.EqualTo(0));
            Assert.That(nvidia.CallCount, Is.EqualTo(0));
            Assert.That(openAi.CallCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task DefaultTwoProviderOrder_NvidiaRegisteredButNotInOrder_IsNeverCalled()
    {
        // Regression proof for existing deployments with no NVIDIA
        // configuration: the default order is exactly ["Gemini","OpenAI"],
        // so a registered-but-unconfigured NVIDIA provider is never
        // touched even though it exists as a DI-resolved instance.
        var gemini = FailingProvider("Gemini", "Gemini unavailable");
        var nvidia = SucceedingProvider("NVIDIA", "Should never be called");
        var openAi = SucceedingProvider("OpenAI", "OpenAI fallback answer");

        var router =
            CreateRouter(
                gemini,
                nvidia,
                openAi,
                order: new[] { "Gemini", "OpenAI" },
                fallbackEnabled: true);

        var response =
            await router.AskAsync(CreateRequest());

        Assert.Multiple(() =>
        {
            Assert.That(response.Answer, Is.EqualTo("OpenAI fallback answer"));
            Assert.That(nvidia.CallCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void AiProviderOptions_DefaultFinanceAssistantProviderOrder_IsGeminiThenOpenAi()
    {
        var options = new AiProviderOptions();

        Assert.That(
            options.FinanceAssistant.ProviderOrder,
            Is.EqualTo(new[] { "Gemini", "OpenAI" }));
    }

    [Test]
    public void Constructor_ProviderAbsentFromOrder_IsNeverConstructed()
    {
        // Global AI Provider DI Resolution fix: registers Gemini/OpenAI
        // as keyed FACTORIES (not pre-built instances) that increment a
        // counter when invoked -- proving the router never even asks DI
        // for a provider excluded from FinanceAssistant.ProviderOrder.
        // This is the F10-side sibling of the reported bug: Gemini's
        // GeminiFinanceAssistantProvider indirectly required
        // IFinanceAssistantModelClient, whose old registration threw
        // eagerly when Gemini's API key was absent.
        var geminiConstructions = 0;
        var openAiConstructions = 0;

        var services = new ServiceCollection();

        services.AddKeyedSingleton<IFinanceAssistantProvider>(
            "Gemini",
            (_, _) =>
            {
                geminiConstructions++;
                return SucceedingProvider("Gemini", "unused");
            });

        services.AddKeyedSingleton<IFinanceAssistantProvider>(
            "NVIDIA",
            SucceedingProvider("NVIDIA", "NVIDIA answer"));

        services.AddKeyedSingleton<IFinanceAssistantProvider>(
            "OpenAI",
            (_, _) =>
            {
                openAiConstructions++;
                return SucceedingProvider("OpenAI", "unused");
            });

        var serviceProvider = services.BuildServiceProvider();

        var options =
            new AiProviderOptions
            {
                FinanceAssistant =
                    new AiProviderOptions.SurfaceOptions
                    {
                        ProviderOrder = new[] { "NVIDIA" },
                        FallbackEnabled = true
                    }
            };

        var router =
            new FinanceAssistantProviderRouter(serviceProvider, options);

        Assert.Multiple(() =>
        {
            Assert.That(geminiConstructions, Is.EqualTo(0));
            Assert.That(openAiConstructions, Is.EqualTo(0));
            Assert.That(router.ProviderName, Is.EqualTo("NVIDIA"));
        });
    }

    [Test]
    public void Constructor_DisabledProvider_IsNeverConstructed_EvenWhenPresentInOrder()
    {
        var geminiConstructions = 0;

        var services = new ServiceCollection();

        services.AddKeyedSingleton<IFinanceAssistantProvider>(
            "Gemini",
            (_, _) =>
            {
                geminiConstructions++;
                return SucceedingProvider("Gemini", "unused");
            });

        services.AddKeyedSingleton<IFinanceAssistantProvider>(
            "NVIDIA",
            SucceedingProvider("NVIDIA", "NVIDIA answer"));

        services.AddKeyedSingleton<IFinanceAssistantProvider>(
            "OpenAI",
            SucceedingProvider("OpenAI", "unused"));

        var serviceProvider = services.BuildServiceProvider();

        var options =
            new AiProviderOptions
            {
                Providers =
                    new AiProviderOptions.ProvidersOptions
                    {
                        Gemini =
                            new AiProviderOptions.GeminiOptions
                            {
                                Enabled = false
                            }
                    },

                FinanceAssistant =
                    new AiProviderOptions.SurfaceOptions
                    {
                        // Gemini is present in the order but disabled --
                        // must still never be constructed.
                        ProviderOrder = new[] { "Gemini", "NVIDIA" },
                        FallbackEnabled = true
                    }
            };

        var router =
            new FinanceAssistantProviderRouter(serviceProvider, options);

        Assert.Multiple(() =>
        {
            Assert.That(geminiConstructions, Is.EqualTo(0));
            Assert.That(router.ProviderName, Is.EqualTo("NVIDIA"));
        });
    }

    private static FakeFinanceAssistantProvider SucceedingProvider(
        string providerName,
        string answer)
    {
        return new FakeFinanceAssistantProvider(
            providerName,
            _ => Task.FromResult(CreateResponse(answer)));
    }

    private static FakeFinanceAssistantProvider FailingProvider(
        string providerName,
        string failureMessage)
    {
        return new FakeFinanceAssistantProvider(
            providerName,
            _ => Task.FromException<FinanceAssistantProviderResponse>(
                new InvalidOperationException(failureMessage)));
    }

    private static FinanceAssistantProviderRouter CreateRouter(
        IFinanceAssistantProvider gemini,
        IFinanceAssistantProvider nvidia,
        IFinanceAssistantProvider openAi,
        IReadOnlyList<string> order,
        bool fallbackEnabled)
    {
        var options =
            new AiProviderOptions
            {
                FinanceAssistant =
                    new AiProviderOptions.SurfaceOptions
                    {
                        ProviderOrder = order,
                        FallbackEnabled = fallbackEnabled
                    }
            };

        // FinanceAssistantProviderRouter resolves providers by name from
        // keyed DI (Global AI Provider DI Resolution fix) -- see
        // AiProviderRouterTests.CreateRouter's identical comment.
        var services = new ServiceCollection();

        services.AddKeyedSingleton<IFinanceAssistantProvider>(
            "Gemini", gemini);

        services.AddKeyedSingleton<IFinanceAssistantProvider>(
            "NVIDIA", nvidia);

        services.AddKeyedSingleton<IFinanceAssistantProvider>(
            "OpenAI", openAi);

        var serviceProvider = services.BuildServiceProvider();

        return new FinanceAssistantProviderRouter(
            serviceProvider,
            options);
    }

    private static FinanceAssistantProviderRequest CreateRequest()
    {
        return new FinanceAssistantProviderRequest
        {
            RunId =
                Guid.NewGuid(),

            Question =
                "Analyze the reconciliation.",

            Tools =
                Array.Empty<FinanceToolDefinition>(),

            PreviousToolCalls =
                Array.Empty<FinanceToolCall>(),

            ToolResults =
                Array.Empty<FinanceToolResultMessage>()
        };
    }

    private static FinanceAssistantProviderResponse CreateResponse(
        string answer)
    {
        return new FinanceAssistantProviderResponse
        {
            Answer =
                answer,

            ToolCalls =
                Array.Empty<FinanceToolCall>(),

            RequiresToolExecution =
                false
        };
    }

    private sealed class FakeFinanceAssistantProvider
        : IFinanceAssistantProvider
    {
        private readonly Func<
            FinanceAssistantProviderRequest,
            Task<FinanceAssistantProviderResponse>>
            _handler;

        public FakeFinanceAssistantProvider(
            string providerName,
            Func<
                FinanceAssistantProviderRequest,
                Task<FinanceAssistantProviderResponse>>
                handler)
        {
            ProviderName =
                providerName;

            _handler =
                handler;
        }

        public string ProviderName {
            get;
        }

        public int CallCount {
            get;
            private set;
        }

        public async Task<FinanceAssistantProviderResponse> AskAsync(
            FinanceAssistantProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            cancellationToken.ThrowIfCancellationRequested();

            return await _handler(request);
        }
    }
}
