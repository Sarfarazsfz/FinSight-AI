using FinSight.Application.AI;
using FinSight.Infrastructure.AI;
using FinSight.Infrastructure.AI.Gemini;
using FinSight.Infrastructure.AI.OpenAI;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.Tests.Integration;

[TestFixture]
[NonParallelizable]
public sealed class FinanceAssistantDiTests
{
    private PostgresIntegrationFixture _fixture = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _fixture = new PostgresIntegrationFixture();
    }

    [Test]
    public async Task FinanceAssistantService_IsResolvable()
    {
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var service =
            scope.ServiceProvider
                .GetRequiredService<IFinanceAssistantService>();

        Assert.That(
            service,
            Is.Not.Null);
    }

    [Test]
    public async Task FinanceAssistantProvider_IsResolvable()
    {
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var provider =
            scope.ServiceProvider
                .GetRequiredService<IFinanceAssistantProvider>();

        Assert.That(
            provider,
            Is.Not.Null);

        Assert.That(
            provider.ProviderName,
            Is.EqualTo("Gemini"));
    }

    [Test]
    public async Task FinanceAssistantProvider_ResolvesToTheRouter_NotDirectlyToGemini()
    {
        // Phase 3 fix: production wiring previously bound
        // IFinanceAssistantProvider directly to GeminiFinanceAssistantProvider,
        // so the chat assistant had zero AI-provider fallback in
        // production even though FinanceAssistantProviderRouter existed
        // and was already proven correct by its own unit tests. This
        // proves the DI graph now actually resolves through the router.
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var provider =
            scope.ServiceProvider
                .GetRequiredService<IFinanceAssistantProvider>();

        Assert.That(
            provider,
            Is.InstanceOf<FinanceAssistantProviderRouter>());
    }

    [Test]
    public async Task GeminiAndOpenAiFinanceAssistantProviders_AreBothResolvable()
    {
        // Both concrete providers must be independently resolvable --
        // the router depends on both being constructable, regardless of
        // which one AI:DefaultProvider selects as primary.
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var gemini =
            scope.ServiceProvider
                .GetRequiredService<GeminiFinanceAssistantProvider>();

        var openAi =
            scope.ServiceProvider
                .GetRequiredService<OpenAiFinanceAssistantProvider>();

        Assert.That(gemini, Is.Not.Null);
        Assert.That(openAi, Is.Not.Null);
    }

    [Test]
    public async Task FinanceAssistantModelClient_IsResolvable()
    {
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var client =
            scope.ServiceProvider
                .GetRequiredService<
                    IFinanceAssistantModelClient>();

        Assert.That(
            client,
            Is.Not.Null);
    }
}
