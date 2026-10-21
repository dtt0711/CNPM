using TourismHcm.Contracts.Auth;

namespace TourismHcm.Api.Common;

public static class AuthCookieWriter
{
    public const string AccessTokenCookie = "access_token";
    public const string RefreshTokenCookie = "refresh_token";

    public static void SetAuthCookies(HttpResponse response, AuthResponse auth, bool isDevelopment)
    {
        var accessOptions = BuildCookieOptions(auth.AccessTokenExpiresAt, isDevelopment);
        var refreshOptions = BuildCookieOptions(auth.RefreshTokenExpiresAt, isDevelopment);

        response.Cookies.Append(AccessTokenCookie, auth.AccessToken, accessOptions);
        response.Cookies.Append(RefreshTokenCookie, auth.RefreshToken, refreshOptions);
    }

    public static void ClearAuthCookies(HttpResponse response, bool isDevelopment)
    {
        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = !isDevelopment,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(-1)
        };

        response.Cookies.Delete(AccessTokenCookie, options);
        response.Cookies.Delete(RefreshTokenCookie, options);
    }

    private static CookieOptions BuildCookieOptions(DateTimeOffset expiresAt, bool isDevelopment) => new()
    {
        HttpOnly = true,
        Secure = !isDevelopment,
        SameSite = SameSiteMode.Lax,
        Expires = expiresAt
    };
}
