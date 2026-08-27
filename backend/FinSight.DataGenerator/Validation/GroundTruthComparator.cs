using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinSight.DataGenerator.Models;

namespace FinSight.DataGenerator.Validation;

public sealed class GroundTruthComparator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Optional injected HttpClient -- production usage (Program.cs)
    // leaves this null and gets a real, freshly-created HttpClient per
    // call (unchanged behavior). Tests can inject a TestServer-backed
    // HttpClient (e.g. WebApplicationFactory<Program>().CreateClient())
    // to exercise the REAL login + [Authorize] + controller pipeline
    // end to end, without a real network listener.
    private readonly HttpClient? _injectedHttpClient;

    public GroundTruthComparator(
        HttpClient? httpClient = null)
    {
        _injectedHttpClient = httpClient;
    }

    public async Task<GroundTruthComparisonResult> CompareAsync(
        string baseUrl,
        Guid runId,
        string groundTruthFilePath,
        string verifierEmail,
        string verifierPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException(
                "Base URL is required.",
                nameof(baseUrl));
        }

        if (runId == Guid.Empty)
        {
            throw new ArgumentException(
                "Run ID is required.",
                nameof(runId));
        }

        if (string.IsNullOrWhiteSpace(groundTruthFilePath))
        {
            throw new ArgumentException(
                "Ground-truth file path is required.",
                nameof(groundTruthFilePath));
        }

        if (string.IsNullOrWhiteSpace(verifierEmail))
        {
            throw new ArgumentException(
                "A verifier email is required to authenticate against " +
                "the [Authorize]-protected reconciliation endpoints.",
                nameof(verifierEmail));
        }

        if (string.IsNullOrWhiteSpace(verifierPassword))
        {
            throw new ArgumentException(
                "A verifier password is required to authenticate " +
                "against the [Authorize]-protected reconciliation " +
                "endpoints.",
                nameof(verifierPassword));
        }

        if (!File.Exists(groundTruthFilePath))
        {
            throw new FileNotFoundException(
                "Ground-truth file was not found.",
                groundTruthFilePath);
        }

        var expectedRows =
            LoadGroundTruth(groundTruthFilePath);

        var trimmedBaseUrl =
            baseUrl.TrimEnd('/');

        // Log in exactly once per evaluation run, through the real
        // POST /api/auth/login endpoint -- no [AllowAnonymous] added,
        // no hardcoded token, no weakening of [Authorize]. The
        // short-lived JWT is cached in this local variable for the
        // remainder of this single CompareAsync call.
        var accessToken =
            await LoginAsync(
                trimmedBaseUrl,
                verifierEmail,
                verifierPassword,
                cancellationToken);

        // The real endpoints (ReconciliationController.GetResults /
        // GetExceptions) return a PagedResponse<T> envelope, not a bare
        // JSON array, and cap each page at pageSize (default 50, max
        // 100). Every page must be fetched and accumulated -- a single
        // unpaged GET would silently miss records beyond the first page.
        var actualResults =
            await AccumulatePagesAsync<ActualResult>(
                pageNumber =>
                    GetAsync<PagedEnvelope<ActualResult>>(
                        $"{trimmedBaseUrl}/api/reconciliation/runs/{runId}/results" +
                        $"?pageNumber={pageNumber}&pageSize=100",
                        accessToken,
                        cancellationToken));

        var actualExceptions =
            await AccumulatePagesAsync<ActualException>(
                pageNumber =>
                    GetAsync<PagedEnvelope<ActualException>>(
                        $"{trimmedBaseUrl}/api/reconciliation/runs/{runId}/exceptions" +
                        $"?pageNumber={pageNumber}&pageSize=100",
                        accessToken,
                        cancellationToken));

        return Compare(
            expectedRows,
            actualResults,
            actualExceptions);
    }

    /// <summary>
    /// Authenticates via the real, unmodified POST /api/auth/login
    /// endpoint using a dedicated verification identity. Throws
    /// GroundTruthAuthenticationException (never folded into
    /// Failures/IsSuccess) on 401/403 or a missing token, so an
    /// authentication problem is never misread as a ground-truth data
    /// mismatch.
    /// </summary>
    private async Task<string> LoginAsync(
        string baseUrl,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var httpClient =
            _injectedHttpClient ?? new HttpClient();

        try
        {
            using var response =
                await httpClient.PostAsJsonAsync(
                    $"{baseUrl}/api/auth/login",
                    new LoginRequestPayload
                    {
                        Email = email,
                        Password = password
                    },
                    cancellationToken);

            var content =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized ||
                response.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new GroundTruthAuthenticationException(
                    "Ground-truth verifier authentication failed " +
                    $"({(int)response.StatusCode} {response.StatusCode}). " +
                    "Check the verifier credentials (FINSIGHT_VERIFIER_EMAIL " +
                    "/ FINSIGHT_VERIFIER_PASSWORD) -- this is an " +
                    "authentication failure, not a ground-truth mismatch.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"POST {baseUrl}/api/auth/login failed with " +
                    $"{(int)response.StatusCode} {response.StatusCode}: " +
                    content);
            }

            var login =
                JsonSerializer.Deserialize<LoginResponsePayload>(
                    content,
                    JsonOptions);

            if (login is null ||
                string.IsNullOrWhiteSpace(login.AccessToken))
            {
                throw new GroundTruthAuthenticationException(
                    "Login succeeded but no access token was returned.");
            }

            return login.AccessToken;
        }
        finally
        {
            if (_injectedHttpClient is null)
            {
                httpClient.Dispose();
            }
        }
    }

    /// <summary>
    /// Fetches every page of a PagedResponse-shaped endpoint and
    /// accumulates all items into one list. Independently testable with
    /// an in-memory fetchPage delegate -- no HTTP client required.
    /// </summary>
    public static async Task<List<TItem>> AccumulatePagesAsync<TItem>(
        Func<int, Task<PagedEnvelope<TItem>>> fetchPage)
    {
        var items = new List<TItem>();
        var pageNumber = 1;

        while (true)
        {
            var page =
                await fetchPage(pageNumber);

            items.AddRange(page.Items);

            if (page.Items.Count == 0 ||
                pageNumber >= page.TotalPages)
            {
                break;
            }

            pageNumber++;
        }

        return items;
    }

    public GroundTruthComparisonResult Compare(
        IReadOnlyList<GroundTruthRow> expectedRows,
        IReadOnlyList<ActualResult> actualResults,
        IReadOnlyList<ActualException> actualExceptions)
    {
        var failures = new List<string>();

        ValidateExpectedReferences(
            expectedRows,
            failures);

        ValidateActualReferences(
            actualResults,
            failures);

        CompareTransactionLevelResults(
            expectedRows,
            actualResults,
            failures);

        CompareTransactionLevelExceptions(
            expectedRows,
            actualExceptions,
            failures);

        var expectedCounts =
            BuildExpectedStatusCounts(expectedRows);

        var actualCounts =
            BuildActualStatusCounts(actualResults);

        CompareStatusCount(
            "Matched",
            expectedCounts.Matched,
            actualCounts.Matched,
            failures);

        CompareStatusCount(
            "Mismatched",
            expectedCounts.Mismatched,
            actualCounts.Mismatched,
            failures);

        CompareStatusCount(
            "Missing",
            expectedCounts.Missing,
            actualCounts.Missing,
            failures);

        CompareStatusCount(
            "Duplicate",
            expectedCounts.Duplicate,
            actualCounts.Duplicate,
            failures);

        CompareStatusCount(
            "Unresolved",
            expectedCounts.Unresolved,
            actualCounts.Unresolved,
            failures);

        var expectedMatchRate =
            CalculateMatchRate(
                expectedCounts.Matched,
                expectedRows.Count);

        var actualMatchRate =
            CalculateMatchRate(
                actualCounts.Matched,
                actualResults.Count);

        if (expectedMatchRate != actualMatchRate)
        {
            failures.Add(
                $"Match rate mismatch. " +
                $"Expected {expectedMatchRate:0.00}, " +
                $"actual {actualMatchRate:0.00}.");
        }

        CompareReasonCodeCounts(
            expectedRows,
            actualResults,
            failures);

        CompareExceptionCategoryCounts(
            expectedRows,
            actualExceptions,
            failures);

        var expectedExceptionCount =
            expectedRows.Count(
                x => !string.IsNullOrWhiteSpace(
                    x.ExpectedExceptionCategory));

        if (expectedExceptionCount !=
            actualExceptions.Count)
        {
            failures.Add(
                $"Exception count mismatch. " +
                $"Expected {expectedExceptionCount}, " +
                $"actual {actualExceptions.Count}.");
        }

        return new GroundTruthComparisonResult
        {
            IsSuccess = failures.Count == 0,

            ExpectedTotalUnits =
                expectedRows.Count,

            ActualTotalUnits =
                actualResults.Count,

            ExpectedMatched =
                expectedCounts.Matched,

            ActualMatched =
                actualCounts.Matched,

            ExpectedMismatched =
                expectedCounts.Mismatched,

            ActualMismatched =
                actualCounts.Mismatched,

            ExpectedMissing =
                expectedCounts.Missing,

            ActualMissing =
                actualCounts.Missing,

            ExpectedDuplicate =
                expectedCounts.Duplicate,

            ActualDuplicate =
                actualCounts.Duplicate,

            ExpectedUnresolved =
                expectedCounts.Unresolved,

            ActualUnresolved =
                actualCounts.Unresolved,

            ExpectedMatchRate =
                expectedMatchRate,

            ActualMatchRate =
                actualMatchRate,

            Failures =
                failures
        };
    }

    private static void CompareTransactionLevelResults(
        IReadOnlyList<GroundTruthRow> expectedRows,
        IReadOnlyList<ActualResult> actualResults,
        List<string> failures)
    {
        var expectedByReference =
            expectedRows.ToDictionary(
                x => x.TransactionReference,
                StringComparer.Ordinal);

        var actualByReference =
            actualResults.ToDictionary(
                x => x.TransactionReference,
                StringComparer.Ordinal);

        foreach (var expected in expectedRows)
        {
            if (!actualByReference.TryGetValue(
                    expected.TransactionReference,
                    out var actual))
            {
                failures.Add(
                    $"{expected.TransactionReference}: " +
                    "missing from reconciliation results.");

                continue;
            }

            if (!string.Equals(
                    expected.ExpectedStatus,
                    actual.Status,
                    StringComparison.OrdinalIgnoreCase))
            {
                failures.Add(
                    $"{expected.TransactionReference}: status mismatch. " +
                    $"Expected '{expected.ExpectedStatus}', " +
                    $"actual '{actual.Status}'.");
            }

            if (!string.Equals(
                    expected.ExpectedReasonCode,
                    actual.ReasonCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                failures.Add(
                    $"{expected.TransactionReference}: reason-code mismatch. " +
                    $"Expected '{expected.ExpectedReasonCode}', " +
                    $"actual '{actual.ReasonCode}'.");
            }
        }

        foreach (var actual in actualResults)
        {
            if (!expectedByReference.ContainsKey(
                    actual.TransactionReference))
            {
                failures.Add(
                    $"{actual.TransactionReference}: " +
                    "returned by reconciliation but missing from ground truth.");
            }
        }
    }

    private static void CompareTransactionLevelExceptions(
        IReadOnlyList<GroundTruthRow> expectedRows,
        IReadOnlyList<ActualException> actualExceptions,
        List<string> failures)
    {
        var expectedExceptionByReference =
            expectedRows
                .Where(
                    x => !string.IsNullOrWhiteSpace(
                        x.ExpectedExceptionCategory))
                .ToDictionary(
                    x => x.TransactionReference,
                    StringComparer.Ordinal);

        var actualExceptionByReference =
            new Dictionary<string, ActualException>(
                StringComparer.Ordinal);

        foreach (var exception in actualExceptions)
        {
            if (string.IsNullOrWhiteSpace(
                    exception.TransactionReference))
            {
                failures.Add(
                    $"Exception '{exception.ExceptionId}' " +
                    "does not contain a transaction reference.");

                continue;
            }

            if (!actualExceptionByReference.TryAdd(
                    exception.TransactionReference,
                    exception))
            {
                failures.Add(
                    $"{exception.TransactionReference}: " +
                    "multiple exceptions found for one reconciliation unit.");
            }
        }

        foreach (var expected in expectedRows)
        {
            var shouldHaveException =
                !string.IsNullOrWhiteSpace(
                    expected.ExpectedExceptionCategory);

            var hasException =
                actualExceptionByReference.ContainsKey(
                    expected.TransactionReference);

            if (shouldHaveException && !hasException)
            {
                failures.Add(
                    $"{expected.TransactionReference}: " +
                    $"expected exception '{expected.ExpectedExceptionCategory}' " +
                    "but no exception was returned.");

                continue;
            }

            if (!shouldHaveException && hasException)
            {
                failures.Add(
                    $"{expected.TransactionReference}: " +
                    "unexpected exception returned.");

                continue;
            }

            if (!shouldHaveException)
            {
                continue;
            }

            var actual =
                actualExceptionByReference[
                    expected.TransactionReference];

            if (!string.Equals(
                    expected.ExpectedExceptionCategory,
                    actual.Category,
                    StringComparison.OrdinalIgnoreCase))
            {
                failures.Add(
                    $"{expected.TransactionReference}: exception category mismatch. " +
                    $"Expected '{expected.ExpectedExceptionCategory}', " +
                    $"actual '{actual.Category}'.");
            }
        }

        foreach (var actual in actualExceptions)
        {
            if (string.IsNullOrWhiteSpace(
                    actual.TransactionReference))
            {
                continue;
            }

            if (!expectedExceptionByReference.ContainsKey(
                    actual.TransactionReference))
            {
                failures.Add(
                    $"{actual.TransactionReference}: " +
                    "unexpected exception reference.");
            }
        }
    }

    private static void ValidateExpectedReferences(
        IReadOnlyList<GroundTruthRow> rows,
        List<string> failures)
    {
        var duplicates =
            rows.GroupBy(
                    x => x.TransactionReference,
                    StringComparer.Ordinal)
                .Where(
                    x => x.Count() > 1);

        foreach (var duplicate in duplicates)
        {
            failures.Add(
                $"Ground truth contains duplicate transaction reference " +
                $"'{duplicate.Key}'.");
        }
    }

    private static void ValidateActualReferences(
        IReadOnlyList<ActualResult> rows,
        List<string> failures)
    {
        var duplicates =
            rows.GroupBy(
                    x => x.TransactionReference,
                    StringComparer.Ordinal)
                .Where(
                    x => x.Count() > 1);

        foreach (var duplicate in duplicates)
        {
            failures.Add(
                $"Reconciliation results contain duplicate transaction reference " +
                $"'{duplicate.Key}'.");
        }
    }

    private static List<GroundTruthRow> LoadGroundTruth(
        string filePath)
    {
        var lines =
            File.ReadAllLines(filePath);

        if (lines.Length <= 1)
        {
            throw new InvalidOperationException(
                "Ground-truth file contains no data rows.");
        }

        var rows =
            new List<GroundTruthRow>();

        for (var index = 1;
             index < lines.Length;
             index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                continue;
            }

            var fields =
                ParseCsvLine(lines[index]);

            if (fields.Count < 10)
            {
                throw new InvalidOperationException(
                    $"Ground-truth row {index + 1} " +
                    "contains fewer than 10 columns.");
            }

            rows.Add(
                new GroundTruthRow(
                    fields[0],
                    fields[1],
                    fields[2],
                    fields[3],
                    fields[4],
                    bool.Parse(fields[5]),
                    bool.Parse(fields[6]),
                    bool.Parse(fields[7]),
                    fields[8],
                    fields[9]));
        }

        return rows;
    }

    private static List<string> ParseCsvLine(
        string line)
    {
        var fields =
            new List<string>();

        var current =
            new List<char>();

        var insideQuotes =
            false;

        for (var index = 0;
             index < line.Length;
             index++)
        {
            var character =
                line[index];

            if (character == '"')
            {
                if (insideQuotes &&
                    index + 1 < line.Length &&
                    line[index + 1] == '"')
                {
                    current.Add('"');
                    index++;
                    continue;
                }

                insideQuotes =
                    !insideQuotes;

                continue;
            }

            if (character == ',' &&
                !insideQuotes)
            {
                fields.Add(
                    new string(
                        current.ToArray()));

                current.Clear();

                continue;
            }

            current.Add(character);
        }

        fields.Add(
            new string(
                current.ToArray()));

        return fields;
    }

    private static ExpectedStatusCounts BuildExpectedStatusCounts(
        IReadOnlyList<GroundTruthRow> rows)
    {
        return new ExpectedStatusCounts
        {
            Matched =
                rows.Count(
                    x => string.Equals(
                        x.ExpectedStatus,
                        "Matched",
                        StringComparison.OrdinalIgnoreCase)),

            Mismatched =
                rows.Count(
                    x => string.Equals(
                        x.ExpectedStatus,
                        "Mismatched",
                        StringComparison.OrdinalIgnoreCase)),

            Missing =
                rows.Count(
                    x => string.Equals(
                        x.ExpectedStatus,
                        "Missing",
                        StringComparison.OrdinalIgnoreCase)),

            Duplicate =
                rows.Count(
                    x => string.Equals(
                        x.ExpectedStatus,
                        "Duplicate",
                        StringComparison.OrdinalIgnoreCase)),

            Unresolved =
                rows.Count(
                    x => string.Equals(
                        x.ExpectedStatus,
                        "Unresolved",
                        StringComparison.OrdinalIgnoreCase))
        };
    }

    private static ActualStatusCounts BuildActualStatusCounts(
        IReadOnlyList<ActualResult> rows)
    {
        return new ActualStatusCounts
        {
            Matched =
                rows.Count(
                    x => string.Equals(
                        x.Status,
                        "Matched",
                        StringComparison.OrdinalIgnoreCase)),

            Mismatched =
                rows.Count(
                    x => string.Equals(
                        x.Status,
                        "Mismatched",
                        StringComparison.OrdinalIgnoreCase)),

            Missing =
                rows.Count(
                    x => string.Equals(
                        x.Status,
                        "Missing",
                        StringComparison.OrdinalIgnoreCase)),

            Duplicate =
                rows.Count(
                    x => string.Equals(
                        x.Status,
                        "Duplicate",
                        StringComparison.OrdinalIgnoreCase)),

            Unresolved =
                rows.Count(
                    x => string.Equals(
                        x.Status,
                        "Unresolved",
                        StringComparison.OrdinalIgnoreCase))
        };
    }

    private static void CompareStatusCount(
        string status,
        int expected,
        int actual,
        List<string> failures)
    {
        if (expected == actual)
        {
            return;
        }

        failures.Add(
            $"{status} count mismatch. " +
            $"Expected {expected}, actual {actual}.");
    }

    private static void CompareReasonCodeCounts(
        IReadOnlyList<GroundTruthRow> expectedRows,
        IReadOnlyList<ActualResult> actualResults,
        List<string> failures)
    {
        var expected =
            expectedRows
                .GroupBy(
                    x => x.ExpectedReasonCode,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x.Count(),
                    StringComparer.OrdinalIgnoreCase);

        var actual =
            actualResults
                .GroupBy(
                    x => x.ReasonCode,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x.Count(),
                    StringComparer.OrdinalIgnoreCase);

        var allCodes =
            expected.Keys.Union(
                actual.Keys,
                StringComparer.OrdinalIgnoreCase);

        foreach (var reasonCode in allCodes)
        {
            expected.TryGetValue(
                reasonCode,
                out var expectedCount);

            actual.TryGetValue(
                reasonCode,
                out var actualCount);

            if (expectedCount != actualCount)
            {
                failures.Add(
                    $"Reason code '{reasonCode}' mismatch. " +
                    $"Expected {expectedCount}, actual {actualCount}.");
            }
        }
    }

    private static void CompareExceptionCategoryCounts(
        IReadOnlyList<GroundTruthRow> expectedRows,
        IReadOnlyList<ActualException> actualExceptions,
        List<string> failures)
    {
        var expected =
            expectedRows
                .Where(
                    x => !string.IsNullOrWhiteSpace(
                        x.ExpectedExceptionCategory))
                .GroupBy(
                    x => x.ExpectedExceptionCategory,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x.Count(),
                    StringComparer.OrdinalIgnoreCase);

        var actual =
            actualExceptions
                .GroupBy(
                    x => x.Category,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x.Count(),
                    StringComparer.OrdinalIgnoreCase);

        var allCategories =
            expected.Keys.Union(
                actual.Keys,
                StringComparer.OrdinalIgnoreCase);

        foreach (var category in allCategories)
        {
            expected.TryGetValue(
                category,
                out var expectedCount);

            actual.TryGetValue(
                category,
                out var actualCount);

            if (expectedCount != actualCount)
            {
                failures.Add(
                    $"Exception category '{category}' mismatch. " +
                    $"Expected {expectedCount}, actual {actualCount}.");
            }
        }
    }

    private static decimal CalculateMatchRate(
        int matched,
        int total)
    {
        if (total == 0)
        {
            return 0.00m;
        }

        return decimal.Round(
            matched * 100.00m / total,
            2);
    }

    private async Task<T> GetAsync<T>(
        string url,
        string bearerToken,
        CancellationToken cancellationToken)
    {
        var httpClient =
            _injectedHttpClient ?? new HttpClient();

        try
        {
            using var request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    url);

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    bearerToken);

            using var response =
                await httpClient.SendAsync(
                    request,
                    cancellationToken);

            var content =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized ||
                response.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new GroundTruthAuthenticationException(
                    $"GET {url} failed with " +
                    $"{(int)response.StatusCode} {response.StatusCode} -- " +
                    "this is an authentication failure (an expired, " +
                    "invalid, or insufficiently-privileged token), not a " +
                    "ground-truth data mismatch.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"GET {url} failed with " +
                    $"{(int)response.StatusCode} " +
                    $"{response.StatusCode}: {content}");
            }

            var value =
                JsonSerializer.Deserialize<T>(
                    content,
                    JsonOptions);

            if (value is null)
            {
                throw new InvalidOperationException(
                    $"Unable to deserialize response from {url}.");
            }

            return value;
        }
        finally
        {
            if (_injectedHttpClient is null)
            {
                httpClient.Dispose();
            }
        }
    }

    /// <summary>
    /// Mirrors the wire shape of FinSight.Application.DTOs.Reconciliation
    /// .PagedResponse&lt;T&gt; without taking a project reference on it --
    /// this comparator deliberately keeps its own independent copy of
    /// every response shape it consumes (see ActualResult/ActualException
    /// below), so that a broken shared DTO cannot pass by construction.
    /// </summary>
    public sealed class PagedEnvelope<TItem>
    {
        public List<TItem> Items { get; set; } = new();

        public int PageNumber { get; set; }

        public int PageSize { get; set; }

        public int TotalCount { get; set; }

        public int TotalPages { get; set; }
    }

    private sealed class LoginRequestPayload
    {
        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }

    private sealed class LoginResponsePayload
    {
        public string AccessToken { get; set; } = string.Empty;

        public string TokenType { get; set; } = string.Empty;

        public DateTime ExpiresAtUtc { get; set; }

        public Guid UserId { get; set; }

        public string Email { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;
    }

    public sealed class ActualResult
    {
        public Guid ResultId { get; set; }

        public Guid RunId { get; set; }

        public Guid NormalizedTransactionId { get; set; }

        public string TransactionReference { get; set; } =
            string.Empty;

        public string Status { get; set; } =
            string.Empty;

        public string? StrategyUsed { get; set; }

        public string ReasonCode { get; set; } =
            string.Empty;

        public DateTime CreatedAt { get; set; }
    }

    public sealed class ActualException
    {
        public Guid ExceptionId { get; set; }

        public Guid RunId { get; set; }

        public Guid ReconciliationResultId { get; set; }

        public string Category { get; set; } =
            string.Empty;

        public string TransactionReference { get; set; } =
            string.Empty;
    }

    private sealed class ExpectedStatusCounts
    {
        public int Matched { get; init; }

        public int Mismatched { get; init; }

        public int Missing { get; init; }

        public int Duplicate { get; init; }

        public int Unresolved { get; init; }
    }

    private sealed class ActualStatusCounts
    {
        public int Matched { get; init; }

        public int Mismatched { get; init; }

        public int Missing { get; init; }

        public int Duplicate { get; init; }

        public int Unresolved { get; init; }
    }
}

/// <summary>
/// Thrown when the ground-truth verifier cannot authenticate or its
/// token is rejected (401/403) -- always distinct from a data-mismatch
/// GroundTruthComparisonResult, so an authentication problem is never
/// misread as "ground truth didn't match".
/// </summary>
public sealed class GroundTruthAuthenticationException
    : Exception
{
    public GroundTruthAuthenticationException(
        string message)
        : base(message)
    {
    }

    public GroundTruthAuthenticationException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class GroundTruthComparisonResult
{
    public bool IsSuccess { get; init; }

    public int ExpectedTotalUnits { get; init; }

    public int ActualTotalUnits { get; init; }

    public int ExpectedMatched { get; init; }

    public int ActualMatched { get; init; }

    public int ExpectedMismatched { get; init; }

    public int ActualMismatched { get; init; }

    public int ExpectedMissing { get; init; }

    public int ActualMissing { get; init; }

    public int ExpectedDuplicate { get; init; }

    public int ActualDuplicate { get; init; }

    public int ExpectedUnresolved { get; init; }

    public int ActualUnresolved { get; init; }

    public decimal ExpectedMatchRate { get; init; }

    public decimal ActualMatchRate { get; init; }

    public IReadOnlyList<string> Failures { get; init; } =
        Array.Empty<string>();

    public void Print()
    {
        Console.WriteLine();
        Console.WriteLine(
            "===== GROUND TRUTH COMPARISON =====");

        Console.WriteLine();

        Console.WriteLine(
            $"Total Units : " +
            $"{ActualTotalUnits}/{ExpectedTotalUnits}");

        Console.WriteLine(
            $"Matched     : " +
            $"{ActualMatched}/{ExpectedMatched}");

        Console.WriteLine(
            $"Mismatched  : " +
            $"{ActualMismatched}/{ExpectedMismatched}");

        Console.WriteLine(
            $"Missing     : " +
            $"{ActualMissing}/{ExpectedMissing}");

        Console.WriteLine(
            $"Duplicate   : " +
            $"{ActualDuplicate}/{ExpectedDuplicate}");

        Console.WriteLine(
            $"Unresolved  : " +
            $"{ActualUnresolved}/{ExpectedUnresolved}");

        Console.WriteLine(
            $"Match Rate  : " +
            $"{ActualMatchRate:0.00}% / " +
            $"{ExpectedMatchRate:0.00}%");

        Console.WriteLine();

        if (IsSuccess)
        {
            Console.WriteLine(
                "TRANSACTION-LEVEL GROUND TRUTH: PASS");

            return;
        }

        Console.WriteLine(
            "TRANSACTION-LEVEL GROUND TRUTH: FAIL");

        Console.WriteLine();

        foreach (var failure in Failures)
        {
            Console.WriteLine(
                $" - {failure}");
        }
    }
}