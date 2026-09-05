using FinSight.Api.Provisioning;
using FinSight.Application.Abstractions.Persistence;
using FinSight.Domain.Entities;
using FinSight.Infrastructure.Authentication;

namespace FinSight.Tests.Provisioning;

/// <summary>
/// Covers the offline `create-user` provisioning path.
///
/// These deliberately use the REAL <see cref="PasswordService"/> rather
/// than a fake: the single most important property of provisioning is that
/// the hash it stores is one the production login path can actually
/// verify. A fake hasher would prove nothing about that. Persistence is
/// faked instead, so the whole fixture runs without a database and is not
/// gated behind FINSIGHT_TEST_CONNECTION.
/// </summary>
[TestFixture]
public sealed class UserProvisioningServiceTests
{
    private const string ValidPassword = "test-only-password-value";

    private static UserProvisioningService CreateService(
        InMemoryUserRepository repository)
    {
        return new UserProvisioningService(
            repository,
            new PasswordService(),
            new NoOpUnitOfWork());
    }

    // A. Password hash round-trip -------------------------------------

    [Test]
    public async Task ProvisionAsync_StoresAHashTheRealPasswordServiceCanVerify()
    {
        var repository = new InMemoryUserRepository();
        var service = CreateService(repository);

        var result =
            await service.ProvisionAsync(
                "operator@example.com",
                "User",
                ValidPassword);

        Assert.That(result.IsSuccess, Is.True, result.Message);

        var stored = repository.Single();

        // Verified through the same service AuthService uses -- never by
        // comparing raw hash strings, which would be meaningless for a
        // salted hash.
        var passwordService = new PasswordService();

        Assert.Multiple(() =>
        {
            Assert.That(
                passwordService.VerifyPassword(ValidPassword, stored.PasswordHash),
                Is.True,
                "The provisioned hash must verify against the real password service.");

            Assert.That(
                passwordService.VerifyPassword("not-the-password", stored.PasswordHash),
                Is.False);

            // The plaintext must never be what lands in the column.
            Assert.That(stored.PasswordHash, Is.Not.EqualTo(ValidPassword));
        });
    }

    // B. Duplicate email ----------------------------------------------

    [Test]
    public async Task ProvisionAsync_WithAnExistingEmail_FailsCleanlyWithoutPersisting()
    {
        var repository = new InMemoryUserRepository();
        repository.Seed(
            new User("operator@example.com", "existing-hash", "User"));

        var service = CreateService(repository);

        var result =
            await service.ProvisionAsync(
                "operator@example.com",
                "User",
                ValidPassword);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Status, Is.EqualTo(UserProvisioningStatus.DuplicateEmail));
            Assert.That(result.ExitCode, Is.Not.Zero);
            Assert.That(repository.Count, Is.EqualTo(1), "No second row may be written.");
        });
    }

    [Test]
    public async Task ProvisionAsync_TreatsDifferentlyCasedEmailAsDuplicate()
    {
        var repository = new InMemoryUserRepository();
        repository.Seed(
            new User("operator@example.com", "existing-hash", "User"));

        var service = CreateService(repository);

        // AuthService lowercases before lookup, so provisioning must too --
        // otherwise this would insert a row nobody could ever log in as.
        var result =
            await service.ProvisionAsync(
                "OPERATOR@Example.COM",
                "User",
                ValidPassword);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(UserProvisioningStatus.DuplicateEmail));
            Assert.That(repository.Count, Is.EqualTo(1));
        });
    }

    // C. Invalid role --------------------------------------------------

    [TestCase("Operator")]
    [TestCase("admin")]
    [TestCase("user")]
    [TestCase("")]
    public async Task ProvisionAsync_WithAnUnsupportedRole_IsRejectedAndPersistsNothing(
        string role)
    {
        var repository = new InMemoryUserRepository();
        var service = CreateService(repository);

        var result =
            await service.ProvisionAsync(
                "operator@example.com",
                role,
                ValidPassword);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(UserProvisioningStatus.InvalidRole));
            Assert.That(result.ExitCode, Is.Not.Zero);
            Assert.That(repository.Count, Is.Zero);
        });
    }

    [TestCase("Admin")]
    [TestCase("User")]
    public async Task ProvisionAsync_AcceptsExactlyTheTwoRolesTheDatabaseAllows(string role)
    {
        var repository = new InMemoryUserRepository();
        var service = CreateService(repository);

        var result =
            await service.ProvisionAsync(
                "operator@example.com",
                role,
                ValidPassword);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(repository.Single().Role, Is.EqualTo(role));
        });
    }

    // D. Invalid email / password --------------------------------------

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("no-at-sign")]
    [TestCase("has space@example.com")]
    public async Task ProvisionAsync_WithAnInvalidEmail_IsRejectedBeforePersistence(
        string email)
    {
        var repository = new InMemoryUserRepository();
        var service = CreateService(repository);

        var result =
            await service.ProvisionAsync(email, "User", ValidPassword);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(UserProvisioningStatus.InvalidEmail));
            Assert.That(result.ExitCode, Is.Not.Zero);
            Assert.That(repository.Count, Is.Zero);
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("short")]
    public async Task ProvisionAsync_WithAWeakOrBlankPassword_IsRejectedBeforePersistence(
        string password)
    {
        var repository = new InMemoryUserRepository();
        var service = CreateService(repository);

        var result =
            await service.ProvisionAsync(
                "operator@example.com",
                "User",
                password);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(UserProvisioningStatus.InvalidPassword));
            Assert.That(repository.Count, Is.Zero);
        });
    }

    // E. Successful persistence ----------------------------------------

    [Test]
    public async Task ProvisionAsync_WithValidInput_CreatesExactlyOneActiveNormalizedUser()
    {
        var repository = new InMemoryUserRepository();
        var service = CreateService(repository);

        var result =
            await service.ProvisionAsync(
                "  Operator@Example.com  ",
                "Admin",
                ValidPassword);

        Assert.That(result.IsSuccess, Is.True, result.Message);

        var stored = repository.Single();

        Assert.Multiple(() =>
        {
            Assert.That(repository.Count, Is.EqualTo(1));
            Assert.That(stored.Email, Is.EqualTo("operator@example.com"));
            Assert.That(stored.Role, Is.EqualTo("Admin"));
            Assert.That(stored.IsActive, Is.True);
            Assert.That(stored.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(result.ExitCode, Is.Zero);
        });
    }

    [Test]
    public async Task ProvisionAsync_NeverEchoesThePasswordOrHashInItsMessage()
    {
        var repository = new InMemoryUserRepository();
        var service = CreateService(repository);

        var result =
            await service.ProvisionAsync(
                "operator@example.com",
                "User",
                ValidPassword);

        Assert.Multiple(() =>
        {
            Assert.That(result.Message, Does.Not.Contain(ValidPassword));
            Assert.That(
                result.Message,
                Does.Not.Contain(repository.Single().PasswordHash));
        });
    }

    // Fakes -------------------------------------------------------------

    private sealed class InMemoryUserRepository : IUserRepository
    {
        private readonly List<User> _users = [];

        public int Count => _users.Count;

        public User Single() => _users.Single();

        public void Seed(User user) => _users.Add(user);

        public Task<User?> GetByEmailAsync(
            string email,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _users.FirstOrDefault(
                    x => string.Equals(x.Email, email, StringComparison.Ordinal)));
        }

        public Task<User?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_users.FirstOrDefault(x => x.Id == id));
        }

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            _users.Add(user);
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }
}
