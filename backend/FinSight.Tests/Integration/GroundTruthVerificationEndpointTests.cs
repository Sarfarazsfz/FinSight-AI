using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FinSight.Application.DTOs.Auth;
using FinSight.Application.Evaluation;
using FinSight.Domain.Entities;
using FinSight.Infrastructure.Authentication;
using FinSight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.Tests.Integration;

/// <summary>
/// Phase 4A, Sub-Phase 4A.1: proves the live
/// POST /api/reconciliation/runs/{runId}/ground-truth-verification
/// endpoint end to end through the REAL HTTP pipeline -- real
/// POST /api/auth/login (not a fake IAuthService), real [Authorize]
/// enforcement, real ingestion, real reconciliation, real persisted
/// results/exceptions compared via the shared GroundTruthComparer.
///
/// Runs against the same ephemeral FINSIGHT_TEST_CONNECTION database
/// used by every other integration test (via PostgresIntegrationFixture
/// for the wipe/migrate lifecycle, with the WebApplicationFactory's own
/// configuration overridden to point at that same connection string) --
/// never the developer's real local dev database.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class GroundTruthVerificationEndpointTests
{
    private const string ConnectionEnvironmentVariable =
        "FINSIGHT_TEST_CONNECTION";

    private const string VerifierEmail =
        "ground-truth-http-test@example.com";

    private const string VerifierPassword =
        "Test-Verifier-Password-123!";

    private PostgresIntegrationFixture _fixture = null!;

    private WebApplicationFactory<Program> _factory = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Reuses the same required-env-var contract as every other
        // integration test -- throws clearly if FINSIGHT_TEST_CONNECTION
        // is not set, rather than silently touching a real database.
        _fixture = new PostgresIntegrationFixture();

        var connectionString =
            Environment.GetEnvironmentVariable(
                ConnectionEnvironmentVariable)!;

        _factory =
            new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration(
                        (_, config) =>
                        {
                            // Point the real HTTP pipeline at the SAME
                            // ephemeral test database PostgresIntegrationFixture
                            // wipes/migrates -- never the developer's
                            // real local dev database.
                            config.AddInMemoryCollection(
                                new Dictionary<string, string?>
                                {
                                    ["ConnectionStrings:FinSightDb"] =
                                        connectionString
                                });
                        });
                });
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _factory.Dispose();
    }

    [Test]
    public async Task VerifyGroundTruth_WithMatchingData_ReturnsSuccessPass()
    {
        await _fixture.ResetDatabaseAsync();

        using var client = _factory.CreateClient();

        await CreateVerifierUserAsync();

        var accessToken =
            await LoginAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var batchId =
            await IngestBatchAsync(client);

        var runId =
            await CreateRunAsync(client, batchId);

        var groundTruthRows = new[]
        {
            new GroundTruthRow(
                "TXN-9001", "ExactMatch", "Matched", "EXACT_MATCH",
                "", true, true, true, "Exact", "Exact"),

            new GroundTruthRow(
                "TXN-9002", "MissingBank", "Missing", "SOURCE_ABSENT_BANK",
                "MissingRecord", true, false, true,
                "NotComparable", "NotComparable")
        };

        using var response =
            await client.PostAsJsonAsync(
                $"/api/reconciliation/runs/{runId}/ground-truth-verification",
                groundTruthRows);

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.OK));

        var comparison =
            await response.Content
                .ReadFromJsonAsync<GroundTruthComparisonResult>();

        Assert.That(comparison, Is.Not.Null);

        Assert.That(
            comparison!.IsSuccess,
            Is.True,
            string.Join(Environment.NewLine, comparison.Failures));

        Assert.That(comparison.Failures, Is.Empty);
        Assert.That(comparison.ExpectedTotalUnits, Is.EqualTo(2));
        Assert.That(comparison.ActualTotalUnits, Is.EqualTo(2));
        Assert.That(comparison.ExpectedMatched, Is.EqualTo(1));
        Assert.That(comparison.ActualMatched, Is.EqualTo(1));
        Assert.That(comparison.ExpectedMissing, Is.EqualTo(1));
        Assert.That(comparison.ActualMissing, Is.EqualTo(1));
    }

    [Test]
    public async Task VerifyGroundTruth_WithDeliberateMismatch_ReturnsFailureWithDeterministicOrder()
    {
        await _fixture.ResetDatabaseAsync();

        using var client = _factory.CreateClient();

        await CreateVerifierUserAsync();

        var accessToken =
            await LoginAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var batchId =
            await IngestBatchAsync(client);

        var runId =
            await CreateRunAsync(client, batchId);

        // Deliberately wrong: TXN-9001 really reconciles as Matched, but
        // this ground truth claims Mismatched.
        var groundTruthRows = new[]
        {
            new GroundTruthRow(
                "TXN-9001", "ExactMatch", "Mismatched", "AMOUNT_MISMATCH",
                "AmountMismatch", true, true, true, "Exact", "Exact"),

            new GroundTruthRow(
                "TXN-9002", "MissingBank", "Missing", "SOURCE_ABSENT_BANK",
                "MissingRecord", true, false, true,
                "NotComparable", "NotComparable")
        };

        using var firstResponse =
            await client.PostAsJsonAsync(
                $"/api/reconciliation/runs/{runId}/ground-truth-verification",
                groundTruthRows);

        var firstComparison =
            await firstResponse.Content
                .ReadFromJsonAsync<GroundTruthComparisonResult>();

        using var secondResponse =
            await client.PostAsJsonAsync(
                $"/api/reconciliation/runs/{runId}/ground-truth-verification",
                groundTruthRows);

        var secondComparison =
            await secondResponse.Content
                .ReadFromJsonAsync<GroundTruthComparisonResult>();

        Assert.Multiple(() =>
        {
            Assert.That(
                firstResponse.StatusCode,
                Is.EqualTo(HttpStatusCode.OK));

            Assert.That(firstComparison!.IsSuccess, Is.False);

            Assert.That(
                firstComparison.Failures,
                Has.Some.Contains("TXN-9001: status mismatch"));

            // Deterministic failure ordering: repeating the identical
            // request produces the identical failure list, in the same
            // order.
            Assert.That(
                secondComparison!.Failures,
                Is.EqualTo(firstComparison.Failures));
        });
    }

    [Test]
    public async Task VerifyGroundTruth_WithUnknownRunId_Returns404()
    {
        await _fixture.ResetDatabaseAsync();

        using var client = _factory.CreateClient();

        await CreateVerifierUserAsync();

        var accessToken =
            await LoginAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var groundTruthRows = new[]
        {
            new GroundTruthRow(
                "TXN-1", "ExactMatch", "Matched", "EXACT_MATCH",
                "", true, true, true, "Exact", "Exact")
        };

        using var response =
            await client.PostAsJsonAsync(
                $"/api/reconciliation/runs/{Guid.NewGuid()}/ground-truth-verification",
                groundTruthRows);

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task VerifyGroundTruth_WithEmptyBody_Returns400()
    {
        await _fixture.ResetDatabaseAsync();

        using var client = _factory.CreateClient();

        await CreateVerifierUserAsync();

        var accessToken =
            await LoginAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var batchId =
            await IngestBatchAsync(client);

        var runId =
            await CreateRunAsync(client, batchId);

        using var response =
            await client.PostAsJsonAsync(
                $"/api/reconciliation/runs/{runId}/ground-truth-verification",
                Array.Empty<GroundTruthRow>());

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task VerifyGroundTruth_WithoutAuthentication_Returns401()
    {
        await _fixture.ResetDatabaseAsync();

        using var client = _factory.CreateClient();

        var groundTruthRows = new[]
        {
            new GroundTruthRow(
                "TXN-1", "ExactMatch", "Matched", "EXACT_MATCH",
                "", true, true, true, "Exact", "Exact")
        };

        using var response =
            await client.PostAsJsonAsync(
                $"/api/reconciliation/runs/{Guid.NewGuid()}/ground-truth-verification",
                groundTruthRows);

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    private async Task CreateVerifierUserAsync()
    {
        using var scope =
            _factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        var existing =
            await dbContext.Users
                .FirstOrDefaultAsync(
                    x => x.Email == VerifierEmail);

        if (existing is not null)
        {
            return;
        }

        var passwordService =
            new PasswordService();

        var hash =
            passwordService.HashPassword(VerifierPassword);

        var user =
            new User(VerifierEmail, hash, "User");

        dbContext.Users.Add(user);

        await dbContext.SaveChangesAsync();
    }

    private static async Task<string> LoginAsync(
        HttpClient client)
    {
        using var response =
            await client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest
                {
                    Email = VerifierEmail,
                    Password = VerifierPassword
                });

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.OK),
            "Real HTTP login for the throwaway verifier user must succeed.");

        var login =
            await response.Content
                .ReadFromJsonAsync<LoginResponse>();

        Assert.That(login, Is.Not.Null);

        return login!.AccessToken;
    }

    private static async Task<Guid> IngestBatchAsync(
        HttpClient client)
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent("Ground-Truth HTTP Test"), "batchLabel" },
            { new StringContent("integration-test"), "createdBy" },
            {
                new StringContent(
                    """
                    payment_record_id,transaction_reference,amount,currency,transaction_date,payment_status
                    PAY-009001,TXN-9001,1000.00,INR,2026-08-01,COMPLETED
                    PAY-009002,TXN-9002,2000.00,INR,2026-08-02,COMPLETED
                    """,
                    Encoding.UTF8,
                    "text/csv"),
                "paymentsFile",
                "payments.csv"
            },
            {
                new StringContent(
                    """
                    bank_record_id,transaction_reference,amount,currency,transaction_date,bank_status
                    BANK-009001,TXN-9001,1000.00,INR,2026-08-01,CLEARED
                    """,
                    Encoding.UTF8,
                    "text/csv"),
                "bankFile",
                "bank.csv"
            },
            {
                new StringContent(
                    """
                    settlement_record_id,transaction_reference,amount,currency,transaction_date,settlement_status
                    SET-009001,TXN-9001,1000.00,INR,2026-08-01,SETTLED
                    SET-009002,TXN-9002,2000.00,INR,2026-08-02,SETTLED
                    """,
                    Encoding.UTF8,
                    "text/csv"),
                "settlementsFile",
                "settlements.csv"
            }
        };

        using var response =
            await client.PostAsync("/api/batches", content);

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.Created),
            await response.Content.ReadAsStringAsync());

        var body =
            await response.Content
                .ReadFromJsonAsync<BatchIngestionResultForTest>();

        return body!.BatchId;
    }

    private static async Task<Guid> CreateRunAsync(
        HttpClient client,
        Guid batchId)
    {
        using var response =
            await client.PostAsJsonAsync(
                "/api/reconciliation/runs",
                new { batchId });

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.Created),
            await response.Content.ReadAsStringAsync());

        var body =
            await response.Content
                .ReadFromJsonAsync<ReconciliationRunResultForTest>();

        return body!.RunId;
    }

    // Minimal local deserialization shapes -- avoids depending on
    // FinSight.Application's ingestion/reconciliation DTOs just for two
    // field reads in this HTTP-level test.
    private sealed class BatchIngestionResultForTest
    {
        public Guid BatchId { get; set; }
    }

    private sealed class ReconciliationRunResultForTest
    {
        public Guid RunId { get; set; }
    }
}
