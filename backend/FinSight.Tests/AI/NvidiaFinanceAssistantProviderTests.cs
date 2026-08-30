using FinSight.Infrastructure.AI.OpenAI;

namespace FinSight.Tests.AI;

/// <summary>
/// NvidiaFinanceAssistantProvider is built directly on the `OpenAI` SDK's
/// `ChatClient` (constructed internally, not behind an injectable
/// abstraction -- unlike GeminiFinanceAssistantProvider's
/// IFinanceAssistantModelClient seam). That means this file, like the
/// pre-existing OpenAiFinanceAssistantProvider it mirrors (which itself
/// has zero dedicated unit tests in this repo), cannot exercise the real
/// request/response wire behavior (tool declarations sent, tool_choice
/// value, response parsing across shapes) without either a real network
/// call or a hand-built System.ClientModel PipelineTransport test double
/// -- a non-trivial SDK-internals undertaking not attempted here. NVIDIA
/// is therefore at exactly the same unit-test parity as the existing
/// OpenAI provider, not a regression from it. What IS directly,
/// deterministically testable without any network access or API key is
/// covered below: provider identity, configuration-driven "not
/// configured" failure modes (missing/invalid API key, model, or base
/// URL), the pre-flight question validation, and cancellation
/// short-circuiting before any request is dispatched. The request/
/// response behaviors this can't cover are proven live instead -- see the
/// Phase F10 NVIDIA implementation report's live verification section.
/// </summary>
[TestFixture]
public sealed class NvidiaFinanceAssistantProviderTests
{
    private const string ValidBaseUrl = "https://integrate.api.nvidia.com/v1";
    private const string ValidModel = "openai/gpt-oss-120b";

    [Test]
    public void ProviderName_IsNvidia()
    {
        var provider =
            new NvidiaFinanceAssistantProvider(
                "test-key",
                ValidModel,
                ValidBaseUrl);

        Assert.That(provider.ProviderName, Is.EqualTo("NVIDIA"));
    }

    [Test]
    public void AskAsync_WithEmptyQuestion_ThrowsBeforeTouchingConfiguration()
    {
        // Validated before the client is even constructed -- proven by
        // this succeeding with a deliberately-invalid API key/model/URL:
        // if configuration were checked first, this would throw
        // InvalidOperationException("...is not configured.") instead.
        var provider =
            new NvidiaFinanceAssistantProvider(
                string.Empty,
                string.Empty,
                string.Empty);

        var exception =
            Assert.ThrowsAsync<ArgumentException>(
                async () =>
                    await provider.AskAsync(
                        CreateRequest(question: "   ")));

        Assert.That(
            exception!.Message,
            Does.StartWith("Question is required."));
    }

    [Test]
    public void AskAsync_WithMissingApiKey_ThrowsNotConfigured()
    {
        var provider =
            new NvidiaFinanceAssistantProvider(
                string.Empty,
                ValidModel,
                ValidBaseUrl);

        var exception =
            Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                    await provider.AskAsync(CreateRequest()));

        Assert.That(
            exception!.Message,
            Is.EqualTo(
                "NVIDIA Finance Assistant provider is not configured."));
    }

    [Test]
    public void AskAsync_WithMissingModel_ThrowsNotConfigured()
    {
        var provider =
            new NvidiaFinanceAssistantProvider(
                "test-key",
                string.Empty,
                ValidBaseUrl);

        var exception =
            Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                    await provider.AskAsync(CreateRequest()));

        Assert.That(
            exception!.Message,
            Is.EqualTo(
                "NVIDIA Finance Assistant provider is not configured."));
    }

    [Test]
    public void AskAsync_WithMissingBaseUrl_ThrowsNotConfigured()
    {
        var provider =
            new NvidiaFinanceAssistantProvider(
                "test-key",
                ValidModel,
                string.Empty);

        var exception =
            Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                    await provider.AskAsync(CreateRequest()));

        Assert.That(
            exception!.Message,
            Is.EqualTo(
                "NVIDIA Finance Assistant provider is not configured."));
    }

    [Test]
    public void AskAsync_WithMalformedBaseUrl_ThrowsNotConfigured()
    {
        // "not-a-url" fails Uri.TryCreate(..., UriKind.Absolute, ...) --
        // treated identically to a missing base URL rather than crashing
        // with an unhandled UriFormatException.
        var provider =
            new NvidiaFinanceAssistantProvider(
                "test-key",
                ValidModel,
                "not-a-url");

        var exception =
            Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                    await provider.AskAsync(CreateRequest()));

        Assert.That(
            exception!.Message,
            Is.EqualTo(
                "NVIDIA Finance Assistant provider is not configured."));
    }

    [Test]
    public void AskAsync_WhenCancelledBeforeDispatch_PropagatesWithoutNetworkCall()
    {
        // A fully-configured provider (real-shaped key/model/URL, no
        // network call ever needed): an already-cancelled token is
        // rejected by the HTTP pipeline before any request is dispatched,
        // so this both proves cancellation propagates and requires no
        // real NVIDIA credential or live network access.
        var provider =
            new NvidiaFinanceAssistantProvider(
                "test-key-not-real",
                ValidModel,
                ValidBaseUrl);

        using var alreadyCancelled = new CancellationTokenSource();
        alreadyCancelled.Cancel();

        Assert.CatchAsync<OperationCanceledException>(
            async () =>
                await provider.AskAsync(
                    CreateRequest(),
                    alreadyCancelled.Token));
    }

    private static FinSight.Application.AI.FinanceAssistantProviderRequest CreateRequest(
        string question = "What is the match rate for this run?")
    {
        return new FinSight.Application.AI.FinanceAssistantProviderRequest
        {
            RunId = Guid.NewGuid(),
            Question = question,
            Tools = Array.Empty<FinSight.Application.AI.FinanceToolDefinition>(),
            PreviousToolCalls = Array.Empty<FinSight.Application.AI.FinanceToolCall>(),
            ToolResults = Array.Empty<FinSight.Application.AI.FinanceToolResultMessage>()
        };
    }
}
