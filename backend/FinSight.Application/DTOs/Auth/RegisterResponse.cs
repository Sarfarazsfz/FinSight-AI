namespace FinSight.Application.DTOs.Auth;

/// <summary>
/// Confirmation only -- never a token, never a hash. Registration does not
/// authenticate the caller; the client is expected to sign in explicitly
/// afterwards through the normal login endpoint.
/// </summary>
public sealed class RegisterResponse
{
    public Guid UserId { get; init; }

    public string Email { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;
}
