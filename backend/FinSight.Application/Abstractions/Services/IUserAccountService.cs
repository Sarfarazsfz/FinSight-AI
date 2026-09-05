using FinSight.Application.DTOs.Auth;

namespace FinSight.Application.Abstractions.Services;

public enum AccountOperationStatus
{
    Success = 0,
    InvalidEmail,
    InvalidPassword,
    PasswordMismatch,
    DuplicateEmail,
    InvalidOrExpiredToken,
}

public sealed record AccountOperationResult(
    AccountOperationStatus Status,
    string Message)
{
    public bool IsSuccess =>
        Status == AccountOperationStatus.Success;

    public static AccountOperationResult Ok(string message) =>
        new(AccountOperationStatus.Success, message);

    public static AccountOperationResult Fail(
        AccountOperationStatus status,
        string message) =>
        new(status, message);
}

public sealed record RegisterResult(
    AccountOperationResult Outcome,
    RegisterResponse? Response);

/// <summary>
/// Account lifecycle operations that sit alongside -- never inside --
/// IAuthService. Login is deliberately left untouched in AuthService so
/// that the credential-verification path this product already depends on
/// keeps exactly one implementation and one set of tests.
/// </summary>
public interface IUserAccountService
{
    Task<RegisterResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Always reports the same outcome to the caller regardless of whether
    /// the address exists -- see the implementation for why.
    /// </summary>
    Task<AccountOperationResult> RequestPasswordResetAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<AccountOperationResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default);
}
