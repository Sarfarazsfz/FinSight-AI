using FinSight.Application.DTOs.Ai;
using FinSight.Infrastructure.AI.OpenAI;

namespace FinSight.Tests.AI;

/// <summary>
/// F9 exception-explanation NVIDIA adapter. Built on the same `OpenAI` SDK
/// `ChatClient` pattern as NvidiaFinanceAssistantProvider (F10's NVIDIA
/// adapter) -- see that file's test class for why the real request/
/// response wire behavior isn't unit-testable without a real network call
/// or a hand-built PipelineTransport test double, and why that's parity
/// with, not a regression from, the pre-existing zero test coverage of
/// OpenAiProvider. What's covered here: provider identity, the
/// configuration-driven "not configured" failure modes (missing/invalid
/// API key, model, or base URL), null-request validation, cancellation
/// short-circuiting, and -- unique to this class among F9's three
/// providers -- a real, computed IsAvailable (Gemini/OpenAI hardcode
/// `true`; this is what lets AiProviderRouter's chain skip an unconfigured
/// NVIDIA via preflight instead of only discovering it fails at call time).
/// </summary>
[TestFixture]
public sealed class NvidiaAiProviderTests
{
    private const string ValidBaseUrl = "https://integrate.api.nvidia.com/v1";
    private const string ValidModel = "openai/gpt-oss-120b";

    [Test]
    public void ProviderName_IsNvidia()
    {
        var provider =
            new NvidiaAiProvider(
                "test-key",
                ValidModel,
                ValidBaseUrl);

        Assert.That(provider.ProviderName, Is.EqualTo("NVIDIA"));
    }

    [Test]
    public void IsAvailable_TrueWhenApiKeyModelAndBaseUrlAreAllConfigured()
    {
        var provider =
            new NvidiaAiProvider(
                "test-key",
                ValidModel,
                ValidBaseUrl);

        Assert.That(provider.IsAvailable, Is.True);
    }

    [Test]
    public void IsAvailable_FalseWhenApiKeyIsMissing()
    {
        var provider =
            new NvidiaAiProvider(
                string.Empty,
                ValidModel,
                ValidBaseUrl);

        Assert.That(provider.IsAvailable, Is.False);
    }

    [Test]
    public void IsAvailable_FalseWhenModelIsMissing()
    {
        var provider =
            new NvidiaAiProvider(
                "test-key",
                string.Empty,
                ValidBaseUrl);

        Assert.That(provider.IsAvailable, Is.False);
    }

    [Test]
    public void IsAvailable_FalseWhenBaseUrlIsMissing()
    {
        var provider =
            new NvidiaAiProvider(
                "test-key",
                ValidModel,
                string.Empty);

        Assert.That(provider.IsAvailable, Is.False);
    }

    [Test]
    public void IsAvailable_FalseWhenBaseUrlIsMalformed()
    {
        // "not-a-url" fails Uri.TryCreate(..., UriKind.Absolute, ...) --
        // treated identically to a missing base URL.
        var provider =
            new NvidiaAiProvider(
                "test-key",
                ValidModel,
                "not-a-url");

        Assert.That(provider.IsAvailable, Is.False);
    }

    [Test]
    public void GenerateExplanationAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        var provider =
            new NvidiaAiProvider(
                "test-key",
                ValidModel,
                ValidBaseUrl);

        Assert.ThrowsAsync<ArgumentNullException>(
            async () =>
                await provider.GenerateExplanationAsync(null!));
    }

    [Test]
    public void GenerateExplanationAsync_WithMissingApiKey_ThrowsNotConfigured()
    {
        var provider =
            new NvidiaAiProvider(
                string.Empty,
                ValidModel,
                ValidBaseUrl);

        var exception =
            Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                    await provider.GenerateExplanationAsync(CreateRequest()));

        Assert.That(
            exception!.Message,
            Is.EqualTo("NVIDIA AI provider is not configured."));
    }

    [Test]
    public void GenerateExplanationAsync_WithMissingModel_ThrowsNotConfigured()
    {
        var provider =
            new NvidiaAiProvider(
                "test-key",
                string.Empty,
                ValidBaseUrl);

        var exception =
            Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                    await provider.GenerateExplanationAsync(CreateRequest()));

        Assert.That(
            exception!.Message,
            Is.EqualTo("NVIDIA AI provider is not configured."));
    }

    [Test]
    public void GenerateExplanationAsync_WithMissingBaseUrl_ThrowsNotConfigured()
    {
        var provider =
            new NvidiaAiProvider(
                "test-key",
                ValidModel,
                string.Empty);

        var exception =
            Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                    await provider.GenerateExplanationAsync(CreateRequest()));

        Assert.That(
            exception!.Message,
            Is.EqualTo("NVIDIA AI provider is not configured."));
    }

    [Test]
    public void GenerateExplanationAsync_WithMalformedBaseUrl_ThrowsNotConfigured()
    {
        var provider =
            new NvidiaAiProvider(
                "test-key",
                ValidModel,
                "not-a-url");

        var exception =
            Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                    await provider.GenerateExplanationAsync(CreateRequest()));

        Assert.That(
            exception!.Message,
            Is.EqualTo("NVIDIA AI provider is not configured."));
    }

    [Test]
    public void GenerateExplanationAsync_WhenCancelledBeforeDispatch_PropagatesWithoutNetworkCall()
    {
        var provider =
            new NvidiaAiProvider(
                "test-key-not-real",
                ValidModel,
                ValidBaseUrl);

        using var alreadyCancelled = new CancellationTokenSource();
        alreadyCancelled.Cancel();

        Assert.CatchAsync<OperationCanceledException>(
            async () =>
                await provider.GenerateExplanationAsync(
                    CreateRequest(),
                    alreadyCancelled.Token));
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
}
