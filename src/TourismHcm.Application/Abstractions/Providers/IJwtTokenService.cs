using System.Security.Claims;
using TourismHcm.Contracts.Auth;
using TourismHcm.Domain.Entities;

namespace TourismHcm.Application.Abstractions.Providers;

public interface IJwtTokenService
{
    /// <summary>
    /// Builds a full <see cref="AuthResponse"/> containing a freshly minted JWT access token
    /// plus the already-generated raw refresh token for the given user and roles.
    /// </summary>
    AuthResponse CreateTokens(
        User user,
        IEnumerable<string> roles,
        IEnumerable<string> permissions,
        string refreshToken,
        DateTimeOffset refreshTokenExpiresAt);

    /// <summary>
    /// Generates a new cryptographically random raw refresh token value.
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Validates an access token and returns its claims principal, or null if invalid/expired.
    /// </summary>
    ClaimsPrincipal? ValidateAccessToken(string accessToken);

    IEnumerable<Claim> BuildUserClaims(User user, IEnumerable<string> roles, IEnumerable<string> permissions);
}
