using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FinSight.Application.DTOs.Auth;
using FinSight.Infrastructure.Authentication;
using FinSight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.Tests.Integration;

/// <summary>
/// Phase 4A.3 (Structured Batch Validation Errors): proves
/// POST /api/batches returns a structured "errors" ProblemDetails
/// extension on validation failure through the REAL HTTP pipeline (real
/// [Authorize] enforcement, real CSV parsing, real
/// BatchIngestionValidator) -- while detail/status/title/content-type stay
/// exactly what they are today. Confirms the [Authorize] boundary on this
/// endpoint is unaffected by the change.
///
/// Runs against the same ephemeral FINSIGHT_TEST_CONNECTION database used
/// by every other integration test (via PostgresIntegrationFixture for the
/// wipe/migrate lifecycle).
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class BatchValidationErrorsIntegrationTests
{
    private const string ConnectionEnvironmentVariable =
        "FINSIGHT_TEST_CONNECTION";

    private const string VerifierEmail =
        "batch-validation-http-test@example.com";

    private const string VerifierPassword =
        "Test-Verifier-Password-123!";

    private PostgresIntegrationFixture _fixture = null!;

    private WebApplicationFactory<Program> _factory = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
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
    public async Task CreateBatch_WithInvalidPaymentsCsv_Returns400WithStructuredErrors()
    {
        await _fixture.ResetDatabaseAsync();

        using var client = _factory.CreateClient();

        await CreateVerifierUserAsync();

        var accessToken =
            await LoginAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // Row 2's payment_record_id is blank -> exactly one validation
        // error: "Required value is missing." on field payment_record_id.
        // Bank/settlement rows are deliberately valid so the response
        // contains only the one expected error.
        using var content = new MultipartFormDataContent
        {
            { new StringContent("Invalid Payments Batch"), "batchLabel" },
            { new StringContent("integration-test"), "createdBy" },
            {
                new StringContent(
                    """
                    payment_record_id,transaction_reference,amount,currency,transaction_date,payment_status
                    ,TXN-9001,1000.00,INR,2026-08-01,COMPLETED
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
                    """,
                    Encoding.UTF8,
                    "text/csv"),
                "settlementsFile",
                "settlements.csv"
            }
        };

        using var response =
            await client.PostAsync("/api/batches", content);

        var body =
            await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(
                response.StatusCode,
                Is.EqualTo(HttpStatusCode.BadRequest),
                body);

            Assert.That(
                response.Content.Headers.ContentType?.MediaType,
                Is.EqualTo("application/problem+json"));

            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;

            Assert.That(
                root.GetProperty("title").GetString(),
                Is.EqualTo("Bad Request"));

            Assert.That(
                root.GetProperty("status").GetInt32(),
                Is.EqualTo(400));

            var detail = root.GetProperty("detail").GetString();

            Assert.That(detail, Is.Not.Null.And.Not.Empty);
            Assert.That(detail, Does.Contain("Payment row 2"));
            Assert.That(detail, Does.Contain("payment_record_id"));

            Assert.That(
                root.TryGetProperty("errors", out var errors),
                Is.True,
                "ProblemDetails response must contain the new 'errors' extension.");

            Assert.That(errors.GetArrayLength(), Is.EqualTo(1));

            var firstError = errors[0];

            Assert.That(
                firstError.GetProperty("source").GetString(),
                Is.EqualTo("Payment"));

            Assert.That(
                firstError.GetProperty("rowNumber").GetInt32(),
                Is.EqualTo(2));

            Assert.That(
                firstError.GetProperty("field").GetString(),
                Is.EqualTo("payment_record_id"));

            Assert.That(
                firstError.GetProperty("message").GetString(),
                Is.EqualTo("Required value is missing."));
        });
    }

    [Test]
    public async Task CreateBatch_WithInvalidBankCsv_Returns400WithStructuredErrors()
    {
        await _fixture.ResetDatabaseAsync();

        using var client = _factory.CreateClient();

        await CreateVerifierUserAsync();

        var accessToken =
            await LoginAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // Row 2's bank_record_id doesn't match the BANK-000001 pattern ->
        // exactly one validation error, sourced from Bank this time.
        // Payments/settlement rows are deliberately valid.
        using var content = new MultipartFormDataContent
        {
            { new StringContent("Invalid Bank Batch"), "batchLabel" },
            { new StringContent("integration-test"), "createdBy" },
            {
                new StringContent(
                    """
                    payment_record_id,transaction_reference,amount,currency,transaction_date,payment_status
                    PAY-009001,TXN-9001,1000.00,INR,2026-08-01,COMPLETED
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
                    BANK-1,TXN-9001,1000.00,INR,2026-08-01,CLEARED
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
                    """,
                    Encoding.UTF8,
                    "text/csv"),
                "settlementsFile",
                "settlements.csv"
            }
        };

        using var response =
            await client.PostAsync("/api/batches", content);

        var body =
            await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(
                response.StatusCode,
                Is.EqualTo(HttpStatusCode.BadRequest),
                body);

            Assert.That(
                response.Content.Headers.ContentType?.MediaType,
                Is.EqualTo("application/problem+json"));

            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;

            Assert.That(
                root.GetProperty("title").GetString(),
                Is.EqualTo("Bad Request"));

            Assert.That(
                root.GetProperty("status").GetInt32(),
                Is.EqualTo(400));

            var detail = root.GetProperty("detail").GetString();

            Assert.That(detail, Is.Not.Null.And.Not.Empty);
            Assert.That(detail, Does.Contain("Bank row 2"));

            Assert.That(
                root.TryGetProperty("errors", out var errors),
                Is.True);

            Assert.That(errors.GetArrayLength(), Is.EqualTo(1));

            var firstError = errors[0];

            Assert.That(
                firstError.GetProperty("source").GetString(),
                Is.EqualTo("Bank"));

            Assert.That(
                firstError.GetProperty("rowNumber").GetInt32(),
                Is.EqualTo(2));

            Assert.That(
                firstError.GetProperty("field").GetString(),
                Is.EqualTo("bank_record_id"));

            Assert.That(
                firstError.GetProperty("message").GetString(),
                Is.EqualTo("Must match BANK-000001 style."));
        });
    }

    [Test]
    public async Task CreateBatch_WithoutAuthentication_Returns401()
    {
        await _fixture.ResetDatabaseAsync();

        using var client = _factory.CreateClient();

        using var content = new MultipartFormDataContent
        {
            { new StringContent("Unauthenticated Batch"), "batchLabel" },
            { new StringContent("integration-test"), "createdBy" },
            {
                new StringContent("x", Encoding.UTF8, "text/csv"),
                "paymentsFile",
                "payments.csv"
            },
            {
                new StringContent("x", Encoding.UTF8, "text/csv"),
                "bankFile",
                "bank.csv"
            },
            {
                new StringContent("x", Encoding.UTF8, "text/csv"),
                "settlementsFile",
                "settlements.csv"
            }
        };

        using var response =
            await client.PostAsync("/api/batches", content);

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
            new FinSight.Domain.Entities.User(
                VerifierEmail, hash, "User");

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
}
