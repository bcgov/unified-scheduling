using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Unified.Infrastructure.Helpers;

namespace Unified.Infrastructure;

/// <summary>
/// Expires authentication cookies that earlier releases issued with <c>Path=/</c>, now that they are
/// scoped to the application base path by <see cref="BaseHrefCookieBuilder"/>.
/// </summary>
/// <remarks>
/// TEMPORARY: remove after this release has been deployed to each environment for one release cycle.
/// The browser sends both the legacy (<c>Path=/</c>) and current (base-path) cookies under the same
/// names, so the request cannot tell them apart; a delete at <c>Path=/</c> only removes the legacy ones.
/// Skipped when the application is served at the root, where it would delete the live cookie.
/// </remarks>
public sealed class LegacyAuthCookieCleanupMiddleware(
    RequestDelegate next,
    IOptionsMonitor<CookieAuthenticationOptions> cookieOptions
)
{
    private const string LegacyCookiePath = "/";

    /// <summary>
    /// Sent by the server-side PDF renderer, whose browser holds the auth cookies at <c>Path=/</c> on the
    /// internal frontend URL; deleting them would sign the render out mid-page.
    /// </summary>
    public const string SkipHeaderName = "X-Unified-Pdf-Render";

    public Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Cookies.Count > 0 && !context.Request.Headers.ContainsKey(SkipHeaderName))
        {
            var cookieName = cookieOptions.Get(CookieAuthenticationDefaults.AuthenticationScheme).Cookie.Name;
            if (
                !string.IsNullOrEmpty(cookieName)
                && XForwardedForHelper.ResolveCookiePath(context.Request) != LegacyCookiePath
            )
            {
                foreach (var name in context.Request.Cookies.Keys)
                {
                    if (IsAuthenticationCookie(name, cookieName))
                        context.Response.Cookies.Append(name, string.Empty, CreateLegacyDeleteOptions());
                }
            }
        }

        return next(context);
    }

    /// <summary>Matches the cookie name itself or a chunk named <c>{name}C{digits}</c>.</summary>
    public static bool IsAuthenticationCookie(string candidateName, string cookieName)
    {
        if (candidateName == cookieName)
            return true;

        if (!candidateName.StartsWith(cookieName, StringComparison.Ordinal))
            return false;

        var chunkSuffix = candidateName.AsSpan(cookieName.Length);
        return chunkSuffix.Length > 1
            && chunkSuffix[0] == 'C'
            && chunkSuffix[1..].IndexOfAnyExceptInRange('0', '9') < 0;
    }

    // No Domain: legacy cookies were host-only, and a cookie is only removed when name, domain and path match.
    private static CookieOptions CreateLegacyDeleteOptions() =>
        new()
        {
            Path = LegacyCookiePath,
            Expires = DateTimeOffset.UnixEpoch,
            MaxAge = TimeSpan.Zero,
            Secure = true,
            SameSite = SameSiteMode.None,
            HttpOnly = true,
        };
}

public static class LegacyAuthCookieCleanupApplicationBuilderExtensions
{
    /// <summary>
    /// TEMPORARY: registers <see cref="LegacyAuthCookieCleanupMiddleware"/>. Must run before authentication.
    /// </summary>
    public static IApplicationBuilder UseLegacyAuthCookieCleanup(this IApplicationBuilder app) =>
        app.UseMiddleware<LegacyAuthCookieCleanupMiddleware>();
}
