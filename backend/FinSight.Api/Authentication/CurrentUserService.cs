using System.Security.Claims;
using FinSight.Application.Abstractions.Services;
using Microsoft.AspNetCore.Http;

namespace FinSight.Api.Authentication;

/// <summary>
/// Reads the caller's identity from the JWT claims the bearer-auth
/// middleware already validated -- JwtTokenService puts the user's real
/// Guid Id in NameIdentifier/sub and the email in the Email claim, so no
/// extra database lookup is needed here.
///
/// Deliberately lives in the API project, not Infrastructure:
/// IHttpContextAccessor is part of the ASP.NET Core web host, which only
/// FinSight.Api (Sdk.Web) carries. Infrastructure is consumed from a bare
/// ServiceCollection in two places with no HTTP context at all -- the
/// offline `create-user` command and the AI provider DI tests -- and
/// registering an HttpContext-dependent service from AddInfrastructure
/// would have broken both, the same lesson already learned once with the
/// password-reset email sender.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var value =
                _httpContextAccessor.HttpContext?.User
                    .FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }

    public string? Email =>
        _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.Email);
}
