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

    public AuthController(
        IAuthService authService)
    {
        _authService = authService;
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
}
