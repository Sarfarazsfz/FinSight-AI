using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinSight.Application.Evaluation;

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

        // Shared, already-tested pure comparison logic -- also used
        // directly by the live HTTP ground-truth-verification endpoint
        // in FinSight.Api (see FinSight.Application.Evaluation).
        return GroundTruthComparer.Compare(
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