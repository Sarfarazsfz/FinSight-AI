using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Auth;
using FinSight.Domain.Entities;
using FinSight.Infrastructure.Authentication;

namespace FinSight.Tests.Authentication;

/// <summary>
/// Signup / forgot-password / reset-password lifecycle.
///
/// Uses the REAL <see cref="PasswordService"/> throughout: the property
/// that matters is that a password set by signup or by reset is one the
/// production login path can actually verify, which a fake hasher could
/// never demonstrate. Persistence and email delivery are faked, so the
/// fixture needs no database and no mail provider.
/// </summary>
[TestFixture]
public sealed class UserAccountServiceTests
{
    private const string ValidPassword = "test-only-password-value";
    private const string ReplacementPassword = "test-only-replacement-value";

    private InMemoryUserRepository _users = null!;
    private InMemoryTokenRepository _tokens = null!;
    private CapturingEmailSender _email = null!;
    private UserAccountService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _users = new InMemoryUserRepository();
        _tokens = new InMemoryTokenRepository();
        _email = new CapturingEmailSender();

        _service = new UserAccountService(
            _users,
            _tokens,
            new PasswordService(),
            _email,
            new NoOpUnitOfWork(),
            new PasswordResetOptions());
    }

    private static RegisterRequest Signup(
        string email = "person@example.com",
        string? password = null,
        string? confirm = null) =>
        new()
        {
            Email = email,
            Password = password ?? ValidPassword,
            ConfirmPassword = confirm ?? password ?? ValidPassword,
        };

    /// <summary>Runs the real forgot-password flow and returns the raw token from the link.</summary>
    private async Task<string> RequestResetTokenAsync(string email)
    {
        await _service.RequestPasswordResetAsync(
            new ForgotPasswordRequest { Email = email });

        return _email.LastTokenFromUrl!;
    }

    // ============================================================ SIGNUP

    [Test]
    public async Task Register_WithValidInput_CreatesAnActiveUser()
    {
        var result = await _service.RegisterAsync(Signup());

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome.IsSuccess, Is.True, result.Outcome.Message);
            Assert.That(result.Response!.Email, Is.EqualTo("person@example.com"));
            Assert.That(_users.Count, Is.EqualTo(1));
            Assert.That(_users.Single().IsActive, Is.True);
        });
    }

    [Test]
    public async Task Register_WithADuplicateEmail_IsRejectedAndPersistsNothingNew()
    {
        await _service.RegisterAsync(Signup());

        var result = await _service.RegisterAsync(Signup());

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome.Status, Is.EqualTo(AccountOperationStatus.DuplicateEmail));
            Assert.That(result.Response, Is.Null);
            Assert.That(_users.Count, Is.EqualTo(1));
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("not-an-email")]
    [TestCase("has space@example.com")]
    public async Task Register_WithAnInvalidEmail_IsRejected(string email)
    {
        var result = await _service.RegisterAsync(Signup(email));

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome.Status, Is.EqualTo(AccountOperationStatus.InvalidEmail));
            Assert.That(_users.Count, Is.Zero);
        });
    }

    [TestCase("")]
    [TestCase("short")]
    public async Task Register_WithAWeakPassword_IsRejected(string password)
    {
        var result = await _service.RegisterAsync(Signup(password: password));

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome.Status, Is.EqualTo(AccountOperationStatus.InvalidPassword));
            Assert.That(_users.Count, Is.Zero);
        });
    }

    [Test]
    public async Task Register_WhenConfirmationDoesNotMatch_IsRejected()
    {
        var result =
            await _service.RegisterAsync(
                Signup(password: ValidPassword, confirm: ReplacementPassword));

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome.Status, Is.EqualTo(AccountOperationStatus.PasswordMismatch));
            Assert.That(_users.Count, Is.Zero);
        });
    }

    [Test]
    public async Task Register_AlwaysAssignsTheStandardRole_PublicSignupCannotCreateAnAdmin()
    {
        // RegisterRequest has no Role property at all, so there is no
        // input that could carry "Admin" -- this asserts the resulting
        // privilege level rather than the absence of a field.
        var result = await _service.RegisterAsync(Signup());

        Assert.Multiple(() =>
        {
            Assert.That(result.Response!.Role, Is.EqualTo("User"));
            Assert.That(_users.Single().Role, Is.EqualTo("User"));
            Assert.That(_users.Single().Role, Is.Not.EqualTo("Admin"));
        });
    }

    [Test]
    public async Task Register_StoresAVerifiableHashAndNeverThePlaintext()
    {
        await _service.RegisterAsync(Signup());

        var stored = _users.Single();
        var passwords = new PasswordService();

        Assert.Multiple(() =>
        {
            Assert.That(stored.PasswordHash, Is.Not.EqualTo(ValidPassword));
            Assert.That(stored.PasswordHash, Does.Not.Contain(ValidPassword));
            Assert.That(passwords.VerifyPassword(ValidPassword, stored.PasswordHash), Is.True);
        });
    }

    // =================================================== FORGOT PASSWORD

    [Test]
    public async Task RequestPasswordReset_ForAnUnknownEmail_IsIndistinguishableFromAKnownOne()
    {
        await _service.RegisterAsync(Signup("known@example.com"));

        var known =
            await _service.RequestPasswordResetAsync(
                new ForgotPasswordRequest { Email = "known@example.com" });

        var unknown =
            await _service.RequestPasswordResetAsync(
                new ForgotPasswordRequest { Email = "nobody@example.com" });

        Assert.Multiple(() =>
        {
            Assert.That(unknown.Status, Is.EqualTo(known.Status));
            Assert.That(unknown.Message, Is.EqualTo(known.Message));
            Assert.That(known.Message, Does.Contain("If an account exists"));

            // And no mail is generated for the unknown address.
            Assert.That(_email.SentCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task RequestPasswordReset_ForAKnownAccount_IssuesExactlyOneStoredToken()
    {
        await _service.RegisterAsync(Signup());

        await _service.RequestPasswordResetAsync(
            new ForgotPasswordRequest { Email = "person@example.com" });

        Assert.Multiple(() =>
        {
            Assert.That(_tokens.Count, Is.EqualTo(1));
            Assert.That(_email.SentCount, Is.EqualTo(1));
            Assert.That(_email.LastRecipient, Is.EqualTo("person@example.com"));
        });
    }

    [Test]
    public async Task RequestPasswordReset_NeverStoresOrReturnsTheRawToken()
    {
        await _service.RegisterAsync(Signup());

        var result =
            await _service.RequestPasswordResetAsync(
                new ForgotPasswordRequest { Email = "person@example.com" });

        var rawToken = _email.LastTokenFromUrl!;
        var stored = _tokens.Single();

        Assert.Multiple(() =>
        {
            Assert.That(rawToken, Is.Not.Empty);

            // Only a digest is persisted.
            Assert.That(stored.TokenHash, Is.Not.EqualTo(rawToken));
            Assert.That(stored.TokenHash, Does.Not.Contain(rawToken));
            Assert.That(stored.TokenHash.Length, Is.EqualTo(64), "SHA-256 hex digest.");

            // And nothing token-shaped leaks through the API-facing message.
            Assert.That(result.Message, Does.Not.Contain(rawToken));
        });
    }

    [Test]
    public async Task RequestPasswordReset_IssuingANewLinkInvalidatesThePreviousOne()
    {
        await _service.RegisterAsync(Signup());

        var firstToken = await RequestResetTokenAsync("person@example.com");
        var secondToken = await RequestResetTokenAsync("person@example.com");

        var usingOldLink =
            await _service.ResetPasswordAsync(
                new ResetPasswordRequest
                {
                    Token = firstToken,
                    NewPassword = ReplacementPassword,
                    ConfirmPassword = ReplacementPassword,
                });

        Assert.That(
            usingOldLink.Status,
            Is.EqualTo(AccountOperationStatus.InvalidOrExpiredToken),
            "Only the newest reset link may be redeemable.");

        // The newest link still works.
        var usingNewLink =
            await _service.ResetPasswordAsync(
                new ResetPasswordRequest
                {
                    Token = secondToken,
                    NewPassword = ReplacementPassword,
                    ConfirmPassword = ReplacementPassword,
                });

        Assert.That(usingNewLink.IsSuccess, Is.True, usingNewLink.Message);
    }

    [Test]
    public async Task RequestPasswordReset_WithAMalformedEmail_IsRejectedWithoutIssuingAnything()
    {
        var result =
            await _service.RequestPasswordResetAsync(
                new ForgotPasswordRequest { Email = "not-an-email" });

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(AccountOperationStatus.InvalidEmail));
            Assert.That(_tokens.Count, Is.Zero);
            Assert.That(_email.SentCount, Is.Zero);
        });
    }

    // ==================================================== RESET PASSWORD

    [Test]
    public async Task ResetPassword_WithAValidToken_ReplacesThePasswordVerifiably()
    {
        await _service.RegisterAsync(Signup());
        var token = await RequestResetTokenAsync("person@example.com");

        var result =
            await _service.ResetPasswordAsync(
                new ResetPasswordRequest
                {
                    Token = token,
                    NewPassword = ReplacementPassword,
                    ConfirmPassword = ReplacementPassword,
                });

        var passwords = new PasswordService();
        var stored = _users.Single();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True, result.Message);

            // New password works...
            Assert.That(
                passwords.VerifyPassword(ReplacementPassword, stored.PasswordHash),
                Is.True);

            // ...and the old one no longer does.
            Assert.That(
                passwords.VerifyPassword(ValidPassword, stored.PasswordHash),
                Is.False);
        });
    }

    [Test]
    public async Task ResetPassword_TokenIsSingleUse()
    {
        await _service.RegisterAsync(Signup());
        var token = await RequestResetTokenAsync("person@example.com");

        var first =
            await _service.ResetPasswordAsync(
                new ResetPasswordRequest
                {
                    Token = token,
                    NewPassword = ReplacementPassword,
                    ConfirmPassword = ReplacementPassword,
                });

        var replay =
            await _service.ResetPasswordAsync(
                new ResetPasswordRequest
                {
                    Token = token,
                    NewPassword = "test-only-third-value",
                    ConfirmPassword = "test-only-third-value",
                });

        var passwords = new PasswordService();

        Assert.Multiple(() =>
        {
            Assert.That(first.IsSuccess, Is.True);
            Assert.That(replay.Status, Is.EqualTo(AccountOperationStatus.InvalidOrExpiredToken));

            // The replay must not have changed anything.
            Assert.That(
                passwords.VerifyPassword(ReplacementPassword, _users.Single().PasswordHash),
                Is.True);
        });
    }

    [Test]
    public async Task ResetPassword_WithAnExpiredToken_IsRejected()
    {
        await _service.RegisterAsync(Signup());

        // A service whose tokens are already past their lifetime when
        // issued -- expiry is asserted through the real redemption path
        // rather than by mutating stored state.
        var expiring = new UserAccountService(
            _users,
            _tokens,
            new PasswordService(),
            _email,
            new NoOpUnitOfWork(),
            new PasswordResetOptions { Lifetime = TimeSpan.FromSeconds(-1) });

        await expiring.RequestPasswordResetAsync(
            new ForgotPasswordRequest { Email = "person@example.com" });

        var result =
            await expiring.ResetPasswordAsync(
                new ResetPasswordRequest
                {
                    Token = _email.LastTokenFromUrl!,
                    NewPassword = ReplacementPassword,
                    ConfirmPassword = ReplacementPassword,
                });

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(AccountOperationStatus.InvalidOrExpiredToken));

            // Password unchanged.
            Assert.That(
                new PasswordService().VerifyPassword(
                    ValidPassword,
                    _users.Single().PasswordHash),
                Is.True);
        });
    }

    [TestCase("")]
    [TestCase("this-token-does-not-exist")]
    public async Task ResetPassword_WithAnUnknownToken_IsRejectedWithTheSameMessageAsExpired(
        string token)
    {
        var result =
            await _service.ResetPasswordAsync(
                new ResetPasswordRequest
                {
                    Token = token,
                    NewPassword = ReplacementPassword,
                    ConfirmPassword = ReplacementPassword,
                });

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(AccountOperationStatus.InvalidOrExpiredToken));
            Assert.That(result.Message, Does.Contain("invalid or has expired"));
        });
    }

    [Test]
    public async Task ResetPassword_WhenConfirmationDoesNotMatch_IsRejectedAndTokenSurvives()
    {
        await _service.RegisterAsync(Signup());
        var token = await RequestResetTokenAsync("person@example.com");

        var mismatch =
            await _service.ResetPasswordAsync(
                new ResetPasswordRequest
                {
                    Token = token,
                    NewPassword = ReplacementPassword,
                    ConfirmPassword = "test-only-different-value",
                });

        Assert.That(mismatch.Status, Is.EqualTo(AccountOperationStatus.PasswordMismatch));

        // A user typo must not burn the link.
        var retry =
            await _service.ResetPasswordAsync(
                new ResetPasswordRequest
                {
                    Token = token,
                    NewPassword = ReplacementPassword,
                    ConfirmPassword = ReplacementPassword,
                });

        Assert.That(retry.IsSuccess, Is.True, retry.Message);
    }

    [Test]
    public async Task ResetPassword_WithAWeakNewPassword_IsRejected()
    {
        await _service.RegisterAsync(Signup());
        var token = await RequestResetTokenAsync("person@example.com");

        var result =
            await _service.ResetPasswordAsync(
                new ResetPasswordRequest
                {
                    Token = token,
                    NewPassword = "short",
                    ConfirmPassword = "short",
                });

        Assert.That(result.Status, Is.EqualTo(AccountOperationStatus.InvalidPassword));
    }

    [Test]
    public async Task ResetPassword_NeverEchoesTheTokenOrThePasswordInItsMessage()
    {
        await _service.RegisterAsync(Signup());
        var token = await RequestResetTokenAsync("person@example.com");

        var result =
            await _service.ResetPasswordAsync(
                new ResetPasswordRequest
                {
                    Token = token,
                    NewPassword = ReplacementPassword,
                    ConfirmPassword = ReplacementPassword,
                });

        Assert.Multiple(() =>
        {
            Assert.That(result.Message, Does.Not.Contain(token));
            Assert.That(result.Message, Does.Not.Contain(ReplacementPassword));
        });
    }

    // ============================================================= FAKES

    private sealed class InMemoryUserRepository : IUserRepository
    {
        private readonly List<User> _store = [];

        public int Count => _store.Count;

        public User Single() => _store.Single();

        public Task<User?> GetByEmailAsync(
            string email,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                _store.FirstOrDefault(
                    x => string.Equals(x.Email, email, StringComparison.Ordinal)));

        public Task<User?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_store.FirstOrDefault(x => x.Id == id));

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            _store.Add(user);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryTokenRepository : IPasswordResetTokenRepository
    {
        private readonly List<PasswordResetToken> _store = [];

        public int Count => _store.Count;

        public PasswordResetToken Single() => _store.Single();

        public Task<PasswordResetToken?> GetByTokenHashAsync(
            string tokenHash,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                _store.FirstOrDefault(
                    x => string.Equals(x.TokenHash, tokenHash, StringComparison.Ordinal)));

        public Task AddAsync(
            PasswordResetToken token,
            CancellationToken cancellationToken = default)
        {
            _store.Add(token);
            return Task.CompletedTask;
        }

        public Task InvalidateActiveTokensForUserAsync(
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            foreach (var token in _store.Where(x => x.UserId == userId && !x.IsUsed))
            {
                token.MarkUsed(utcNow);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class CapturingEmailSender : IPasswordResetEmailSender
    {
        public int SentCount { get; private set; }

        public string? LastRecipient { get; private set; }

        public string? LastResetUrl { get; private set; }

        /// <summary>The raw token as it appears in the link -- the only place it exists.</summary>
        public string? LastTokenFromUrl =>
            LastResetUrl is null
                ? null
                : Uri.UnescapeDataString(
                    LastResetUrl[(LastResetUrl.IndexOf("token=", StringComparison.Ordinal) + 6)..]);

        public Task SendAsync(
            string recipientEmail,
            string resetUrl,
            DateTime expiresAtUtc,
            CancellationToken cancellationToken = default)
        {
            SentCount++;
            LastRecipient = recipientEmail;
            LastResetUrl = resetUrl;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }
}
