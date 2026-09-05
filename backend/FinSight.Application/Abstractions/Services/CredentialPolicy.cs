using System.Diagnostics.CodeAnalysis;

namespace FinSight.Application.Abstractions.Services;

/// <summary>
/// The single credential policy for the whole product.
///
/// It lives here so the offline `create-user` provisioning command and the
/// public signup/reset endpoints cannot drift apart: an account created by
/// one path must be subject to the same rules as an account created by the
/// other, otherwise "minimum password length" means whichever number the
/// caller happened to use.
/// </summary>
public static class CredentialPolicy
{
    /// <summary>
    /// Matches the users.email column length.
    /// </summary>
    public const int MaximumEmailLength = 255;

    /// <summary>
    /// Not the security control -- the salted hash is -- but a floor that
    /// stops trivially guessable credentials being created.
    /// </summary>
    public const int MinimumPasswordLength = 8;

    /// <summary>
    /// Normalized exactly as User.cs and AuthService do, so a duplicate
    /// check, a provisioning insert and a login lookup all agree on what
    /// "the same email" means.
    /// </summary>
    public static string NormalizeEmail(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>
    /// Deliberately minimal: enough to catch a mistyped address before it
    /// reaches the database, without pretending to validate deliverability.
    /// </summary>
    public static bool IsEmailAcceptable(string normalizedEmail) =>
        normalizedEmail.Length > 0 &&
        normalizedEmail.Length <= MaximumEmailLength &&
        normalizedEmail.Contains('@') &&
        !normalizedEmail.Any(char.IsWhiteSpace);

    /// <summary>
    /// The NotNullWhen annotation lets callers hash the password directly
    /// after this check without a redundant null test or a null-forgiving
    /// operator.
    /// </summary>
    public static bool IsPasswordAcceptable(
        [NotNullWhen(true)] string? password) =>
        !string.IsNullOrWhiteSpace(password) &&
        password.Length >= MinimumPasswordLength;

    public const string EmailRequirementMessage =
        "Enter a valid email address.";

    public static readonly string PasswordRequirementMessage =
        $"Password must be at least {MinimumPasswordLength} characters.";

    public const string PasswordMismatchMessage =
        "Passwords do not match.";
}
