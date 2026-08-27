namespace FinSight.Application.DTOs.Auth;

public sealed class LoginResponse
{
    public string AccessToken { get; init; } =
        string.Empty;

    public string TokenType { get; init; } =
        "Bearer";

    public DateTime ExpiresAtUtc { get; init; }

    public Guid UserId { get; init; }

    public string Email { get; init; } =
        string.Empty;

    public string Role { get; init; } =
        string.Empty;
}
