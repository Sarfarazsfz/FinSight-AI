using FinSight.Application.Abstractions.Services;

namespace FinSight.Tests.Authorization;

/// <summary>
/// Trivial ICurrentUserService for controller tests that construct a
/// controller directly rather than through the DI-resolved HTTP pipeline
/// -- exactly the same convention already used for IBatchRepository/
/// IReconciliationRunRepository fakes elsewhere in this test project.
/// </summary>
public sealed class FixedCurrentUserService : ICurrentUserService
{
    public FixedCurrentUserService(Guid? userId, string? email = null)
    {
        UserId = userId;
        Email = email;
    }

    public Guid? UserId { get; }

    public string? Email { get; }
}
