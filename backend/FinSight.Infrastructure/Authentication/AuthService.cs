using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Auth;

namespace FinSight.Infrastructure.Authentication;

public sealed class AuthService
    : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordService _passwordService;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(
        IUserRepository userRepository,
        IPasswordService passwordService,
        IJwtTokenService jwtTokenService)
    {
        _userRepository =
            userRepository;

        _passwordService =
            passwordService;

        _jwtTokenService =
            jwtTokenService;
    }

    public async Task<LoginResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(
                nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ArgumentException(
                "Email is required.",
                nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException(
                "Password is required.",
                nameof(request));
        }

        var normalizedEmail =
            request.Email
                .Trim()
                .ToLowerInvariant();

        var user =
            await _userRepository.GetByEmailAsync(
                normalizedEmail,
                cancellationToken);

        // Do not reveal whether the email exists.
        if (user is null ||
            !user.IsActive ||
            !_passwordService.VerifyPassword(
                request.Password,
                user.PasswordHash))
        {
            throw new UnauthorizedAccessException(
                "Invalid email or password.");
        }

        return _jwtTokenService.GenerateToken(
            user);
    }
}
