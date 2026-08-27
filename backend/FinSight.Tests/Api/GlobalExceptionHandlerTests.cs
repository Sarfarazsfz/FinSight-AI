using System.Security.Claims;
using System.Text.Encodings.Web;
using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Exceptions;
using FinSight.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinSight.Tests.Api;

[TestFixture]
public sealed class GlobalExceptionHandlerTests
{
    private WebApplicationFactory<Program> _factory = null!;

    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory =
            new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        services
                            .AddAuthentication("Test")
                            .AddScheme<
                                AuthenticationSchemeOptions,
                                TestAuthenticationHandler>(
                                "Test",
                                _ => { });

                        services.AddScoped<
                            IBatchRepository,
                            ThrowingBatchRepository>();
                    });
                });

        _client =
            _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task UnexpectedException_ReturnsProblemDetails()
    {
        using var response =
            await _client.GetAsync(
                $"/api/batches/{Guid.NewGuid()}");

        var body =
            await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(
                response.StatusCode,
                Is.EqualTo(
                    System.Net.HttpStatusCode.InternalServerError));

            Assert.That(
                response.Content.Headers.ContentType?.MediaType,
                Is.EqualTo("application/problem+json"));

            Assert.That(
                body,
                Does.Contain("\"status\":500"));

            Assert.That(
                body,
                Does.Contain(
                    "\"title\":\"An unexpected error occurred.\""));

            Assert.That(
                body,
                Does.Contain("\"traceId\""));

            Assert.That(
                body,
                Does.Not.Contain(
                    "Sensitive test exception"));

            Assert.That(
                body,
                Does.Not.Contain(
                    "System.InvalidOperationException"));
        });
    }

    [Test]
    public async Task AiProviderUnavailableException_Returns503ProblemDetails()
    {
        using var factory =
            new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        services
                            .AddAuthentication("Test")
                            .AddScheme<
                                AuthenticationSchemeOptions,
                                TestAuthenticationHandler>(
                                "Test",
                                _ => { });

                        services.AddScoped<
                            IBatchRepository,
                            ThrowingAiProviderRepository>();
                    });
                });

        using var client =
            factory.CreateClient();

        using var response =
            await client.GetAsync(
                $"/api/batches/{Guid.NewGuid()}");

        var body =
            await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(
                response.StatusCode,
                Is.EqualTo(
                    System.Net.HttpStatusCode.ServiceUnavailable));

            Assert.That(
                response.Content.Headers.ContentType?.MediaType,
                Is.EqualTo("application/problem+json"));

            Assert.That(
                body,
                Does.Contain("\"status\":503"));

            Assert.That(
                body,
                Does.Contain(
                    "\"title\":\"AI Provider Unavailable\""));

            Assert.That(
                body,
                Does.Contain("\"traceId\""));

            Assert.That(
                body,
                Does.Not.Contain(
                    "Sensitive AI provider details"));

            Assert.That(
                body,
                Does.Not.Contain(
                    "ClientResultException"));

            Assert.That(
                body,
                Does.Not.Contain(
                    "API key"));
        });
    }

    private sealed class TestAuthenticationHandler
        : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult>
            HandleAuthenticateAsync()
        {
            var claims =
                new[]
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        "test-user"),

                    new Claim(
                        ClaimTypes.Name,
                        "test-user")
                };

            var identity =
                new ClaimsIdentity(
                    claims,
                    "Test");

            var principal =
                new ClaimsPrincipal(identity);

            var ticket =
                new AuthenticationTicket(
                    principal,
                    "Test");

            return Task.FromResult(
                AuthenticateResult.Success(ticket));
        }
    }

    private sealed class ThrowingBatchRepository
        : IBatchRepository
    {
        public Task AddAsync(
            Batch batch,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Batch?> GetByIdAsync(
            Guid batchId,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "Sensitive test exception");
        }
    }

    private sealed class ThrowingAiProviderRepository
        : IBatchRepository
    {
        public Task AddAsync(
            Batch batch,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Batch?> GetByIdAsync(
            Guid batchId,
            CancellationToken cancellationToken = default)
        {
            throw new AiProviderUnavailableException(
                "Sensitive AI provider details: quota/API key failure.");
        }
    }
}
