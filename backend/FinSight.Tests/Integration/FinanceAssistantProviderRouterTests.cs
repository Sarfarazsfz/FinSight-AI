using FinSight.Application.AI;
using FinSight.Infrastructure.AI;
using NUnit.Framework;

namespace FinSight.Tests.Integration;

[TestFixture]
public sealed class FinanceAssistantProviderRouterTests
{
    [Test]
    public async Task GeminiSuccess_ReturnsGeminiResponse_WithoutCallingFallback()
    {
        var gemini =
            new FakeFinanceAssistantProvider(
                "Gemini",
                _ =>
                    Task.FromResult(
                        CreateResponse("Gemini answer")));

        var openAi =
            new FakeFinanceAssistantProvider(
                "OpenAI",
                _ =>
                    Task.FromResult(
                        CreateResponse("OpenAI answer")));

        var router =
            CreateRouter(
                gemini,
                openAi,
                defaultProvider: "Gemini",
                fallbackEnabled: true);

        var response =
            await router.AskAsync(CreateRequest());

        Assert.That(
            response.Answer,
            Is.EqualTo("Gemini answer"));

        Assert.That(
            gemini.CallCount,
            Is.EqualTo(1));

        Assert.That(
            openAi.CallCount,
            Is.EqualTo(0));
    }

    [Test]
    public async Task GeminiFailure_WithFallbackEnabled_UsesOpenAi()
    {
        var gemini =
            new FakeFinanceAssistantProvider(
                "Gemini",
                _ =>
                    Task.FromException<
                        FinanceAssistantProviderResponse>(
                        new InvalidOperationException(
                            "Gemini unavailable")));

        var openAi =
            new FakeFinanceAssistantProvider(
                "OpenAI",
                _ =>
                    Task.FromResult(
                        CreateResponse("OpenAI fallback answer")));

        var router =
            CreateRouter(
                gemini,
                openAi,
                defaultProvider: "Gemini",
                fallbackEnabled: true);

        var response =
            await router.AskAsync(CreateRequest());

        Assert.That(
            response.Answer,
            Is.EqualTo("OpenAI fallback answer"));

        Assert.That(
            gemini.CallCount,
            Is.EqualTo(1));

        Assert.That(
            openAi.CallCount,
            Is.EqualTo(1));
    }

    [Test]
    public async Task GeminiFailure_WithFallbackDisabled_Throws()
    {
        var gemini =
            new FakeFinanceAssistantProvider(
                "Gemini",
                _ =>
                    Task.FromException<
                        FinanceAssistantProviderResponse>(
                        new InvalidOperationException(
                            "Gemini unavailable")));

        var openAi =
            new FakeFinanceAssistantProvider(
                "OpenAI",
                _ =>
                    Task.FromResult(
                        CreateResponse("Should not be used")));

        var router =
            CreateRouter(
                gemini,
                openAi,
                defaultProvider: "Gemini",
                fallbackEnabled: false);

        var exception =
            Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                    await router.AskAsync(
                        CreateRequest()));

        Assert.That(
            exception!.Message,
            Does.Contain(
                "Finance Assistant provider 'Gemini' failed."));

        Assert.That(
            gemini.CallCount,
            Is.EqualTo(1));

        Assert.That(
            openAi.CallCount,
            Is.EqualTo(0));
    }

    [Test]
    public async Task BothProvidersFail_ThrowsCombinedFailure()
    {
        var gemini =
            new FakeFinanceAssistantProvider(
                "Gemini",
                _ =>
                    Task.FromException<
                        FinanceAssistantProviderResponse>(
                        new InvalidOperationException(
                            "Gemini failed")));

        var openAi =
            new FakeFinanceAssistantProvider(
                "OpenAI",
                _ =>
                    Task.FromException<
                        FinanceAssistantProviderResponse>(
                        new InvalidOperationException(
                            "OpenAI failed")));

        var router =
            CreateRouter(
                gemini,
                openAi,
                defaultProvider: "Gemini",
                fallbackEnabled: true);

        var exception =
            Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                    await router.AskAsync(
                        CreateRequest()));

        Assert.That(
            exception!.Message,
            Is.EqualTo(
                "Both Finance Assistant AI providers failed."));

        Assert.That(
            exception.InnerException,
            Is.TypeOf<AggregateException>());

        var aggregate =
            (AggregateException)exception.InnerException!;

        Assert.That(
            aggregate.InnerExceptions.Count,
            Is.EqualTo(2));

        Assert.That(
            gemini.CallCount,
            Is.EqualTo(1));

        Assert.That(
            openAi.CallCount,
            Is.EqualTo(1));
    }

    private static FinanceAssistantProviderRouter CreateRouter(
        IFinanceAssistantProvider gemini,
        IFinanceAssistantProvider openAi,
        string defaultProvider,
        bool fallbackEnabled)
    {
        var options =
            new AiProviderOptions
            {
                DefaultProvider =
                    defaultProvider,

                FallbackEnabled =
                    fallbackEnabled
            };

        return new FinanceAssistantProviderRouter(
            gemini,
            openAi,
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
