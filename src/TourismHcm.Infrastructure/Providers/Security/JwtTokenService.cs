using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Application.Options;
using TourismHcm.Contracts.Auth;
using TourismHcm.Domain.Constants;
using TourismHcm.Domain.Entities;

namespace TourismHcm.Infrastructure.Providers.Security;

/// <summary>
/// Issues and validates JWT access tokens, and generates opaque refresh token values.
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public AuthResponse CreateTokens(
        User user,
        IEnumerable<string> roles,
        IEnumerable<string> permissions,
        string refreshToken,
        DateTimeOffset refreshTokenExpiresAt)
    {
        var roleList = roles.ToList();
        var permissionList = permissions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var accessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenMinutes);
        var accessToken = BuildAccessToken(user, roleList, permissionList, accessTokenExpiresAt);

        return new AuthResponse
        {
            UserId = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Roles = roleList,
            Permissions = permissionList,
            AccessToken = accessToken,
            AccessTokenExpiresAt = accessTokenExpiresAt,
            RefreshToken = refreshToken,
            RefreshTokenExpiresAt = refreshTokenExpiresAt,
        };
    }

    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public ClaimsPrincipal? ValidateAccessToken(string accessToken)
    {
        var handler = new JwtSecurityTokenHandler();
        var parameters = BuildValidationParameters();

        try
        {
            var principal = handler.ValidateToken(accessToken, parameters, out var validatedToken);
            return validatedToken is JwtSecurityToken jwt
                && jwt.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase)
                ? principal
                : null;
        }
        catch (SecurityTokenException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public IEnumerable<Claim> BuildUserClaims(User user, IEnumerable<string> roles, IEnumerable<string> permissions)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(permissions.Select(p => new Claim(JwtClaimTypes.Permission, p)));

        return claims;
    }

    private string BuildAccessToken(
        User user, IEnumerable<string> roles, IEnumerable<string> permissions, DateTimeOffset expiresAt)
    {
        var signingKey = BuildSigningKey();
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: BuildUserClaims(user, roles, permissions),
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private SymmetricSecurityKey BuildSigningKey() => new(Encoding.UTF8.GetBytes(_options.SigningKey));

    private TokenValidationParameters BuildValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = _options.Issuer,
        ValidateAudience = true,
        ValidAudience = _options.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = BuildSigningKey(),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
    };
}
