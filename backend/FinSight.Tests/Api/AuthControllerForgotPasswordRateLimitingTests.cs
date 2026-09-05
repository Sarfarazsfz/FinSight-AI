using FinSight.Api.Controllers;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Auth;
using FinSight.Infrastructure.Authentication;
using FinSight.Tests.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Tests.Api;

/// <summary>
/// AuthController's forgot-password rate limiting, in isolation from HTTP.
/// The account service is a spy rather than the real UserAccountService --
/// the existing anti-enumeration guarantee for the account service itself
/// is UserAccountServiceTests' job (unaffected by this file, since the
/// limiter sits entirely in the controller, above that service). What
/// matters here is that the controller applies the SAME limiter to every
/// request regardless of what the account service would eventually say
/// about the email, and that an allowed request still reaches the account
/// service unchanged.
/// </summary>
[TestFixture]
public sealed class AuthControllerForgotPasswordRateLimitingTests
{
    private static AuthController CreateController(
        SpyUserAccountService accountService,
        ManualTimeProvider timeProvider,
        string clientIp = "203.0.113.1",
        int maxAttemptsPerEmail = 3,
        int maxAttemptsPerIp = 100)
    {
        var rateLimiter =
            new InMemoryPasswordResetRateLimiter(
                new PasswordResetRateLimitOptions
                {
                    MaxAttemptsPerEmail = maxAttemptsPerEmail,
                    EmailWindow = TimeSpan.FromMinutes(15),
                    MaxAttemptsPerIp = maxAttemptsPerIp,
                    IpWindow = TimeSpan.FromMinutes(15),
                },
                timeProvider);

        var controller =
            new AuthController(
                new ThrowingAuthService(),
                accountService,
                rateLimiter);

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(clientIp);

        controller.ControllerContext =
            new ControllerContext { HttpContext = httpContext };

        return controller;
    }

    private static ForgotPasswordRequest Request(string email) =>
        new() { Email = email };

    [Test]
    public async Task ForgotPassword_WithinTheLimit_ReturnsOkAndCallsTheAccountServiceEachTime()
    {
        var accountService = new SpyUserAccountService();
        var controller = CreateController(accountService, new ManualTimeProvider(), maxAttemptsPerEmail: 3);

        for (var i = 0; i < 3; i++)
        {
            var result = await controller.ForgotPassword(Request("person@example.com"), CancellationToken.None);

            Assert.That(result, Is.InstanceOf<OkObjectResult>(), $"attempt {i + 1} of 3");
        }

        Assert.That(accountService.Calls, Is.EqualTo(3));
    }

    [Test]
    public async Task ForgotPassword_ImmediatelyBeyondTheLimit_Returns429WithoutCallingTheAccountService()
    {
        var accountService = new SpyUserAccountService();
        var controller = CreateController(accountService, new ManualTimeProvider(), maxAttemptsPerEmail: 3);

        for (var i = 0; i < 3; i++)
        {
            await controller.ForgotPassword(Request("person@example.com"), CancellationToken.None);
        }

        var fourth = await controller.ForgotPassword(Request("person@example.com"), CancellationToken.None);

        var objectResult = fourth as ObjectResult;

        Assert.That(objectResult, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status429TooManyRequests));

            // No additional token, no additional email -- the account
            // service (the only path to either) was never called again.
            Assert.That(accountService.Calls, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task ForgotPassword_WhenRateLimited_UsesProblemDetailsWithAGenericMessage()
    {
        var accountService = new SpyUserAccountService();
        var controller = CreateController(accountService, new ManualTimeProvider(), maxAttemptsPerEmail: 1);

        await controller.ForgotPassword(Request("person@example.com"), CancellationToken.None);
        var blocked = await controller.ForgotPassword(Request("person@example.com"), CancellationToken.None);

        var objectResult = blocked as ObjectResult;
        var problem = objectResult!.Value as ProblemDetails;

        Assert.That(problem, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(problem!.Status, Is.EqualTo(StatusCodes.Status429TooManyRequests));

            Assert.That(
                problem.Detail,
                Is.EqualTo("Too many password reset requests. Please try again later."));

            // Never says whether the account exists.
            Assert.That(problem.Detail, Does.Not.Contain("account exists"));
            Assert.That(problem.Detail, Does.Not.Contain("registered"));
        });
    }

    [Test]
    public async Task ForgotPassword_WhenRateLimited_SetsARetryAfterHeader()
    {
        var accountService = new SpyUserAccountService();
        var controller = CreateController(accountService, new ManualTimeProvider(), maxAttemptsPerEmail: 1);

        await controller.ForgotPassword(Request("person@example.com"), CancellationToken.None);
        await controller.ForgotPassword(Request("person@example.com"), CancellationToken.None);

        var retryAfter = controller.Response.Headers.RetryAfter.ToString();

        Assert.That(retryAfter, Is.Not.Null.And.Not.Empty);
        Assert.That(int.Parse(retryAfter), Is.GreaterThan(0));
    }

    [Test]
    public async Task ForgotPassword_AfterTheWindowElapses_AllowsRequestsAgain()
    {
        var accountService = new SpyUserAccountService();
        var time = new ManualTimeProvider();
        var controller = CreateController(accountService, time, maxAttemptsPerEmail: 1);

        await controller.ForgotPassword(Request("person@example.com"), CancellationToken.None);

        var blocked = await controller.ForgotPassword(Request("person@example.com"), CancellationToken.None);
        Assert.That((blocked as ObjectResult)!.StatusCode, Is.EqualTo(StatusCodes.Status429TooManyRequests));

        time.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));

        var afterWindow = await controller.ForgotPassword(Request("person@example.com"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(afterWindow, Is.InstanceOf<OkObjectResult>());
            Assert.That(accountService.Calls, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task ForgotPassword_CaseVariantsOfTheSameEmail_ShareOneRateLimitIdentity()
    {
        var accountService = new SpyUserAccountService();
        var controller = CreateController(accountService, new ManualTimeProvider(), maxAttemptsPerEmail: 3);

        await controller.ForgotPassword(Request("Person@Example.com"), CancellationToken.None);
        await controller.ForgotPassword(Request("PERSON@EXAMPLE.COM"), CancellationToken.None);
        await controller.ForgotPassword(Request("person@example.com"), CancellationToken.None);

        var fourth = await controller.ForgotPassword(Request("pErSoN@eXaMpLe.CoM"), CancellationToken.None);

        Assert.That(
            (fourth as ObjectResult)!.StatusCode,
            Is.EqualTo(StatusCodes.Status429TooManyRequests),
            "case variants of the same address must share one budget");
    }

    [Test]
    public async Task ForgotPassword_WhitespacePaddedVariantsOfTheSameEmail_ShareOneRateLimitIdentity()
    {
        var accountService = new SpyUserAccountService();
        var controller = CreateController(accountService, new ManualTimeProvider(), maxAttemptsPerEmail: 2);

        await controller.ForgotPassword(Request("  person@example.com"), CancellationToken.None);
        await controller.ForgotPassword(Request("person@example.com  "), CancellationToken.None);

        var third = await controller.ForgotPassword(Request(" person@example.com "), CancellationToken.None);

        Assert.That(
            (third as ObjectResult)!.StatusCode,
            Is.EqualTo(StatusCodes.Status429TooManyRequests),
            "leading/trailing whitespace variants must share one budget");
    }

    [Test]
    public async Task ForgotPassword_DifferentEmails_HaveIndependentBudgets()
    {
        var accountService = new SpyUserAccountService();
        var controller = CreateController(accountService, new ManualTimeProvider(), maxAttemptsPerEmail: 1);

        await controller.ForgotPassword(Request("first@example.com"), CancellationToken.None);
        var blockedFirst = await controller.ForgotPassword(Request("first@example.com"), CancellationToken.None);

        var secondEmailStillAllowed =
            await controller.ForgotPassword(Request("second@example.com"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That((blockedFirst as ObjectResult)!.StatusCode, Is.EqualTo(StatusCodes.Status429TooManyRequests));
            Assert.That(secondEmailStillAllowed, Is.InstanceOf<OkObjectResult>());
        });
    }

    [Test]
    public async Task ForgotPassword_RateLimitsAnUnknownEmailTheSameWayAsAKnownOne()
    {
        // The spy answers with the exact same generic message the real
        // UserAccountService returns regardless of whether the account
        // exists -- proving the limiter above it does not need to (and
        // does not) know or care either, for either address.
        var accountService = new SpyUserAccountService();
        var controller = CreateController(accountService, new ManualTimeProvider(), maxAttemptsPerEmail: 1);

        await controller.ForgotPassword(Request("nobody-real@example.com"), CancellationToken.None);
        var blocked = await controller.ForgotPassword(Request("nobody-real@example.com"), CancellationToken.None);

        Assert.That((blocked as ObjectResult)!.StatusCode, Is.EqualTo(StatusCodes.Status429TooManyRequests));
    }

    [Test]
    public async Task ForgotPassword_ForAnAllowedRequest_PassesTheOriginalRequestThroughUnmodified()
    {
        var accountService = new SpyUserAccountService();
        var controller = CreateController(accountService, new ManualTimeProvider());

        var request = Request("Person@Example.com");
        await controller.ForgotPassword(request, CancellationToken.None);

        Assert.That(
            accountService.LastRequest!.Email,
            Is.EqualTo("Person@Example.com"),
            "the limiter's own normalization must not leak into what the account service receives");
    }

    [Test]
    public async Task ForgotPassword_DifferentClientIps_AreIsolatedFromEachOthersIpBudget()
    {
        var accountService = new SpyUserAccountService();
        var time = new ManualTimeProvider();

        // One rate limiter shared by two controller instances, each
        // observing a different client IP -- exactly what two real
        // requests from two different callers would look like.
        var sharedRateLimiter =
            new InMemoryPasswordResetRateLimiter(
                new PasswordResetRateLimitOptions { MaxAttemptsPerEmail = 100, MaxAttemptsPerIp = 1 },
                time);

        var controllerOne = BuildWithLimiter(accountService, sharedRateLimiter, "203.0.113.10");
        var controllerTwo = BuildWithLimiter(accountService, sharedRateLimiter, "203.0.113.20");

        await controllerOne.ForgotPassword(Request("a@example.com"), CancellationToken.None);
        var blockedOnIpOne = await controllerOne.ForgotPassword(Request("b@example.com"), CancellationToken.None);

        var fromIpTwo = await controllerTwo.ForgotPassword(Request("c@example.com"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(
                (blockedOnIpOne as ObjectResult)!.StatusCode,
                Is.EqualTo(StatusCodes.Status429TooManyRequests),
                "the first IP's own budget of 1 is exhausted");

            Assert.That(
                fromIpTwo,
                Is.InstanceOf<OkObjectResult>(),
                "a different IP must not inherit the first IP's exhausted budget");
        });
    }

    private static AuthController BuildWithLimiter(
        SpyUserAccountService accountService,
        InMemoryPasswordResetRateLimiter rateLimiter,
        string clientIp)
    {
        var controller =
            new AuthController(new ThrowingAuthService(), accountService, rateLimiter);

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(clientIp);

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return controller;
    }

    // ------------------------------------------------------------------ fakes

    private sealed class ThrowingAuthService : IAuthService
    {
        public Task<LoginResponse> LoginAsync(
            LoginRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "Forgot-password rate-limit tests never exercise login.");
    }

    private sealed class SpyUserAccountService : IUserAccountService
    {
        public int Calls { get; private set; }

        public ForgotPasswordRequest? LastRequest { get; private set; }

        public Task<RegisterResult> RegisterAsync(
            RegisterRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "Forgot-password rate-limit tests never exercise registration.");

        public Task<AccountOperationResult> RequestPasswordResetAsync(
            ForgotPasswordRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequest = request;

            // The same generic message the real service returns for
            // every address, known or not.
            return Task.FromResult(
                AccountOperationResult.Ok(
                    "If an account exists for that email, we sent password reset instructions."));
        }

        public Task<AccountOperationResult> ResetPasswordAsync(
            ResetPasswordRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "Forgot-password rate-limit tests never exercise reset-password.");
    }
}
