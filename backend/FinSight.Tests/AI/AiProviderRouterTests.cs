using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Ai;
using FinSight.Application.Exceptions;
using FinSight.Infrastructure.AI;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.Tests.AI;

/// <summary>
/// Global AI Provider Architecture Refactor: AiProviderRouter (F9) now
/// takes AiProviderOptions.ExceptionExplanation (a resolved ProviderOrder +
/// FallbackEnabled pair) directly -- legacy AI:DefaultProvider/
/// AI:FallbackEnabled translation happens once, in DependencyInjection,
/// and is covered separately in AiProviderConfigurationResolutionTests.
/// These tests exercise the router's own behavior given an
/// already-resolved order: preflight exclusion via IsAvailable, fallback
/// ordering, cancellation, and all four legacy message shapes, now
/// extended to a genuine third (NVIDIA) provider.
/// </summary>
[TestFixture]
public sealed class AiProviderRouterTests
{
    [Test]
    public async Task GeminiFirstInOrder_UsesGemini()
    {
        var gemini =
            new FakeGeminiProvider(
                isAvailable: true,
                responseText: "Gemini response.");

        var nvidia =
            new FakeNvidiaProvider(
                isAvailable: true,
                responseText: "NVIDIA response.");

        var openAi =
            new FakeOpenAiProvider(
                isAvailable: true,
                responseText: "OpenAI response.");

        var router =
            CreateRouter(
                gemini,
                nvidia,
                openAi,
                order: new[] { "Gemini", "OpenAI" },
                fallbackEnabled: true);

        var result =
            await router.GenerateExplanationAsync(
                CreateRequest());

        Assert.Multiple(() =>
        {
            Assert.That(result.Provider, Is.EqualTo("Gemini"));
            Assert.That(gemini.Calls, Is.EqualTo(1));
            Assert.That(nvidia.Calls, Is.EqualTo(0));
            Assert.That(openAi.Calls, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task OpenAiFirstInOrder_UsesOpenAi()
    {
        var gemini =
            new FakeGeminiProvider(
                isAvailable: true,
                responseText: "Gemini response.");

        var nvidia =
            new FakeNvidiaProvider(
                isAvailable: true,
                responseText: "NVIDIA response.");

        var openAi =
            new FakeOpenAiProvider(
                isAvailable: true,
                responseText: "OpenAI response.");

        var router =
            CreateRouter(
                gemini,
                nvidia,
                openAi,
                order: new[] { "OpenAI", "Gemini" },
                fallbackEnabled: true);

        var result =
            await router.GenerateExplanationAsync(
                CreateRequest());

        Assert.Multiple(() =>
        {
            Assert.That(result.Provider, Is.EqualTo("OpenAI"));
            Assert.That(openAi.Calls, Is.EqualTo(1));
            Assert.That(gemini.Calls, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task GeminiFailure_WithFallbackEnabled_UsesOpenAi()
    {
        var gemini =
            new FakeGeminiProvider(
                isAvailable: true,
                responseText: "Gemini response.",
                exception:
                    new InvalidOperationException(
                        "Gemini unavailable."));

        var nvidia =
            new FakeNvidiaProvider(
                isAvailable: true,
                responseText: "NVIDIA response.");

        var openAi =
            new FakeOpenAiProvider(
                isAvailable: true,
                responseText: "OpenAI response.");

        var router =
            CreateRouter(
                gemini,
                nvidia,
                openAi,
                order: new[] { "Gemini", "OpenAI" },
                fallbackEnabled: true);

        var result =
            await router.GenerateExplanationAsync(
                CreateRequest());

        Assert.Multiple(() =>
        {
            Assert.That(result.Provider, Is.EqualTo("OpenAI"));
            Assert.That(gemini.Calls, Is.EqualTo(1));
            Assert.That(openAi.Calls, Is.EqualTo(1));
        });
    }

    [Test]
    public void GeminiFailure_WithFallbackDisabled_Rethrows()
    {
        var gemini =
            new FakeGeminiProvider(
                isAvailable: true,
                responseText: "Gemini response.",
                exception:
                    new InvalidOperationException(
                        "Gemini unavailable."));

        var nvidia =
            new FakeNvidiaProvider(
                isAvailable: true,
                responseText: "NVIDIA response.");

        var openAi =
            new FakeOpenAiProvider(
                isAvailable: true,
                responseText: "OpenAI response.");

        var router =
            CreateRouter(
                gemini,
                nvidia,
                openAi,
                order: new[] { "Gemini", "OpenAI" },
                fallbackEnabled: false);

        var caughtException =
            Assert.ThrowsAsync<AiProviderUnavailableException>(
                async () =>
                    await router.GenerateExplanationAsync(
                        CreateRequest()));

        Assert.That(
            caughtException!.InnerException,
            Is.TypeOf<InvalidOperationException>());

        Assert.Multiple(() =>
        {
            Assert.That(gemini.Calls, Is.EqualTo(1));
            Assert.That(nvidia.Calls, Is.EqualTo(0));
            Assert.That(openAi.Calls, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task PrimaryUnavailable_FallsBackToOtherAvailableProvider()
    {
        var gemini =
            new FakeGeminiProvider(
                isAvailable: false,
                responseText: "Gemini response.");

        var nvidia =
            new FakeNvidiaProvider(
                isAvailable: true,
                responseText: "NVIDIA response.");

        var openAi =
            new FakeOpenAiProvider(
                isAvailable: true,
                responseText: "OpenAI response.");

        var router =
            CreateRouter(
                gemini,
                nvidia,
                openAi,
                order: new[] { "Gemini", "OpenAI" },
                fallbackEnabled: true);

        var result =
            await router.GenerateExplanationAsync(
                CreateRequest());

        Assert.Multiple(() =>
        {
            Assert.That(result.Provider, Is.EqualTo("OpenAI"));
            // Gemini is excluded by the IsAvailable preflight -- never
            // actually invoked.
            Assert.That(gemini.Calls, Is.EqualTo(0));
            Assert.That(openAi.Calls, Is.EqualTo(1));
        });
    }

    [Test]
    public void PrimaryUnavailable_SubstitutedPrimaryFails_DoesNotRetrySameProvider()
    {
        // Regression test for the confirmed Phase 3 defect: when the
        // configured primary (Gemini) is unavailable, OpenAI is the sole
        // effective candidate. If OpenAI then fails, the router must not
        // retry Gemini (still unavailable, correctly excluded) or OpenAI
        // itself a second time.
        var gemini =
            new FakeGeminiProvider(
                isAvailable: false,
                responseText: "Gemini response.");

        var nvidia =
            new FakeNvidiaProvider(
                isAvailable: true,
                responseText: "NVIDIA response.");

        var openAi =
            new FakeOpenAiProvider(
                isAvailable: true,
                responseText: "OpenAI response.",
                exception:
                    new InvalidOperationException(
                        "OpenAI unavailable."));

        var router =
            CreateRouter(
                gemini,
                nvidia,
                openAi,
                order: new[] { "Gemini", "OpenAI" },
                fallbackEnabled: true);

        var caughtException =
            Assert.ThrowsAsync<AiProviderUnavailableException>(
                async () =>
                    await router.GenerateExplanationAsync(
                        CreateRequest()));

        Assert.Multiple(() =>
        {
            Assert.That(openAi.Calls, Is.EqualTo(1));
            Assert.That(gemini.Calls, Is.EqualTo(0));
            Assert.That(caughtException, Is.Not.Null);
        });
    }

    [Test]
    public void PrimaryUnavailable_SubstitutedPrimaryFails_MessageNamesRealFallbackProvider()
    {
        // Regression test for the cosmetic missing-'$'-interpolation bug:
        // the "fallback also unavailable" message must name the actual
        // excluded provider, not render a literal placeholder.
        var gemini =
            new FakeGeminiProvider(
                isAvailable: false,
                responseText: "Gemini response.");

        var nvidia =
            new FakeNvidiaProvider(
                isAvailable: true,
                responseText: "NVIDIA response.");

        var openAi =
            new FakeOpenAiProvider(
                isAvailable: true,
                responseText: "OpenAI response.",
                exception:
                    new InvalidOperationException(
                        "OpenAI unavailable."));

        var router =
            CreateRouter(
                gemini,
                nvidia,
                openAi,
                order: new[] { "Gemini", "OpenAI" },
                fallbackEnabled: true);

        var caughtException =
            Assert.ThrowsAsync<AiProviderUnavailableException>(
                async () =>
                    await router.GenerateExplanationAsync(
                        CreateRequest()));

        Assert.Multiple(() =>
        {
            Assert.That(
                caughtException!.Message,
                Does.Contain(
                    "fallback AI provider 'Gemini' is unavailable"));

            Assert.That(
                caughtException.Message,
                Does.Not.Contain("{fallback.ProviderName}"));
        });
    }

    [Test]
    public void EmptyProviderOrder_Throws()
    {
        // Reproduces what an unrecognized legacy AI:DefaultProvider value
        // resolves to (see AiProviderConfigurationResolutionTests) --
        // an empty order at the router level, which must throw
        // AiProviderUnavailableException without invoking anything.
        var gemini =
            new FakeGeminiProvider(
                isAvailable: true,
                responseText: "Gemini response.");

        var nvidia =
            new FakeNvidiaProvider(
                isAvailable: true,
                responseText: "NVIDIA response.");

        var openAi =
            new FakeOpenAiProvider(
                isAvailable: true,
                responseText: "OpenAI response.");

        var router =
            CreateRouter(
                gemini,
                nvidia,
                openAi,
                order: Array.Empty<string>(),
                fallbackEnabled: true);

        var caughtException =
            Assert.ThrowsAsync<AiProviderUnavailableException>(
                async () =>
                    await router.GenerateExplanationAsync(
                        CreateRequest()));

        Assert.Multiple(() =>
        {
            Assert.That(
                caughtException!.Message,
                Is.EqualTo("No configured AI provider is available."));

            Assert.That(gemini.Calls, Is.EqualTo(0));
            Assert.That(nvidia.Calls, Is.EqualTo(0));
            Assert.That(openAi.Calls, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task GeminiSuccess_NvidiaNotInOrder_NvidiaNeverCalled()
    {
        // Regression proof for existing 2-provider deployments: NVIDIA is
        // registered (DI always constructs it) but simply absent from the
        // configured order, so it's never touched.
        var gemini =
            new FakeGeminiProvider(
                isAvailable: true,
                responseText: "Gemini response.");

        var nvidia =
            new FakeNvidiaProvider(
                isAvailable: true,
                responseText: "Should not be called.");

        var openAi =
            new FakeOpenAiProvider(
                isAvailable: true,
                responseText: "OpenAI response.");

        var router =
            CreateRouter(
                gemini,
                nvidia,
                openAi,
                order: new[] { "Gemini", "OpenAI" },
                fallbackEnabled: true);

        await router.GenerateExplanationAsync(CreateRequest());

        Assert.That(nvidia.Calls, Is.EqualTo(0));
    }

    [Test]
    public async Task GeminiFailure_NvidiaIsAttempted()
    {
        var gemini =
            new FakeGeminiProvider(
                isAvailable: true,
                responseText: "Gemini response.",
                exception: new InvalidOperationException("Gemini failed."));

        var nvidia =
            new FakeNvidiaProvider(
                isAvailable: true,
                responseText: "NVIDIA response.");

        var openAi =
            new FakeOpenAiProvider(
                isAvailable: true,
                responseText: "OpenAI response.");

        var router =
            CreateRouter(
                gemini,
                nvidia,
                openAi,
                order: new[] { "Gemini", "NVIDIA", "OpenAI" },
                fallbackEnabled: true);

        var result =
            await router.GenerateExplanationAsync(CreateRequest());

        Assert.Multiple(() =>
        {
            Assert.That(result.Provider, Is.EqualTo("NVIDIA"));
            Assert.That(gemini.Calls, Is.EqualTo(1));
            Assert.That(nvidia.Calls, Is.EqualTo(1));
            Assert.That(openAi.Calls, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task NvidiaSuccess_OpenAiNotCalled()
    {
        var gemini =
            new FakeGeminiProvider(
                isAvailable: true,
                responseText: "Gemini response.",
                exception: new InvalidOperationException("Gemini failed."));

        var nvidia =
            new FakeNvidiaProvider(
                isAvailable: true,
                responseText: "NVIDIA response.");

        var openAi =
            new FakeOpenAiProvider(
                isAvailable: true,
                responseText: "Should not be used.");

        var router =
            CreateRouter(
                gemini,
                nvidia,
                openAi,
                order: new[] { "Gemini", "NVIDIA", "OpenAI" },
                fallbackEnabled: true);

        var result =
            await router.GenerateExplanationAsync(CreateRequest());

        Assert.Multiple(() =>
        {
            Assert.That(result.Provider, Is.EqualTo("NVIDIA"));
            Assert.That(openAi.Calls, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task NvidiaFailure_OpenAiIsAttempted()
    {
        var gemini =
            new FakeGeminiProvider(
                isAvailable: true,
                responseText: "Gemini response.",
                exception: new InvalidOperationException("Gemini failed."));

        var nvidia =
            new FakeNvidiaProvider(
                isAvailable: true,
                responseText: "NVIDIA response.",
                exception: new InvalidOperationException("NVIDIA failed."));

        var openAi =
            new FakeOpenAiProvider(
                isAvailable: true,
                responseText: "OpenAI fallback answer.");

        var router =
            CreateRouter(
                gemini,
                nvidia,
                openAi,
                order: new[] { "Gemini", "NVIDIA", "OpenAI" },
                fallbackEnabled: true);

        var result =
            await router.GenerateExplanationAsync(CreateRequest());

        Assert.Multiple(() =>
        {
            Assert.That(result.Provider, Is.EqualTo("OpenAI"));
            Assert.That(gemini.Calls, Is.EqualTo(1));
            Assert.That(nvidia.Calls, Is.EqualTo(1));
            Assert.That(openAi.Calls, Is.EqualTo(1));
        });
    }

    [Test]
    public void AllThreeProvidersFail_ThrowsWithNewProviderCountWording()
    {
        var gemini =
            new FakeGeminiProvider(
                isAvailable: true,
                responseText: "unused",
                exception: new InvalidOperationException("Gemini failed."));

        var nvidia =
            new FakeNvidiaProvider(
                isAvailable: true,
                responseText: "unused",
                exception: new InvalidOperationException("NVIDIA failed."));

        var openAi =
            new FakeOpenAiProvider(
                isAvailable: true,
                responseText: "unused",
                exception: new InvalidOperationException("OpenAI failed."));

        var router =
            CreateRouter(
                gemini,
                nvidia,
                openAi,
                order: new[] { "Gemini", "NVIDIA", "OpenAI" },
                fallbackEnabled: true);

        var caughtException =
            Assert.ThrowsAsync<AiProviderUnavailableException>(
                async () =>
                    await router.GenerateExplanationAsync(
                        CreateRequest()));

        Assert.Multiple(() =>
        {
            // Never the old hardcoded "Both AI providers failed..." --
            // that wording is reserved for the exactly-2-failure case.
            Assert.That(
                caughtException!.Message,
                Is.EqualTo("All 3 configured AI providers failed."));

            Assert.That(
                caughtException.InnerException,
                Is.TypeOf<AggregateException>());

            var aggregate = (AggregateException)caughtException.InnerException!;

            Assert.That(aggregate.InnerExceptions.Count, Is.EqualTo(3));

            Assert.That(gemini.Calls, Is.EqualTo(1));
            Assert.That(nvidia.Calls, Is.EqualTo(1));
            Assert.That(openAi.Calls, Is.EqualTo(1));
        });
    }

    [Test]
    public void ExactlyTwoProvidersFail_PreservesLegacyBothProvidersWording()
    {
        var gemini =
            new FakeGeminiProvider(
                isAvailable: true,
                responseText: "unused",
                exception: new InvalidOperationException("Gemini failed."));

        var nvidia =
            new FakeNvidiaProvider(
                isAvailable: true,
                responseText: "unused");

        var openAi =
            new FakeOpenAiProvider(
                isAvailable: true,
                responseText: "unused",
                exception: new InvalidOperationException("OpenAI failed."));

        // NVIDIA deliberately excluded from the order -- this reproduces
        // the exact pre-NVIDIA 2-provider topology.
        var router =
            CreateRouter(
                gemini,
                nvidia,
                openAi,
                order: new[] { "Gemini", "OpenAI" },
                fallbackEnabled: true);

        var caughtException =
            Assert.ThrowsAsync<AiProviderUnavailableException>(
                async () =>
                    await router.GenerateExplanationAsync(
                        CreateRequest()));

        Assert.That(
            caughtException!.Message,
            Is.EqualTo(
                "Both AI providers failed. Primary provider 'Gemini' " +
                "and fallback provider 'OpenAI' were unavailable."));
    }

    [Test]
    public void IsAvailable_TrueWhenAnyOfThreeProvidersIsAvailable()
    {
        var gemini =
            new FakeGeminiProvider(isAvailable: false, responseText: "unused");

        var nvidia =
            new FakeNvidiaProvider(isAvailable: false, responseText: "unused");

        var openAi =
            new FakeOpenAiProvider(isAvailable: true, responseText: "unused");

        var router =
            CreateRouter(
                gemini,
                nvidia,
                openAi,
                order: new[] { "Gemini", "NVIDIA", "OpenAI" },
                fallbackEnabled: true);

        Assert.That(router.IsAvailable, Is.True);
    }

    [Test]
    public void IsAvailable_FalseWhenAllThreeProvidersAreUnavailable()
    {
        var gemini =
            new FakeGeminiProvider(isAvailable: false, responseText: "unused");

        var nvidia =
            new FakeNvidiaProvider(isAvailable: false, responseText: "unused");

        var openAi =
            new FakeOpenAiProvider(isAvailable: false, responseText: "unused");

        var router =
            CreateRouter(
                gemini,
                nvidia,
                openAi,
                order: new[] { "Gemini", "NVIDIA", "OpenAI" },
                fallbackEnabled: true);

        Assert.That(router.IsAvailable, Is.False);
    }

    [Test]
    public void Constructor_ProviderAbsentFromOrder_IsNeverConstructed()
    {
        // Global AI Provider DI Resolution fix: registers Gemini/OpenAI
        // as keyed FACTORIES (not pre-built instances) that increment a
        // counter when invoked -- proving the router never even asks DI
        // for a provider excluded from ProviderOrder, not merely that it
        // never calls GenerateExplanationAsync on it.
        var geminiConstructions = 0;
        var openAiConstructions = 0;

        var services = new ServiceCollection();

        services.AddKeyedSingleton<IAiProvider>(
            "Gemini",
            (_, _) =>
            {
                geminiConstructions++;
                return new FakeGeminiProvider(
                    isAvailable: true,
                    responseText: "unused");
            });

        services.AddKeyedSingleton<IAiProvider>(
            "NVIDIA",
            new FakeNvidiaProvider(
                isAvailable: true,
                responseText: "NVIDIA response."));

        services.AddKeyedSingleton<IAiProvider>(
            "OpenAI",
            (_, _) =>
            {
                openAiConstructions++;
                return new FakeOpenAiProvider(
                    isAvailable: true,
                    responseText: "unused");
            });

        var serviceProvider = services.BuildServiceProvider();

        var options =
            new AiProviderOptions
            {
                ExceptionExplanation =
                    new AiProviderOptions.SurfaceOptions
                    {
                        ProviderOrder = new[] { "NVIDIA" },
                        FallbackEnabled = true
                    }
            };

        var router = new AiProviderRouter(serviceProvider, options);

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

        services.AddKeyedSingleton<IAiProvider>(
            "Gemini",
            (_, _) =>
            {
                geminiConstructions++;
                return new FakeGeminiProvider(
                    isAvailable: true,
                    responseText: "unused");
            });

        services.AddKeyedSingleton<IAiProvider>(
            "NVIDIA",
            new FakeNvidiaProvider(
                isAvailable: true,
                responseText: "NVIDIA response."));

        services.AddKeyedSingleton<IAiProvider>(
            "OpenAI",
            new FakeOpenAiProvider(
                isAvailable: true,
                responseText: "unused"));

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

                ExceptionExplanation =
                    new AiProviderOptions.SurfaceOptions
                    {
                        // Gemini is present in the order but disabled --
                        // must still never be constructed.
                        ProviderOrder = new[] { "Gemini", "NVIDIA" },
                        FallbackEnabled = true
                    }
            };

        var router = new AiProviderRouter(serviceProvider, options);

        Assert.Multiple(() =>
        {
            Assert.That(geminiConstructions, Is.EqualTo(0));
            Assert.That(router.ProviderName, Is.EqualTo("NVIDIA"));
        });
    }

    private static AiProviderRouter CreateRouter(
        IGeminiAiProvider gemini,
        INvidiaAiProvider nvidia,
        IOpenAiProvider openAi,
        IReadOnlyList<string> order,
        bool fallbackEnabled)
    {
        var options =
            new AiProviderOptions
            {
                ExceptionExplanation =
                    new AiProviderOptions.SurfaceOptions
                    {
                        ProviderOrder = order,
                        FallbackEnabled = fallbackEnabled
                    }
            };

        // AiProviderRouter resolves providers by name from keyed DI
        // (Global AI Provider DI Resolution fix) -- these fakes are
        // registered as fixed instances under the same keys
        // DependencyInjection.cs uses in production ("Gemini"/"NVIDIA"/
        // "OpenAI"), so the router's own name-based resolution logic is
        // exercised exactly as it runs for real, not bypassed.
        var services = new ServiceCollection();

        services.AddKeyedSingleton<IAiProvider>("Gemini", gemini);
        services.AddKeyedSingleton<IAiProvider>("NVIDIA", nvidia);
        services.AddKeyedSingleton<IAiProvider>("OpenAI", openAi);

        var serviceProvider = services.BuildServiceProvider();

        return new AiProviderRouter(
            serviceProvider,
            options);
    }

    private static AiExplanationRequest CreateRequest()
    {
        return new AiExplanationRequest
        {
            ExceptionId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            ReconciliationResultId = Guid.NewGuid(),
            TransactionReference = "TXN-0001",
            DeterministicCategory = "AmountMismatch",
            InvolvedSources = "Payment,Bank,Settlement",
            DiscrepancyDetail =
                """{"paymentAmount":3500,"bankAmount":3490}"""
        };
    }

    private sealed class FakeGeminiProvider
        : IGeminiAiProvider
    {
        private readonly string _responseText;
        private readonly Exception? _exception;

        public FakeGeminiProvider(
            bool isAvailable,
            string responseText,
            Exception? exception = null)
        {
            IsAvailable = isAvailable;
            _responseText = responseText;
            _exception = exception;
        }

        public string ProviderName => "Gemini";

        public bool IsAvailable { get; }

        public int Calls { get; private set; }

        public Task<AiExplanationResponse>
            GenerateExplanationAsync(
                AiExplanationRequest request,
                CancellationToken cancellationToken = default)
        {
            Calls++;

            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(
                new AiExplanationResponse
                {
                    Provider = ProviderName,
                    Explanation = _responseText,
                    SuggestedCategory = "AmountMismatch",
                    GeneratedAtUtc = DateTime.UtcNow
                });
        }
    }

    private sealed class FakeNvidiaProvider
        : INvidiaAiProvider
    {
        private readonly string _responseText;
        private readonly Exception? _exception;

        public FakeNvidiaProvider(
            bool isAvailable,
            string responseText,
            Exception? exception = null)
        {
            IsAvailable = isAvailable;
            _responseText = responseText;
            _exception = exception;
        }

        public string ProviderName => "NVIDIA";

        public bool IsAvailable { get; }

        public int Calls { get; private set; }

        public Task<AiExplanationResponse>
            GenerateExplanationAsync(
                AiExplanationRequest request,
                CancellationToken cancellationToken = default)
        {
            Calls++;

            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(
                new AiExplanationResponse
                {
                    Provider = ProviderName,
                    Explanation = _responseText,
                    SuggestedCategory = "AmountMismatch",
                    GeneratedAtUtc = DateTime.UtcNow
                });
        }
    }

    private sealed class FakeOpenAiProvider
        : IOpenAiProvider
    {
        private readonly string _responseText;
        private readonly Exception? _exception;

        public FakeOpenAiProvider(
            bool isAvailable,
            string responseText,
            Exception? exception = null)
        {
            IsAvailable = isAvailable;
            _responseText = responseText;
            _exception = exception;
        }

        public string ProviderName => "OpenAI";

        public bool IsAvailable { get; }

        public int Calls { get; private set; }

        public Task<AiExplanationResponse>
            GenerateExplanationAsync(
                AiExplanationRequest request,
                CancellationToken cancellationToken = default)
        {
            Calls++;

            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(
                new AiExplanationResponse
                {
                    Provider = ProviderName,
                    Explanation = _responseText,
                    SuggestedCategory = "AmountMismatch",
                    GeneratedAtUtc = DateTime.UtcNow
                });
        }
    }
}
