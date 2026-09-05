using FinSight.Domain.Entities;

namespace FinSight.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Used by password reset, which resolves the account from the token's
    /// UserId rather than from a caller-supplied email.
    /// </summary>
    Task<User?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        User user,
        CancellationToken cancellationToken = default);
}
