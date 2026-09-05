using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Services;
using FinSight.Domain.Entities;
using FinSight.Infrastructure.Authentication;

namespace FinSight.Api.Provisioning;

/// <summary>
/// Outcome of one provisioning attempt. The numeric values double as the
/// process exit code, so a caller (script, CI job, judge following the
/// setup doc) can distinguish "already exists" from "bad role" without
/// parsing console text. 0 is success; 1 is reserved for usage/unexpected
/// errors raised by the command layer.
/// </summary>
public enum UserProvisioningStatus
{
    Created = 0,
    InvalidEmail = 2,
    InvalidRole = 3,
    InvalidPassword = 4,
    DuplicateEmail = 5,
}

public sealed record UserProvisioningResult(
    UserProvisioningStatus Status,
    string Message)
{
    public bool IsSuccess =>
        Status == UserProvisioningStatus.Created;

    public int ExitCode =>
        (int)Status;
}

/// <summary>
/// Creates the first (or an additional) application user for a fresh
/// deployment.
///
/// This exists because FinSight deliberately has no public registration
/// endpoint -- adding one to a financial audit product purely to solve a
/// setup problem would be a real security regression. Provisioning is
/// therefore an offline, operator-run action.
///
/// Everything security-relevant is delegated, never reimplemented: the
/// password is hashed only by <see cref="IPasswordService"/> (the same
/// service <c>AuthService</c> verifies against, so a provisioned account
/// is guaranteed to be loginable), the entity is built by the real
/// <see cref="User"/> constructor so its invariants and email
/// normalization apply, and persistence goes through the existing
/// repository + unit-of-work rather than raw SQL.
///
/// This type performs no console I/O and holds no configuration, which is
/// what makes it directly testable.
/// </summary>
public sealed class UserProvisioningService
{
    /// <summary>
    /// The only two values the database's CHK_User_Role constraint
    /// permits. Matched case-sensitively and after trimming, exactly as
    /// the User entity stores them -- "admin" would pass a naive
    /// case-insensitive check here and then be rejected by PostgreSQL.
    /// </summary>
    private static readonly string[] AllowedRoles = ["Admin", "User"];

    private readonly IUserRepository _userRepository;
    private readonly IPasswordService _passwordService;
    private readonly IUnitOfWork _unitOfWork;

    public UserProvisioningService(
        IUserRepository userRepository,
        IPasswordService passwordService,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
        _unitOfWork = unitOfWork;
    }

    public async Task<UserProvisioningResult> ProvisionAsync(
        string? email,
        string? role,
        string? password,
        CancellationToken cancellationToken = default)
    {
        // Normalization and the email/password rules come from the shared
        // CredentialPolicy, so this offline path and the public signup
        // endpoint cannot drift apart on what a valid credential is.
        var normalizedEmail = CredentialPolicy.NormalizeEmail(email);

        if (!CredentialPolicy.IsEmailAcceptable(normalizedEmail))
        {
            return new UserProvisioningResult(
                UserProvisioningStatus.InvalidEmail,
                "Email must contain '@', must not contain whitespace, " +
                $"and must be {CredentialPolicy.MaximumEmailLength} characters or fewer.");
        }

        var normalizedRole = (role ?? string.Empty).Trim();

        if (!AllowedRoles.Contains(normalizedRole, StringComparer.Ordinal))
        {
            return new UserProvisioningResult(
                UserProvisioningStatus.InvalidRole,
                "Role must be exactly 'Admin' or 'User' (case-sensitive).");
        }

        if (!CredentialPolicy.IsPasswordAcceptable(password))
        {
            return new UserProvisioningResult(
                UserProvisioningStatus.InvalidPassword,
                CredentialPolicy.PasswordRequirementMessage);
        }

        // Checked explicitly so that "this account already exists" is a
        // normal, clearly-worded outcome rather than a raw
        // IX_users_email unique-violation stack trace.
        var existing =
            await _userRepository.GetByEmailAsync(
                normalizedEmail,
                cancellationToken);

        if (existing is not null)
        {
            return new UserProvisioningResult(
                UserProvisioningStatus.DuplicateEmail,
                $"A user with email '{normalizedEmail}' already exists.");
        }

        var passwordHash =
            _passwordService.HashPassword(password);

        var user =
            new User(
                normalizedEmail,
                passwordHash,
                normalizedRole);

        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // The hash is never returned or logged -- only non-sensitive
        // identifiers a operator needs to confirm what was created.
        return new UserProvisioningResult(
            UserProvisioningStatus.Created,
            $"Created user '{normalizedEmail}' with role '{normalizedRole}'.");
    }
}
