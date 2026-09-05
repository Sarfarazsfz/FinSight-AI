namespace FinSight.Application.DTOs.Auth;

/// <summary>
/// Public signup input. Deliberately carries no Role, no PasswordHash and
/// no identifier: a public caller must not be able to choose its own
/// privileges or supply pre-hashed credentials. The role is assigned by
/// the server. Admin accounts are created only by the offline
/// `create-user` provisioning command.
/// </summary>
public sealed class RegisterRequest
{
    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string ConfirmPassword { get; init; } = string.Empty;
}
