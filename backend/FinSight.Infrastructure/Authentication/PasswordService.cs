using Microsoft.AspNetCore.Identity;

namespace FinSight.Infrastructure.Authentication;

public sealed class PasswordService
    : IPasswordService
{
    private readonly PasswordHasher<object> _hasher =
        new();

    private static readonly object UserContext =
        new();

    public string HashPassword(
        string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException(
                "Password is required.",
                nameof(password));
        }

        return _hasher.HashPassword(
            UserContext,
            password);
    }

    public bool VerifyPassword(
        string password,
        string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        var result =
            _hasher.VerifyHashedPassword(
                UserContext,
                passwordHash,
                password);

        return result ==
               PasswordVerificationResult.Success ||
               result ==
               PasswordVerificationResult.SuccessRehashNeeded;
    }
}
