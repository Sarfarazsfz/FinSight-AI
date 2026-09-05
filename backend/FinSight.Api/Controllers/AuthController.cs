using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserAccountService _accountService;
    private readonly IPasswordResetRateLimiter _rateLimiter;

    public AuthController(
        IAuthService authService,
        IUserAccountService accountService,
        IPasswordResetRateLimiter rateLimiter)
    {
        _authService = authService;
        _accountService = accountService;
        _rateLimiter = rateLimiter;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(
        typeof(LoginResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Problem(
                detail: "Request body is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Problem(
                detail: "Email is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return Problem(
                detail: "Password is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        try
        {
            var response =
                await _authService.LoginAsync(
                    request,
                    cancellationToken);

            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            // 401: authentication itself failed (invalid credentials) --
            // distinct from 403, which is reserved for an authenticated
            // caller lacking permission for an action (see
            // GlobalExceptionHandler).
            return Problem(
                detail: "Invalid email or password.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized");
        }
    }

    /// <summary>
    /// Public signup. Routed under the existing flat `api/auth` prefix --
    /// this project uses no API versioning, and introducing `api/v1/...`
    /// for one endpoint would leave the surface inconsistent.
    ///
    /// The request carries no role: every account created here is a
    /// standard user. Admin accounts come only from the offline
    /// `create-user` provisioning command.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(
        typeof(RegisterResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegisterResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Problem(
                detail: "Request body is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        var result =
            await _accountService.RegisterAsync(
                request,
                cancellationToken);

        if (result.Outcome.IsSuccess)
        {
            // 201 with the created identity -- deliberately no token. The
            // client signs in explicitly afterwards through the existing
            // login endpoint, so there is exactly one path that issues a
            // JWT.
            return StatusCode(
                StatusCodes.Status201Created,
                result.Response);
        }

        if (result.Outcome.Status == AccountOperationStatus.DuplicateEmail)
        {
            return Problem(
                detail: result.Outcome.Message,
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict");
        }

        return Problem(
            detail: result.Outcome.Message,
            statusCode: StatusCodes.Status400BadRequest,
            title: "Bad Request");
    }

    /// <summary>
    /// Requests a password reset link.
    ///
    /// Always answers 200 with the same message for any syntactically
    /// valid address, whether or not an account exists. Returning 404 (or
    /// any distinguishable response) for an unknown address would turn
    /// this endpoint into an account-enumeration oracle.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Problem(
                detail: "Request body is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        // Checked before any lookup, and with the exact same
        // normalization the rest of the application uses for "the same
        // email" -- and unconditionally, for every request, whether or
        // not this address turns out to belong to a real account. That
        // is what keeps this endpoint's anti-enumeration guarantee
        // intact: the limiter must never be the thing that tells a known
        // address apart from an unknown one.
        var normalizedEmail = CredentialPolicy.NormalizeEmail(request.Email);
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var decision = _rateLimiter.CheckAndConsume(normalizedEmail, clientIp);

        if (!decision.IsAllowed)
        {
            if (decision.RetryAfter is { } retryAfter)
            {
                Response.Headers.RetryAfter =
                    Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                        .ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            // Deliberately generic: no mention of whether an account
            // exists, no counter values, nothing enumerable.
            return Problem(
                detail: "Too many password reset requests. Please try again later.",
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Too Many Requests");
        }

        var result =
            await _accountService.RequestPasswordResetAsync(
                request,
                cancellationToken);

        if (!result.IsSuccess)
        {
            return Problem(
                detail: result.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        // No token, no user id, nothing that differs between a known and
        // an unknown address.
        return Ok(new { message = result.Message });
    }

    /// <summary>
    /// Completes a password reset. Unknown, expired and already-used
    /// tokens are reported identically so a stale link cannot be probed
    /// for whether it was ever valid.
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Problem(
                detail: "Request body is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        var result =
            await _accountService.ResetPasswordAsync(
                request,
                cancellationToken);

        if (!result.IsSuccess)
        {
            return Problem(
                detail: result.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        return Ok(new { message = result.Message });
    }
}
