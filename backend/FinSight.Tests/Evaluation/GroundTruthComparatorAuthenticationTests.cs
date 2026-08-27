using System.Net;
using System.Text;
using FinSight.DataGenerator.Validation;

namespace FinSight.Tests.Evaluation;

/// <summary>
/// Proves GroundTruthComparator's Phase 3 authentication behavior in
/// full isolation from any real ASP.NET host or database, via a fake
/// HttpMessageHandler injected through the new optional HttpClient
/// constructor parameter -- deterministic, no network, no DB.
///
/// Verifies: a successful login is followed by an authenticated fetch
/// carrying the returned bearer token; a 401 (or 403) at login or at
/// the results/exceptions endpoints is surfaced as
/// GroundTruthAuthenticationException, never folded into a
/// GroundTruthComparisonResult's Failures list; and the token is
/// obtained exactly once and reused for every request in one
/// evaluation run.
/// </summary>
[TestFixture]
public sealed class GroundTruthComparatorAuthenticationTests
{
    private const string BaseUrl = "http://localhost:5180";

    private const string FakeToken = "fake-jwt-token-value";

    private static readonly string LoginSuccessJson =
        """
        {
            "AccessToken": "fake-jwt-token-value",
            "TokenType": "Bearer",
            "ExpiresAtUtc": "2026-01-01T00:00:00Z",
            "UserId": "11111111-1111-1111-1111-111111111111",
            "Email": "verifier@test.local",
            "Role": "User"
        }
        """;

    private static readonly string EmptyResultsPageJson =
        """
        {
            "Items": [
                {
                    "ResultId": "22222222-2222-2222-2222-222222222222",
                    "RunId": "33333333-3333-3333-3333-333333333333",
                    "NormalizedTransactionId": "44444444-4444-4444-4444-444444444444",
                    "TransactionReference": "TXN-1",
                    "Status": "Matched",
                    "StrategyUsed": "StrategyOne_ExactReferenceMatch",
                    "ReasonCode": "EXACT_MATCH",
                    "CreatedAt": "2026-01-01T00:00:00Z"
                }
            ],
            "PageNumber": 1,
            "PageSize": 100,
            "TotalCount": 1,
            "TotalPages": 1
        }
        """;

    private static readonly string EmptyExceptionsPageJson =
        """
        {
            "Items": [],
            "PageNumber": 1,
            "PageSize": 100,
            "TotalCount": 0,
            "TotalPages": 1
        }
        """;

    [Test]
    public async Task CompareAsync_WithValidCredentials_LogsInAndFetchesWithBearerToken()
    {
        var handler =
            new FakeHttpMessageHandler(
                request =>
                {
                    var path = request.RequestUri!.AbsolutePath;

                    if (path.EndsWith("/api/auth/login"))
                    {
                        return JsonResponse(
                            HttpStatusCode.OK,
                            LoginSuccessJson);
                    }

                    if (path.Contains("/results"))
                    {
                        return JsonResponse(
                            HttpStatusCode.OK,
                            EmptyResultsPageJson);
                    }

                    if (path.Contains("/exceptions"))
                    {
                        return JsonResponse(
                            HttpStatusCode.OK,
                            EmptyExceptionsPageJson);
                    }

                    return new HttpResponseMessage(
                        HttpStatusCode.NotFound);
                });

        var groundTruthFile =
            WriteGroundTruthCsv();

        try
        {
            using var httpClient =
                new HttpClient(handler);

            var comparator =
                new GroundTruthComparator(httpClient);

            var comparison =
                await comparator.CompareAsync(
                    BaseUrl,
                    Guid.NewGuid(),
                    groundTruthFile,
                    "verifier@test.local",
                    "correct-password");

            Assert.That(
                comparison.IsSuccess,
                Is.True,
                string.Join(
                    Environment.NewLine,
                    comparison.Failures));

            var resultsRequest =
                handler.Requests.Single(
                    r => r.RequestUri!.AbsolutePath.Contains("/results"));

            Assert.That(
                resultsRequest.Headers.Authorization,
                Is.Not.Null);

            Assert.That(
                resultsRequest.Headers.Authorization!.Scheme,
                Is.EqualTo("Bearer"));

            Assert.That(
                resultsRequest.Headers.Authorization.Parameter,
                Is.EqualTo(FakeToken));
        }
        finally
        {
            File.Delete(groundTruthFile);
        }
    }

    [Test]
    public void CompareAsync_WhenLoginReturns401_ThrowsGroundTruthAuthenticationException()
    {
        var handler =
            new FakeHttpMessageHandler(
                request =>
                    new HttpResponseMessage(
                        HttpStatusCode.Unauthorized)
                    {
                        Content =
                            new StringContent(
                                "{\"title\":\"Unauthorized\"}",
                                Encoding.UTF8,
                                "application/problem+json")
                    });

        var groundTruthFile =
            WriteGroundTruthCsv();

        try
        {
            using var httpClient =
                new HttpClient(handler);

            var comparator =
                new GroundTruthComparator(httpClient);

            var exception =
                Assert.ThrowsAsync<
                    GroundTruthAuthenticationException>(
                    async () =>
                        await comparator.CompareAsync(
                            BaseUrl,
                            Guid.NewGuid(),
                            groundTruthFile,
                            "verifier@test.local",
                            "wrong-password"));

            Assert.That(
                exception!.Message,
                Does.Contain("authentication failed"));

            // Never reaches the results/exceptions endpoints once login
            // itself failed.
            Assert.That(
                handler.Requests,
                Has.Count.EqualTo(1));
        }
        finally
        {
            File.Delete(groundTruthFile);
        }
    }

    [Test]
    public void CompareAsync_WhenResultsEndpointReturns403_ThrowsGroundTruthAuthenticationException()
    {
        var handler =
            new FakeHttpMessageHandler(
                request =>
                {
                    var path = request.RequestUri!.AbsolutePath;

                    if (path.EndsWith("/api/auth/login"))
                    {
                        return JsonResponse(
                            HttpStatusCode.OK,
                            LoginSuccessJson);
                    }

                    // Login succeeds (a genuine token is issued), but
                    // the token is rejected as insufficiently
                    // privileged when actually used -- this must be
                    // reported as an auth failure, not a data mismatch.
                    return new HttpResponseMessage(
                        HttpStatusCode.Forbidden)
                    {
                        Content =
                            new StringContent(
                                "{\"title\":\"Forbidden\"}",
                                Encoding.UTF8,
                                "application/problem+json")
                    };
                });

        var groundTruthFile =
            WriteGroundTruthCsv();

        try
        {
            using var httpClient =
                new HttpClient(handler);

            var comparator =
                new GroundTruthComparator(httpClient);

            var exception =
                Assert.ThrowsAsync<
                    GroundTruthAuthenticationException>(
                    async () =>
                        await comparator.CompareAsync(
                            BaseUrl,
                            Guid.NewGuid(),
                            groundTruthFile,
                            "verifier@test.local",
                            "correct-password"));

            Assert.That(
                exception!.Message,
                Does.Contain("authentication failure"));
        }
        finally
        {
            File.Delete(groundTruthFile);
        }
    }

    [Test]
    public async Task CompareAsync_LogsInExactlyOnce_AndReusesTheSameTokenForEveryRequest()
    {
        var handler =
            new FakeHttpMessageHandler(
                request =>
                {
                    var path = request.RequestUri!.AbsolutePath;

                    if (path.EndsWith("/api/auth/login"))
                    {
                        return JsonResponse(
                            HttpStatusCode.OK,
                            LoginSuccessJson);
                    }

                    if (path.Contains("/results"))
                    {
                        return JsonResponse(
                            HttpStatusCode.OK,
                            EmptyResultsPageJson);
                    }

                    return JsonResponse(
                        HttpStatusCode.OK,
                        EmptyExceptionsPageJson);
                });

        var groundTruthFile =
            WriteGroundTruthCsv();

        try
        {
            using var httpClient =
                new HttpClient(handler);

            var comparator =
                new GroundTruthComparator(httpClient);

            await comparator.CompareAsync(
                BaseUrl,
                Guid.NewGuid(),
                groundTruthFile,
                "verifier@test.local",
                "correct-password");

            var loginRequests =
                handler.Requests
                    .Where(
                        r => r.RequestUri!.AbsolutePath
                            .EndsWith("/api/auth/login"))
                    .ToList();

            Assert.That(
                loginRequests,
                Has.Count.EqualTo(1),
                "Login must happen exactly once per evaluation run.");

            var fetchRequests =
                handler.Requests
                    .Where(
                        r => !r.RequestUri!.AbsolutePath
                            .EndsWith("/api/auth/login"))
                    .ToList();

            Assert.That(
                fetchRequests,
                Is.Not.Empty);

            Assert.That(
                fetchRequests.All(
                    r =>
                        r.Headers.Authorization != null &&
                        r.Headers.Authorization.Parameter == FakeToken),
                Is.True,
                "Every fetch must reuse the exact token returned by " +
                "the single login call.");
        }
        finally
        {
            File.Delete(groundTruthFile);
        }
    }

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode statusCode,
        string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json")
        };
    }

    private static string WriteGroundTruthCsv()
    {
        var path =
            Path.Combine(
                Path.GetTempPath(),
                $"ground-truth-auth-test-{Guid.NewGuid()}.csv");

        File.WriteAllText(
            path,
            """
            transaction_reference,scenario_type,expected_status,expected_reason_code,expected_exception_category,expected_payment_present,expected_bank_present,expected_settlement_present,expected_amount_relationship,expected_date_relationship
            TXN-1,ExactMatch,Matched,EXACT_MATCH,,true,true,true,Exact,Exact
            """);

        return path;
    }

    private sealed class FakeHttpMessageHandler
        : HttpMessageHandler
    {
        private readonly Func<
            HttpRequestMessage,
            HttpResponseMessage> _respond;

        public FakeHttpMessageHandler(
            Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);

            return Task.FromResult(
                _respond(request));
        }
    }
}
