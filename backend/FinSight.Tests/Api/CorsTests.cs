using Microsoft.AspNetCore.Mvc.Testing;

namespace FinSight.Tests.Api;

/// <summary>
/// Proves the CORS policy added in Phase 3 actually behaves as designed:
/// the configured Angular dev-server origin is allowed, an arbitrary
/// other origin is not, and no AllowAnyOrigin()/AllowCredentials() is in
/// effect. Uses a CORS preflight (OPTIONS) request against the real
/// middleware pipeline via WebApplicationFactory -- no database access
/// required, since CORS is handled entirely by middleware before any
/// controller/repository code runs.
/// </summary>
[TestFixture]
public sealed class CorsTests
{
    private const string AllowedDevOrigin =
        "http://localhost:4200";

    private const string DisallowedOrigin =
        "http://evil-origin.example";

    private WebApplicationFactory<Program> _factory = null!;

    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory =
            new WebApplicationFactory<Program>();

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
    public async Task Preflight_FromAllowedAngularDevOrigin_ReceivesAccessControlAllowOriginHeader()
    {
        using var request =
            new HttpRequestMessage(
                HttpMethod.Options,
                "/api/auth/login");

        request.Headers.Add(
            "Origin",
            AllowedDevOrigin);

        request.Headers.Add(
            "Access-Control-Request-Method",
            "POST");

        using var response =
            await _client.SendAsync(request);

        Assert.That(
            response.Headers.TryGetValues(
                "Access-Control-Allow-Origin",
                out var allowedOrigins),
            Is.True,
            "Expected an Access-Control-Allow-Origin header for the " +
            "configured Angular dev-server origin.");

        Assert.That(
            allowedOrigins,
            Does.Contain(AllowedDevOrigin));
    }

    [Test]
    public async Task Preflight_FromArbitraryOrigin_ReceivesNoAccessControlAllowOriginHeader()
    {
        using var request =
            new HttpRequestMessage(
                HttpMethod.Options,
                "/api/auth/login");

        request.Headers.Add(
            "Origin",
            DisallowedOrigin);

        request.Headers.Add(
            "Access-Control-Request-Method",
            "POST");

        using var response =
            await _client.SendAsync(request);

        Assert.That(
            response.Headers.Contains(
                "Access-Control-Allow-Origin"),
            Is.False,
            "An arbitrary, non-configured origin must not be granted " +
            "CORS access -- AllowAnyOrigin() must never be in effect.");
    }

    [Test]
    public async Task Preflight_FromAllowedOrigin_DoesNotReceiveAllowCredentialsHeader()
    {
        // Auth is a Bearer token in the Authorization header, not a
        // cookie, so AllowCredentials() is not needed and must not be
        // enabled -- confirms the policy stays as narrow as possible.
        using var request =
            new HttpRequestMessage(
                HttpMethod.Options,
                "/api/auth/login");

        request.Headers.Add(
            "Origin",
            AllowedDevOrigin);

        request.Headers.Add(
            "Access-Control-Request-Method",
            "POST");

        using var response =
            await _client.SendAsync(request);

        Assert.That(
            response.Headers.Contains(
                "Access-Control-Allow-Credentials"),
            Is.False);
    }
}
