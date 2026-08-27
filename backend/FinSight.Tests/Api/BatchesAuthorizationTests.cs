using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinSight.Tests.Api;

/// <summary>
/// Phase 4A.2 (Batch History): proves GET /api/batches genuinely
/// enforces [Authorize] via the real HTTP pipeline. A directly
/// constructed BatchesController (as used elsewhere for validation-only
/// tests) bypasses the [Authorize] attribute entirely, so only a real
/// WebApplicationFactory request can prove this either way. No database
/// access occurs -- the authentication middleware rejects the request
/// before any controller or repository code runs.
/// </summary>
[TestFixture]
public sealed class BatchesAuthorizationTests
{
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
    public async Task GetBatches_WithoutAuthentication_Returns401()
    {
        using var response =
            await _client.GetAsync("/api/batches?pageNumber=1&pageSize=50");

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
