using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Ai;
using FinSight.Application.Exceptions;
using FinSight.Infrastructure.AI;

namespace FinSight.Tests.AI;

[TestFixture]
public sealed class AiProviderRouterTests
{
    [Test]
    public async Task GeminiAsDefault_UsesGemini()
    {
        var gemini =
            new FakeGeminiProvider(
                isAvailable: true,
                responseText: "Gemini response.");

        var openAi =
            new FakeOpenAiProvider(
                isAvailable: true,
                responseText: "OpenAI response.");

        var options =
            new AiProviderOptions
            {
                DefaultProvider = "Gemini",
                FallbackEnabled = true
            };

        var router =
            new AiProviderRouter(
                gemini,
                openAi,
                options);

        var result =
            await router.GenerateExplanationAsync(
                CreateRequest());

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Provider,
                Is.EqualTo("Gemini"));

            Assert.That(
                gemini.Calls,
                Is.EqualTo(1));

            Assert.That(
                openAi.Calls,
                Is.EqualTo(0));
        });
    }

    [Test]
    public async Task OpenAiAsDefault_UsesOpenAi()
    {
        var gemini =
            new FakeGeminiProvider(
                isAvailable: true,
                responseText: "Gemini response.");

        var openAi =
            new FakeOpenAiProvider(
                isAvailable: true,
                responseText: "OpenAI response.");

        var options =
            new AiProviderOptions
            {
                DefaultProvider = "OpenAI",
                FallbackEnabled = true
            };

        var router =
            new AiProviderRouter(
                gemini,
                openAi,
                options);

        var result =
            await router.GenerateExplanationAsync(
                CreateRequest());

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Provider,
                Is.EqualTo("OpenAI"));

            Assert.That(
                openAi.Calls,
                Is.EqualTo(1));

            Assert.That(
                gemini.Calls,
                Is.EqualTo(0));
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

        var openAi =
            new FakeOpenAiProvider(
                isAvailable: true,
                responseText: "OpenAI response.");

        var options =
            new AiProviderOptions
            {
                DefaultProvider = "Gemini",
                FallbackEnabled = true
            };

        var router =
            new AiProviderRouter(
                gemini,
                openAi,
                options);

        var result =
            await router.GenerateExplanationAsync(
                CreateRequest());

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Provider,
                Is.EqualTo("OpenAI"));

            Assert.That(
                gemini.Calls,
                Is.EqualTo(1));

            Assert.That(
                openAi.Calls,
                Is.EqualTo(1));
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

        var openAi =
            new FakeOpenAiProvider(
                isAvailable: true,
                responseText: "OpenAI response.");

        var options =
            new AiProviderOptions
            {
                DefaultProvider = "Gemini",
                FallbackEnabled = false
            };

        var router =
            new AiProviderRouter(
                gemini,
                openAi,
                options);

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
            Assert.That(
                gemini.Calls,
                Is.EqualTo(1));

            Assert.That(
                openAi.Calls,
                Is.EqualTo(0));
        });
    }

    [Test]
    public async Task DefaultProviderUnavailable_FallsBackToOtherAvailableProvider()
    {
        var gemini =
            new FakeGeminiProvider(
                isAvailable: false,
                responseText: "Gemini response.");

        var openAi =
            new FakeOpenAiProvider(
                isAvailable: true,
                responseText: "OpenAI response.");

        var options =
            new AiProviderOptions
            {
                DefaultProvider = "Gemini",
                FallbackEnabled = true
            };

        var router =
            new AiProviderRouter(
                gemini,
                openAi,
                options);

        var result =
            await router.GenerateExplanationAsync(
                CreateRequest());

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Provider,
                Is.EqualTo("OpenAI"));

            Assert.That(
                gemini.Calls,
                Is.EqualTo(0));

            Assert.That(
                openAi.Calls,
                Is.EqualTo(1));
        });
    }

    [Test]
    public async Task DefaultProviderUnavailable_SubstitutedPrimaryFails_DoesNotRetrySameProvider()
    {
        // Regression test for the confirmed Phase 3 defect: when the
        // configured default (Gemini) is unavailable, OpenAI is
        // substituted as primary. If that substituted primary then
        // fails, the fallback must be derived from the ACTUAL primary
        // instance (correctly recognizing Gemini -- still unavailable --
        // as the only other candidate), never re-derived from the
        // configured default string, which would incorrectly return
        // OpenAI a second time.
        var gemini =
            new FakeGeminiProvider(
                isAvailable: false,
                responseText: "Gemini response.");

        var openAi =
            new FakeOpenAiProvider(
                isAvailable: true,
                responseText: "OpenAI response.",
                exception:
                    new InvalidOperationException(
                        "OpenAI unavailable."));

        var options =
            new AiProviderOptions
            {
                DefaultProvider = "Gemini",
                FallbackEnabled = true
            };

        var router =
            new AiProviderRouter(
                gemini,
                openAi,
                options);

        var caughtException =
            Assert.ThrowsAsync<AiProviderUnavailableException>(
                async () =>
                    await router.GenerateExplanationAsync(
                        CreateRequest()));

        Assert.Multiple(() =>
        {
            // OpenAI (the substituted primary) must be invoked exactly
            // once -- the pre-fix behavior invoked it a second time as
            // a bogus "fallback".
            Assert.That(
                openAi.Calls,
                Is.EqualTo(1));

            Assert.That(
                gemini.Calls,
                Is.EqualTo(0));

            Assert.That(
                caughtException,
                Is.Not.Null);
        });
    }

    [Test]
    public void DefaultProviderUnavailable_SubstitutedPrimaryFails_MessageNamesRealFallbackProvider()
    {
        // Regression test for the cosmetic missing-'$'-interpolation bug:
        // the "fallback also unavailable" message must name the actual
        // fallback provider, not render the literal placeholder text.
        var gemini =
            new FakeGeminiProvider(
                isAvailable: false,
                responseText: "Gemini response.");

        var openAi =
            new FakeOpenAiProvider(
                isAvailable: true,
                responseText: "OpenAI response.",
                exception:
                    new InvalidOperationException(
                        "OpenAI unavailable."));

        var options =
            new AiProviderOptions
            {
                DefaultProvider = "Gemini",
                FallbackEnabled = true
            };

        var router =
            new AiProviderRouter(
                gemini,
                openAi,
                options);

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
    public void UnsupportedProvider_Throws()
    {
        var gemini =
            new FakeGeminiProvider(
                isAvailable: true,
                responseText: "Gemini response.");

        var openAi =
            new FakeOpenAiProvider(
                isAvailable: true,
                responseText: "OpenAI response.");

        var options =
            new AiProviderOptions
            {
                DefaultProvider = "SomethingElse",
                FallbackEnabled = true
            };

        var router =
            new AiProviderRouter(
                gemini,
                openAi,
                options);

        Assert.ThrowsAsync<AiProviderUnavailableException>(
            async () =>
                await router.GenerateExplanationAsync(
                    CreateRequest()));
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