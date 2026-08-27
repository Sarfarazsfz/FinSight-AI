using System.Net;
using System.Net.Http.Json;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.Tests.Api;

[TestFixture]
public sealed class AuthControllerTests
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
                        services.AddScoped<
                            IAuthService,
                            FakeAuthService>();
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
    public async Task Login_WithValidCredentials_Returns200()
    {
        var request =
            new LoginRequest
            {
                Email =
                    "test@example.com",

                Password =
                    "correct-password"
            };

        using var response =
            await _client.PostAsJsonAsync(
                "/api/auth/login",
                request);

        var body =
            await response.Content
                .ReadFromJsonAsync<LoginResponse>();

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.OK));

        Assert.That(
            body,
            Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(
                body!.AccessToken,
                Is.EqualTo("test-access-token"));

            Assert.That(
                body.TokenType,
                Is.EqualTo("Bearer"));

            Assert.That(
                body.Email,
                Is.EqualTo("test@example.com"));

            Assert.That(
                body.Role,
                Is.EqualTo("Admin"));
        });
    }

    [Test]
    public async Task Login_WithInvalidCredentials_Returns401()
    {
        var request =
            new LoginRequest
            {
                Email =
                    "invalid@example.com",

                Password =
                    "wrong-password"
            };

        using var response =
            await _client.PostAsJsonAsync(
                "/api/auth/login",
                request);

        Assert.That(
            response.StatusCode,
            Is.EqualTo(
                HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Login_WithMissingEmail_Returns400()
    {
        var request =
            new LoginRequest
            {
                Email =
                    string.Empty,

                Password =
                    "password"
            };

        using var response =
            await _client.PostAsJsonAsync(
                "/api/auth/login",
                request);

        Assert.That(
            response.StatusCode,
            Is.EqualTo(
                HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Login_WithMissingPassword_Returns400()
    {
        var request =
            new LoginRequest
            {
                Email =
                    "test@example.com",

                Password =
                    string.Empty
            };

        using var response =
            await _client.PostAsJsonAsync(
                "/api/auth/login",
                request);

        Assert.That(
            response.StatusCode,
            Is.EqualTo(
                HttpStatusCode.BadRequest));
    }

    private sealed class FakeAuthService
        : IAuthService
    {
        public Task<LoginResponse> LoginAsync(
            LoginRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.Email ==
                    "test@example.com" &&
                request.Password ==
                    "correct-password")
            {
                return Task.FromResult(
                    new LoginResponse
                    {
                        AccessToken =
                            "test-access-token",

                        TokenType =
                            "Bearer",

                        ExpiresAtUtc =
                            DateTime.UtcNow.AddMinutes(60),

                        UserId =
                            Guid.Parse(
                                "11111111-1111-1111-1111-111111111111"),

                        Email =
                            "test@example.com",

                        Role =
                            "Admin"
                    });
            }

            throw new UnauthorizedAccessException(
                "Invalid email or password.");
        }
    }
}
