namespace FinSight.Application.DTOs.Auth;

public sealed class ResetPasswordRequest
{
    public string Token { get; init; } = string.Empty;

    public string NewPassword { get; init; } = string.Empty;

    public string ConfirmPassword { get; init; } = string.Empty;
}
