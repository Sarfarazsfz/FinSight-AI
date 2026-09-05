using FinSight.Application.Abstractions.Services;

namespace FinSight.Api.Authentication;

/// <summary>
/// One shared null-check, not an authorization framework: every
/// ownership-checking action needs "the caller's id, or 401" as its first
/// step, and repeating that inline nine times across three controllers
/// would invite the checks to drift.
/// </summary>
public static class CurrentUserServiceExtensions
{
    public static bool TryGetCurrentUserId(
        this ICurrentUserService currentUserService,
        out Guid userId)
    {
        if (currentUserService.UserId is { } id)
        {
            userId = id;
            return true;
        }

        userId = Guid.Empty;
        return false;
    }
}
