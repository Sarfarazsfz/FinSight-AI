using FinSight.Application.Abstractions.Persistence;
using FinSight.Domain.Entities;
using FinSight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Repositories;

public class PasswordResetTokenRepository
    : IPasswordResetTokenRepository
{
    private readonly AppDbContext _dbContext;

    public PasswordResetTokenRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PasswordResetToken?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PasswordResetTokens
            .FirstOrDefaultAsync(
                x => x.TokenHash == tokenHash,
                cancellationToken);
    }

    public async Task AddAsync(
        PasswordResetToken token,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.PasswordResetTokens.AddAsync(
            token,
            cancellationToken);
    }

    public async Task InvalidateActiveTokensForUserAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        // Marked used rather than deleted, so a replayed older link is
        // still recognisable as consumed rather than simply unknown.
        var active =
            await _dbContext.PasswordResetTokens
                .Where(x =>
                    x.UserId == userId &&
                    x.UsedAtUtc == null)
                .ToListAsync(cancellationToken);

        foreach (var token in active)
        {
            // The predicate above runs as SQL against committed state, so
            // it still returns a row that was marked used earlier in this
            // same unit of work but not yet saved -- notably the token
            // being redeemed right now, which ResetPasswordAsync consumes
            // before asking for its siblings to be burned. Re-marking it
            // would throw, so the tracked in-memory state decides.
            if (token.IsUsed)
            {
                continue;
            }

            token.MarkUsed(utcNow);
        }
    }
}
