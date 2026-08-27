using FinSight.Application.DTOs.Auth;

namespace FinSight.Application.Abstractions.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);
}
