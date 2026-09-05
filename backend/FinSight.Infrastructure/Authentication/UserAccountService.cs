using System.Security.Cryptography;
using System.Text;
using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Auth;
using FinSight.Domain.Entities;

namespace FinSight.Infrastructure.Authentication;

/// <summary>
/// Public account lifecycle: signup, reset request, reset completion.
///
/// AuthService.LoginAsync is intentionally not touched by any of this --
/// credential verification keeps exactly one implementation.
/// </summary>
public sealed class UserAccountService : IUserAccountService
{
    /// <summary>
    /// The role every public signup receives. Public callers can never
    /// choose a role; "Admin" is reachable only through the offline
    /// provisioning command.
    /// </summary>
    public const string DefaultSignupRole = "User";

    private readonly IUserRepository _userRepository;
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IPasswordService _passwordService;
    private readonly IPasswordResetEmailSender _emailSender;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PasswordResetOptions _options;

    public UserAccountService(
        IUserRepository userRepository,
        IPasswordResetTokenRepository tokenRepository,
        IPasswordService passwordService,
        IPasswordResetEmailSender emailSender,
        IUnitOfWork unitOfWork,
        PasswordResetOptions options)
    {
        _userRepository = userRepository;
        _tokenRepository = tokenRepository;
        _passwordService = passwordService;
        _emailSender = emailSender;
        _unitOfWork = unitOfWork;
        _options = options;
    }

    // ---------------------------------------------------------------- signup

    public async Task<RegisterResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = CredentialPolicy.NormalizeEmail(request.Email);

        if (!CredentialPolicy.IsEmailAcceptable(email))
        {
            return new RegisterResult(
                AccountOperationResult.Fail(
                    AccountOperationStatus.InvalidEmail,
                    CredentialPolicy.EmailRequirementMessage),
                null);
        }

        if (!CredentialPolicy.IsPasswordAcceptable(request.Password))
        {
            return new RegisterResult(
                AccountOperationResult.Fail(
                    AccountOperationStatus.InvalidPassword,
                    CredentialPolicy.PasswordRequirementMessage),
                null);
        }

        if (!string.Equals(
                request.Password,
                request.ConfirmPassword,
                StringComparison.Ordinal))
        {
            return new RegisterResult(
                AccountOperationResult.Fail(
                    AccountOperationStatus.PasswordMismatch,
                    CredentialPolicy.PasswordMismatchMessage),
                null);
        }

        var existing =
            await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (existing is not null)
        {
            // Signup, unlike password reset, cannot hide this: the account
            // holder must be told the address is taken or they cannot
            // proceed. The address is one the caller already supplied, and
            // the message deliberately says nothing about the account.
            return new RegisterResult(
                AccountOperationResult.Fail(
                    AccountOperationStatus.DuplicateEmail,
                    "An account with that email already exists."),
                null);
        }

        // Server-assigned. request carries no Role field at all, so this
        // cannot be influenced from outside.
        var user =
            new User(
                email,
                _passwordService.HashPassword(request.Password),
                DefaultSignupRole);

        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RegisterResult(
            AccountOperationResult.Ok("Account created."),
            new RegisterResponse
            {
                UserId = user.Id,
                Email = user.Email,
                Role = user.Role,
            });
    }

    // -------------------------------------------------------- forgot password

    public async Task<AccountOperationResult> RequestPasswordResetAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = CredentialPolicy.NormalizeEmail(request.Email);

        // A malformed address is the one case that is safe to reject
        // outright: it cannot belong to anybody, so saying so leaks
        // nothing about who has an account.
        if (!CredentialPolicy.IsEmailAcceptable(email))
        {
            return AccountOperationResult.Fail(
                AccountOperationStatus.InvalidEmail,
                CredentialPolicy.EmailRequirementMessage);
        }

        var user =
            await _userRepository.GetByEmailAsync(email, cancellationToken);

        // Every path below this point returns the SAME result. An attacker
        // must not be able to tell registered addresses from unregistered
        // ones by the response, so an unknown address does exactly as much
        // observable work as a known one: nothing is sent, nothing is
        // stored, and the caller is told the same thing.
        if (user is not null && user.IsActive)
        {
            var rawToken = GenerateRawToken();
            var expiresAtUtc = DateTime.UtcNow.Add(_options.Lifetime);

            // Only the newest link may work. Any earlier outstanding link
            // is consumed here rather than left redeemable.
            await _tokenRepository.InvalidateActiveTokensForUserAsync(
                user.Id,
                DateTime.UtcNow,
                cancellationToken);

            await _tokenRepository.AddAsync(
                new PasswordResetToken(
                    user.Id,
                    HashToken(rawToken),
                    expiresAtUtc),
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _emailSender.SendAsync(
                user.Email,
                BuildResetUrl(rawToken),
                expiresAtUtc,
                cancellationToken);
        }

        return AccountOperationResult.Ok(
            "If an account exists for that email, we sent password reset instructions.");
    }

    // --------------------------------------------------------- reset password

    public async Task<AccountOperationResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return AccountOperationResult.Fail(
                AccountOperationStatus.InvalidOrExpiredToken,
                "This password reset link is invalid or has expired.");
        }

        if (!CredentialPolicy.IsPasswordAcceptable(request.NewPassword))
        {
            return AccountOperationResult.Fail(
                AccountOperationStatus.InvalidPassword,
                CredentialPolicy.PasswordRequirementMessage);
        }

        if (!string.Equals(
                request.NewPassword,
                request.ConfirmPassword,
                StringComparison.Ordinal))
        {
            return AccountOperationResult.Fail(
                AccountOperationStatus.PasswordMismatch,
                CredentialPolicy.PasswordMismatchMessage);
        }

        var token =
            await _tokenRepository.GetByTokenHashAsync(
                HashToken(request.Token),
                cancellationToken);

        // Unknown, already-used and expired are reported identically:
        // distinguishing them would tell an attacker holding a stale link
        // whether it was ever real.
        if (token is null || !token.IsRedeemable(DateTime.UtcNow))
        {
            return AccountOperationResult.Fail(
                AccountOperationStatus.InvalidOrExpiredToken,
                "This password reset link is invalid or has expired.");
        }

        var user =
            await _userRepository.GetByIdAsync(token.UserId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return AccountOperationResult.Fail(
                AccountOperationStatus.InvalidOrExpiredToken,
                "This password reset link is invalid or has expired.");
        }

        user.ChangePasswordHash(
            _passwordService.HashPassword(request.NewPassword));

        var utcNow = DateTime.UtcNow;

        token.MarkUsed(utcNow);

        // Also burns any sibling token issued before this one, so a second
        // outstanding link cannot change the password again afterwards.
        await _tokenRepository.InvalidateActiveTokensForUserAsync(
            user.Id,
            utcNow,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return AccountOperationResult.Ok(
            "Your password has been updated. You can now sign in.");
    }

    // ------------------------------------------------------------- token bits

    /// <summary>
    /// 256 bits from a cryptographic RNG, URL-safe encoded. Not a GUID,
    /// not a counter, not a JWT -- nothing about it is derivable from
    /// anything an attacker can observe.
    /// </summary>
    private static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);

        // Base64url by hand rather than pulling in
        // Microsoft.AspNetCore.WebUtilities: this is the only place in
        // Infrastructure that needs it, and the encoding is three
        // substitutions.
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// Only this digest is persisted. A plain SHA-256 is correct here (and
    /// deliberately not the password hasher): the input is 256 bits of
    /// uniform randomness, so it is not brute-forceable and needs no work
    /// factor or salt -- while lookup by hash must stay a single indexed
    /// equality match.
    /// </summary>
    private static string HashToken(string rawToken)
    {
        var digest =
            SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));

        return Convert.ToHexStringLower(digest);
    }

    private string BuildResetUrl(string rawToken) =>
        $"{_options.FrontendBaseUrl.TrimEnd('/')}" +
        $"/reset-password?token={Uri.EscapeDataString(rawToken)}";
}
