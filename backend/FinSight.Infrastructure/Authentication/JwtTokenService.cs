using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Auth;
using FinSight.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace FinSight.Infrastructure.Authentication;

public sealed class JwtTokenService
    : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(
        JwtOptions options)
    {
        _options = options;
    }

    public LoginResponse GenerateToken(
        User user)
    {
        if (user is null)
        {
            throw new ArgumentNullException(
                nameof(user));
        }

        if (!user.IsActive)
        {
            throw new InvalidOperationException(
                "Cannot generate a token for an inactive user.");
        }

        if (string.IsNullOrWhiteSpace(
                _options.SecretKey))
        {
            throw new InvalidOperationException(
                "JWT SecretKey is not configured.");
        }

        var now =
            DateTime.UtcNow;

        var expiresAtUtc =
            now.AddMinutes(
                _options.ExpirationMinutes);

        var claims =
            new List<Claim>
            {
                new(
                    JwtRegisteredClaimNames.Sub,
                    user.Id.ToString()),

                new(
                    JwtRegisteredClaimNames.Email,
                    user.Email),

                new(
                    ClaimTypes.NameIdentifier,
                    user.Id.ToString()),

                new(
                    ClaimTypes.Email,
                    user.Email),

                new(
                    ClaimTypes.Role,
                    user.Role)
            };

        var key =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    _options.SecretKey));

        var credentials =
            new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

        var token =
            new JwtSecurityToken(
                issuer:
                    _options.Issuer,

                audience:
                    _options.Audience,

                claims:
                    claims,

                notBefore:
                    now,

                expires:
                    expiresAtUtc,

                signingCredentials:
                    credentials);

        var accessToken =
            new JwtSecurityTokenHandler()
                .WriteToken(token);

        return new LoginResponse
        {
            AccessToken =
                accessToken,

            TokenType =
                "Bearer",

            ExpiresAtUtc =
                expiresAtUtc,

            UserId =
                user.Id,

            Email =
                user.Email,

            Role =
                user.Role
        };
    }
}
