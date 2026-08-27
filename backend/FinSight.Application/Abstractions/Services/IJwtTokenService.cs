using FinSight.Application.DTOs.Auth;
using FinSight.Domain.Entities;

namespace FinSight.Application.Abstractions.Services;

public interface IJwtTokenService
{
    LoginResponse GenerateToken(
        User user);
}
