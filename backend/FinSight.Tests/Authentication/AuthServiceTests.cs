using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Auth;
using FinSight.Domain.Entities;
using FinSight.Infrastructure.Authentication;

namespace FinSight.Tests.Authentication;

[TestFixture]
public sealed class AuthServiceTests
{
    [Test]
    public async Task LoginAsync_WithValidCredentials_ReturnsToken()
    {
        var user =
            new User(
                "test@example.com",
                "hashed-password",
                "Admin");

        var userRepository =
            new FakeUserRepository(user);

        var passwordService =
            new FakePasswordService(
                passwordMatches: true);

        var expectedResponse =
            new LoginResponse
            {
                AccessToken = "test-token",
                TokenType = "Bearer",
                ExpiresAtUtc =
                    DateTime.UtcNow.AddMinutes(60),
                UserId = user.Id,
                Email = user.Email,
                Role = user.Role
            };

        var tokenService =
            new FakeJwtTokenService(
                expectedResponse);

        var service =
            new AuthService(
                userRepository,
                passwordService,
                tokenService);

        var request =
            new LoginRequest
            {
                Email =
                    " TEST@EXAMPLE.COM ",

                Password =
                    "correct-password"
            };

        var result =
            await service.LoginAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.AccessToken,
                Is.EqualTo("test-token"));

            Assert.That(
                result.Email,
                Is.EqualTo("test@example.com"));

            Assert.That(
                result.Role,
                Is.EqualTo("Admin"));

            Assert.That(
                userRepository.LastRequestedEmail,
                Is.EqualTo("test@example.com"));

            Assert.That(
                passwordService.LastPassword,
                Is.EqualTo("correct-password"));

            Assert.That(
                tokenService.GeneratedForUser,
                Is.SameAs(user));
        });
    }

    [Test]
    public async Task LoginAsync_WithWrongPassword_ThrowsUnauthorizedAccessException()
    {
        var user =
            new User(
                "test@example.com",
                "hashed-password",
                "Admin");

        var service =
            new AuthService(
                new FakeUserRepository(user),
                new FakePasswordService(
                    passwordMatches: false),
                new FakeJwtTokenService(
                    new LoginResponse()));

        var request =
            new LoginRequest
            {
                Email =
                    "test@example.com",

                Password =
                    "wrong-password"
            };

        Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () =>
                await service.LoginAsync(request));
    }

    [Test]
    public void LoginAsync_WithUnknownUser_ThrowsUnauthorizedAccessException()
    {
        var service =
            new AuthService(
                new FakeUserRepository(null),
                new FakePasswordService(
                    passwordMatches: true),
                new FakeJwtTokenService(
                    new LoginResponse()));

        var request =
            new LoginRequest
            {
                Email =
                    "missing@example.com",

                Password =
                    "password"
            };

        Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () =>
                await service.LoginAsync(request));
    }

    [Test]
    public void LoginAsync_WithInactiveUser_ThrowsUnauthorizedAccessException()
    {
        var user =
            new User(
                "inactive@example.com",
                "hashed-password",
                "User");

        user.Deactivate();

        var service =
            new AuthService(
                new FakeUserRepository(user),
                new FakePasswordService(
                    passwordMatches: true),
                new FakeJwtTokenService(
                    new LoginResponse()));

        var request =
            new LoginRequest
            {
                Email =
                    "inactive@example.com",

                Password =
                    "correct-password"
            };

        Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () =>
                await service.LoginAsync(request));
    }

    private sealed class FakeUserRepository
        : IUserRepository
    {
        private readonly User? _user;

        public string? LastRequestedEmail { get; private set; }

        public FakeUserRepository(
            User? user)
        {
            _user = user;
        }

        public Task<User?> GetByEmailAsync(
            string email,
            CancellationToken cancellationToken = default)
        {
            LastRequestedEmail = email;

            return Task.FromResult(_user);
        }

        public Task<User?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _user is not null && _user.Id == id ? _user : null);
        }

        public Task AddAsync(
            User user,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakePasswordService
        : IPasswordService
    {
        private readonly bool _passwordMatches;

        public string? LastPassword { get; private set; }

        public FakePasswordService(
            bool passwordMatches)
        {
            _passwordMatches =
                passwordMatches;
        }

        public string HashPassword(
            string password)
        {
            throw new NotSupportedException();
        }

        public bool VerifyPassword(
            string password,
            string passwordHash)
        {
            LastPassword = password;

            return _passwordMatches;
        }
    }

    private sealed class FakeJwtTokenService
        : IJwtTokenService
    {
        private readonly LoginResponse _response;

        public User? GeneratedForUser { get; private set; }

        public FakeJwtTokenService(
            LoginResponse response)
        {
            _response = response;
        }

        public LoginResponse GenerateToken(
            User user)
        {
            GeneratedForUser = user;

            return _response;
        }
    }
}
