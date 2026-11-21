using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.Db.Models.UserManagement;
using Unified.Infrastructure;
using Unified.UserManagement.Controllers;

namespace Unified.Tests.Infrastructure;

public class AuthenticationPipelineTests
{
    private const int ConservativeCookieHeaderBudgetBytes = 16 * 1024;

    [Fact]
    public async Task ProtectedApi_UnauthenticatedRequest_Returns401WithoutRedirect()
    {
        using var server = CreateServer();

        var response = await server.CreateClient().GetAsync("/api/protected", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotEqual("keycloak.example.com", response.Headers.Location?.Host);
    }

    [Fact]
    public async Task ProtectedApi_ExpiredCookie_Returns401WithoutRedirect()
    {
        using var server = CreateServer();
        using var client = CreateClientWithCookie(server, DateTimeOffset.UtcNow.AddMinutes(-1));

        var response = await client.GetAsync("/api/protected", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotEqual("keycloak.example.com", response.Headers.Location?.Host);
    }

    [Fact]
    public async Task ProtectedApi_RealCookieHandler_KeepsRepeatedRefreshCookiesWithinHeaderBudget()
    {
        var handler = new PipelineRefreshHandler(_ => PipelineRefreshHandler.Success(expiresIn: 30));
        using var server = CreateServer(handler);
        var cookieHeader = await IssueAuthenticationCookieAsync(server);

        using var firstClient = server.CreateClient();
        firstClient.DefaultRequestHeaders.Add("Cookie", cookieHeader);
        var firstResponse = await firstClient.GetAsync("/api/protected", TestContext.Current.CancellationToken);
        var firstRenewedCookieHeader = GetCombinedCookieHeader(firstResponse);

        using var secondClient = server.CreateClient();
        secondClient.DefaultRequestHeaders.Add("Cookie", firstRenewedCookieHeader);
        var secondResponse = await secondClient.GetAsync("/api/protected", TestContext.Current.CancellationToken);
        var secondRenewedCookieHeader = GetCombinedCookieHeader(secondResponse);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(2, handler.CallCount);
        Assert.InRange(cookieHeader.Length, 1, ConservativeCookieHeaderBudgetBytes);
        Assert.InRange(firstRenewedCookieHeader.Length, 1, ConservativeCookieHeaderBudgetBytes);
        Assert.InRange(secondRenewedCookieHeader.Length, 1, ConservativeCookieHeaderBudgetBytes);
        Assert.InRange(secondRenewedCookieHeader.Length, 1, firstRenewedCookieHeader.Length + 256);
    }

    [Fact]
    public async Task ProtectedApi_ConcurrentRotatedRefresh_RejectsFailedRequestWithoutDeletingSuccessfulCookie()
    {
        var handler = new PipelineRefreshHandler(call =>
            call == 1
                ? PipelineRefreshHandler.Success()
                : new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = JsonContent.Create(new { error = "invalid_grant" }),
                }
        );
        using var server = CreateServer(handler);
        using var firstClient = CreateClientWithCookie(server, DateTimeOffset.UtcNow.AddMinutes(-1), "refresh", "id");
        using var secondClient = CreateClientWithCookie(server, DateTimeOffset.UtcNow.AddMinutes(-1), "refresh", "id");

        var responses = await Task.WhenAll(
            firstClient.GetAsync("/api/protected", TestContext.Current.CancellationToken),
            secondClient.GetAsync("/api/protected", TestContext.Current.CancellationToken)
        );

        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
        var rejectedResponse = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Unauthorized);
        Assert.False(
            rejectedResponse.Headers.TryGetValues("Set-Cookie", out var rejectedCookies)
                && rejectedCookies.Any(cookie => cookie.Contains("UnifiedAuth.Development=;", StringComparison.Ordinal))
        );
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task ProtectedApi_AuthenticatedUserWithoutPermission_Returns403WithoutRedirect()
    {
        using var server = CreateServer();
        using var client = CreateClientWithCookie(server, DateTimeOffset.UtcNow.AddHours(1));

        var response = await client.GetAsync("/api/requires-permission", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task ProtectedApi_AuthenticatedUserWithPermission_ReturnsSuccess()
    {
        using var server = CreateServer();
        using var client = CreateClientWithCookie(
            server,
            DateTimeOffset.UtcNow.AddHours(1),
            new Claim("permission", "view")
        );

        var response = await client.GetAsync("/api/requires-permission", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LoginEndpoint_ExplicitOidcChallenge_RedirectsToIdentityProvider()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();

        var response = await client.GetAsync(
            "/api/auth/login?redirectUri=%2Fdashboard",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal("keycloak.example.com", response.Headers.Location.Host);
        var query = QueryHelpers.ParseQuery(response.Headers.Location.Query);
        Assert.Equal("idir", query["kc_idp_hint"].ToString());
        query.TryGetValue("prompt", out var prompt);
        Assert.NotEqual("none", prompt.ToString());
    }

    [Theory]
    [InlineData("/", true)]
    [InlineData("/unified-scheduling/", true)]
    [InlineData("/unified-scheduling/", false)]
    public async Task Logout_AuthenticatedCookie_ChainsOidcAndSiteMinderBeforeLoggedOutReturn(
        string applicationBase,
        bool behindProxy
    )
    {
        using var server = CreateServer(pathBase: behindProxy ? "" : applicationBase.TrimEnd('/'));
        using var client = CreateClientWithCookie(
            server,
            DateTimeOffset.UtcNow.AddHours(1),
            "refresh-token",
            "retained-id-token"
        );
        client.BaseAddress = new Uri(behindProxy ? "http://internal-api:5000" : "https://scheduling.example.com");
        if (behindProxy)
        {
            client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
            client.DefaultRequestHeaders.Add("X-Forwarded-Host", "scheduling.example.com");
            client.DefaultRequestHeaders.Add("X-Forwarded-Port", "8443");
            client.DefaultRequestHeaders.Add("X-Base-Href", applicationBase);
        }
        var backendBase = behindProxy ? "/" : applicationBase;
        var logoutUri = new Uri(client.BaseAddress, $"{backendBase}api/auth/logout");
        var publicOrigin = behindProxy ? "https://scheduling.example.com:8443" : "https://scheduling.example.com";
        // The browser only sees the public URL; behind the proxy the backend path has no base.
        var publicLogoutUri = new Uri($"{publicOrigin}{applicationBase}api/auth/logout");
        var cookies = new CookieContainer();
        // Cookies are scoped to the public application base path: X-Base-Href behind the proxy,
        // otherwise the request PathBase.
        var cookiePath = applicationBase == "/" ? "/" : applicationBase.TrimEnd('/');
        cookies.SetCookies(
            publicLogoutUri,
            client.DefaultRequestHeaders.GetValues("Cookie").Single() + $"; path={cookiePath}"
        );
        Assert.NotEmpty(cookies.GetCookieHeader(publicLogoutUri));

        var response = await client.GetAsync(logoutUri, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location);
        Assert.Equal("https://keycloak.example.com/logout", location.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("retained-id-token", query["id_token_hint"].ToString());
        Assert.Equal(
            $"{publicOrigin}{applicationBase}api/auth/signout-callback-oidc",
            query["post_logout_redirect_uri"].ToString()
        );
        var options = server
            .Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);
        var state = query["state"].ToString();
        Assert.Equal(
            $"{publicOrigin}{applicationBase}logged-out",
            options.StateDataFormat.Unprotect(state)?.RedirectUri
        );

        var deletedCookies = response.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Contains(
            deletedCookies,
            cookie =>
                cookie.StartsWith("UnifiedAuth.Development=;", StringComparison.Ordinal)
                && GetCookieAttribute(cookie, "path") == cookiePath
        );
        foreach (var cookie in deletedCookies)
            cookies.SetCookies(publicLogoutUri, cookie);
        Assert.Empty(cookies.GetCookieHeader(publicLogoutUri));
        client.DefaultRequestHeaders.Remove("Cookie");

        var callback = QueryHelpers.AddQueryString($"{backendBase}api/auth/signout-callback-oidc", "state", state);
        var callbackResponse = await client.GetAsync(callback, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, callbackResponse.StatusCode);
        var upstreamLocation = Assert.IsType<Uri>(callbackResponse.Headers.Location);
        Assert.Equal(
            "https://siteminder.example.com/clp-cgi/logoff.cgi",
            upstreamLocation.GetLeftPart(UriPartial.Path)
        );
        var upstreamQuery = QueryHelpers.ParseQuery(upstreamLocation.Query);
        Assert.Contains("returl=https%3A%2F%2F", upstreamLocation.OriginalString);
        Assert.Equal("1", upstreamQuery["retnow"].ToString());
        Assert.Equal("1", upstreamQuery["existing"].ToString());
        Assert.Equal($"{publicOrigin}{applicationBase}logged-out", upstreamQuery["returl"].ToString());
    }

    [Theory]
    [InlineData("missing", null)]
    [InlineData("invalid", null)]
    [InlineData("no-redirect", null)]
    [InlineData("valid", "server_error")]
    public async Task LogoutCallback_InvalidStateOrProviderError_DoesNotRedirectAsSuccess(
        string stateKind,
        string? error
    )
    {
        using var server = CreateServer();
        using var client = server.CreateClient();
        var options = server
            .Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);
        var state = stateKind switch
        {
            "missing" => null,
            "invalid" => "tampered-state",
            "no-redirect" => options.StateDataFormat.Protect(new AuthenticationProperties()),
            _ => options.StateDataFormat.Protect(new AuthenticationProperties { RedirectUri = "/" }),
        };
        var callback = QueryHelpers.AddQueryString(
            "/api/auth/signout-callback-oidc",
            new Dictionary<string, string?> { ["state"] = state, ["error"] = error }
        );

        var response = await client.GetAsync(callback, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Contains(
            "identity-provider session may still be active",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/relative-logout")]
    [InlineData("http://siteminder.example.com/clp-cgi/logoff.cgi")]
    public async Task LogoutCallback_MissingOrUnsafeUpstreamUrl_DoesNotReturnToAutomaticLogin(string? logoutUrl)
    {
        using var server = CreateServer(siteMinderLogoutUrl: logoutUrl);
        using var client = CreateClientWithCookie(server, DateTimeOffset.UtcNow.AddHours(1), "refresh", "id");
        var logoutResponse = await client.GetAsync("/api/auth/logout", TestContext.Current.CancellationToken);
        var query = QueryHelpers.ParseQuery(logoutResponse.Headers.Location!.Query);
        var callback = QueryHelpers.AddQueryString(
            "/api/auth/signout-callback-oidc",
            "state",
            query["state"].ToString()
        );

        var response = await client.GetAsync(callback, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Contains(
            "identity-provider session may still be active",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Logout_MissingEndSessionEndpoint_PropagatesFailureInsteadOfRedirectingToRoot()
    {
        using var server = CreateServer(endSessionEndpoint: "");
        using var client = CreateClientWithCookie(server, DateTimeOffset.UtcNow.AddHours(1), "refresh", "id");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetAsync("/api/auth/logout", TestContext.Current.CancellationToken)
        );

        Assert.Contains("end session endpoint", exception.Message);
    }

    [Theory]
    [InlineData("/sheriff-scheduling/", "/sheriff-scheduling")]
    [InlineData(null, "/")]
    [InlineData("/", "/")]
    [InlineData("/bad;domain=evil.example/", "/")]
    public async Task SignInAndLogout_AuthCookieChunks_UsePublicBaseHrefPath(string? baseHref, string expectedPath)
    {
        // The issued ticket's access token is already expired, so logout refreshes it first.
        using var server = CreateServer(new PipelineRefreshHandler());
        using var signInClient = CreateProxiedClient(server, baseHref);

        var signInResponse = await signInClient.GetAsync(
            "/api/auth/test-sign-in",
            TestContext.Current.CancellationToken
        );

        var issuedCookies = GetSetCookies(signInResponse, "UnifiedAuth.Development");
        Assert.True(issuedCookies.Length > 1, "The representative ticket should be chunked.");
        Assert.All(issuedCookies, cookie => Assert.Equal(expectedPath, GetCookieAttribute(cookie, "path")));
        Assert.All(issuedCookies, cookie => Assert.Null(GetCookieAttribute(cookie, "domain")));

        using var logoutClient = CreateProxiedClient(server, baseHref);
        logoutClient.DefaultRequestHeaders.Add("Cookie", GetCombinedCookieHeader(signInResponse));
        var logoutResponse = await logoutClient.GetAsync("/api/auth/logout", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, logoutResponse.StatusCode);
        var deletedCookies = GetSetCookies(logoutResponse, "UnifiedAuth.Development")
            .Where(cookie => GetCookieAttribute(cookie, "path") == expectedPath)
            .Select(cookie => cookie.Split('=', 2)[0])
            .ToArray();
        Assert.Equal(issuedCookies.Select(cookie => cookie.Split('=', 2)[0]).Order(), deletedCookies.Order());
    }

    [Theory]
    [InlineData("/sheriff-scheduling/", "/sheriff-scheduling")]
    [InlineData(null, "/")]
    public async Task LoginChallenge_CorrelationAndNonceCookies_UsePublicBaseHrefPath(
        string? baseHref,
        string expectedPath
    )
    {
        using var server = CreateServer();
        using var client = CreateProxiedClient(server, baseHref);

        var response = await client.GetAsync(
            "/api/auth/login?redirectUri=%2Fdashboard",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        foreach (var prefix in new[] { ".AspNetCore.Correlation.", ".AspNetCore.OpenIdConnect.Nonce." })
        {
            var cookie = Assert.Single(GetSetCookies(response, prefix));
            Assert.Equal(expectedPath, GetCookieAttribute(cookie, "path"));
            Assert.True(
                DateTimeOffset.TryParseExact(
                    GetCookieAttribute(cookie, "expires"),
                    "r",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var expires
                )
                    && expires > DateTimeOffset.UtcNow,
                "The default short-lived expiry should be preserved."
            );
            Assert.Equal("none", GetCookieAttribute(cookie, "samesite"));
            Assert.Contains("; secure", cookie, StringComparison.Ordinal);
            Assert.Contains("; httponly", cookie, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task LegacyCookieCleanup_BehindBasePath_ExpiresRootScopedAuthCookiesOnly()
    {
        using var server = CreateServer(environmentName: Environments.Production);
        using var client = CreateProxiedClient(server, "/sheriff-scheduling/");
        client.DefaultRequestHeaders.Add(
            "Cookie",
            "UnifiedAuth=chunks-2; UnifiedAuthC1=a; UnifiedAuthC2=b; UnifiedAuthX=keep; UnifiedAuth.Development=keep; Other=keep"
        );

        var response = await client.GetAsync("/api/public", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var deletes = response.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Equal(
            new[] { "UnifiedAuth", "UnifiedAuthC1", "UnifiedAuthC2" },
            deletes.Select(cookie => cookie.Split('=', 2)[0]).Order()
        );
        Assert.All(
            deletes,
            cookie =>
            {
                Assert.Contains("=;", cookie, StringComparison.Ordinal);
                Assert.Equal("/", GetCookieAttribute(cookie, "path"));
                Assert.Null(GetCookieAttribute(cookie, "domain"));
                Assert.Equal("0", GetCookieAttribute(cookie, "max-age"));
                Assert.Equal("Thu, 01 Jan 1970 00:00:00 GMT", GetCookieAttribute(cookie, "expires"));
                Assert.Equal("none", GetCookieAttribute(cookie, "samesite"));
                Assert.Contains("; secure", cookie, StringComparison.Ordinal);
                Assert.Contains("; httponly", cookie, StringComparison.Ordinal);
            }
        );

        // A browser holding both generations keeps only the base-path cookies.
        var appUri = new Uri("https://scheduling.example.com/sheriff-scheduling/api/public");
        var jar = new CookieContainer();
        jar.SetCookies(appUri, "UnifiedAuth=legacy; path=/");
        jar.SetCookies(appUri, "UnifiedAuth=current; path=/sheriff-scheduling");
        foreach (var cookie in deletes)
            jar.SetCookies(appUri, cookie);
        Assert.Equal("UnifiedAuth=current", jar.GetCookieHeader(appUri));
    }

    [Theory]
    [InlineData("/sheriff-scheduling/", "Other=keep")]
    [InlineData(null, "UnifiedAuth=root; UnifiedAuthC1=root")]
    [InlineData("/", "UnifiedAuth=root; UnifiedAuthC1=root")]
    public async Task LegacyCookieCleanup_WithoutLegacyCookiesOrAtRoot_DoesNotDeleteCookies(
        string? baseHref,
        string cookieHeader
    )
    {
        using var server = CreateServer(environmentName: Environments.Production);
        using var client = CreateProxiedClient(server, baseHref);
        client.DefaultRequestHeaders.Add("Cookie", cookieHeader);

        var response = await client.GetAsync("/api/public", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task LegacyCookieCleanup_ExplicitRootBaseHref_IgnoresBasePathLikeRequestPath()
    {
        using var server = CreateServer(environmentName: Environments.Production);
        using var client = CreateProxiedClient(server, "/");
        client.DefaultRequestHeaders.Add("Cookie", "UnifiedAuth=root; UnifiedAuthC1=root");

        var response = await client.GetAsync("/sheriff-scheduling/api/public", TestContext.Current.CancellationToken);

        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task LegacyCookieCleanup_PdfRendererRequest_DoesNotDeleteCookies()
    {
        using var server = CreateServer(environmentName: Environments.Production);
        using var client = CreateProxiedClient(server, "/sheriff-scheduling/");
        client.DefaultRequestHeaders.Add("Cookie", "UnifiedAuth=root; UnifiedAuthC1=root");
        client.DefaultRequestHeaders.Add(LegacyAuthCookieCleanupMiddleware.SkipHeaderName, "1");

        var response = await client.GetAsync("/api/public", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task OidcSignIn_RealisticKeycloakTokens_MeasuresAuthCookieSizeBeforeAndAfterClaimTrim()
    {
        var untrimmed = await MeasureOidcSignInCookieAsync(restoreFullPrincipal: true);
        var trimmed = await MeasureOidcSignInCookieAsync(restoreFullPrincipal: false);

        var output = TestContext.Current.TestOutputHelper;
        foreach (var (label, size) in new[] { ("untrimmed (previous)", untrimmed), ("trimmed (current)", trimmed) })
        {
            output?.WriteLine(
                $"Auth cookie {label}: {size.CookieCount} cookie(s), Set-Cookie {size.SetCookieBytes} bytes, "
                    + $"request Cookie header {size.RequestCookieBytes} bytes, protected value {size.ProtectedValueLength} chars, "
                    + $"serialized ticket {size.SerializedTicketBytes} bytes, {size.ClaimTypes.Length} claims, tokens "
                    + string.Join(", ", size.TokenLengths.Select(token => $"{token.Key} {token.Value}"))
                    + "."
            );
        }

        Assert.All(trimmed.Paths, path => Assert.Equal("/sheriff-scheduling", path));
        Assert.Equal(
            new[]
            {
                ClaimTypes.NameIdentifier,
                UnifiedClaimTypes.IdirId,
                "idir_user_guid",
                "idir_username",
                "name",
            }.Order(),
            trimmed.ClaimTypes.Order()
        );
        Assert.True(untrimmed.ClaimTypes.Length > trimmed.ClaimTypes.Length);
        Assert.True(trimmed.RequestCookieBytes < untrimmed.RequestCookieBytes);
        // Regression guard for these token shapes (measured ~4.6 KB); the previous cookie measured ~7.2 KB.
        Assert.True(trimmed.RequestCookieBytes < 5 * 1024, $"Auth cookie grew to {trimmed.RequestCookieBytes} bytes.");
    }

    private sealed record AuthCookieMeasurement(
        int CookieCount,
        int SetCookieBytes,
        int RequestCookieBytes,
        int ProtectedValueLength,
        int SerializedTicketBytes,
        string[] ClaimTypes,
        Dictionary<string, int> TokenLengths,
        string?[] Paths
    );

    /// <summary>
    /// Runs the real login challenge and OIDC code callback with Keycloak-shaped tokens (BC Gov standard realm,
    /// IDIR: RS256 id_token, HS512 refresh token, userinfo) and measures the issued authentication cookie.
    /// </summary>
    private static async Task<AuthCookieMeasurement> MeasureOidcSignInCookieAsync(bool restoreFullPrincipal)
    {
        const string issuer = "https://loginproxy.gov.bc.ca/auth/realms/standard";
        const string clientId = "sheriff-scheduling-4491";
        const string accessTokenValue = "access-token-used-for-userinfo-only";
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var signingKey = new Microsoft.IdentityModel.Tokens.RsaSecurityKey(rsa)
        {
            KeyId = "ofsy6sA9kULxqk5oU1mTrJ6t5bpOuK9mLc2wNAsq0b4",
        };
        var refreshKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(64)
        )
        {
            KeyId = Guid.NewGuid().ToString(),
        };
        var idirGuid = Guid.NewGuid().ToString("N").ToUpperInvariant();
        var subject = $"{idirGuid.ToLowerInvariant()}@idir";
        var sessionId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var userInfo = new Dictionary<string, object>
        {
            ["sub"] = subject,
            ["idir_user_guid"] = idirGuid,
            ["identity_provider"] = "idir",
            ["idir_username"] = "JSMITHSON",
            ["email_verified"] = false,
            ["name"] = "Smithson, Jonathan PSSG:EX",
            ["preferred_username"] = subject,
            ["display_name"] = "Smithson, Jonathan PSSG:EX",
            ["given_name"] = "Jonathan",
            ["family_name"] = "Smithson",
            ["email"] = "jonathan.smithson@gov.bc.ca",
        };
        var tokenHandler = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler
        {
            SetDefaultTimesOnTokenCreation = false,
        };
        string CreateJwt(
            Dictionary<string, object> claims,
            Microsoft.IdentityModel.Tokens.SecurityKey key,
            string alg
        ) =>
            tokenHandler.CreateToken(
                new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
                {
                    Claims = claims,
                    SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(key, alg),
                }
            );
        string? nonce = null;
        var atHash = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(accessTokenValue))[..16]
        );
        var backchannel = new KeycloakBackchannelHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/token", StringComparison.Ordinal)
                ? JsonContent.Create(
                    new Dictionary<string, object>
                    {
                        ["access_token"] = accessTokenValue,
                        ["expires_in"] = 300,
                        ["refresh_expires_in"] = 1800,
                        ["token_type"] = "Bearer",
                        ["not-before-policy"] = 0,
                        ["session_state"] = sessionId,
                        ["scope"] = "openid idir email profile",
                        ["id_token"] = CreateJwt(
                            new Dictionary<string, object>(userInfo)
                            {
                                ["exp"] = now + 300,
                                ["iat"] = now,
                                ["auth_time"] = now,
                                ["jti"] = Guid.NewGuid().ToString(),
                                ["iss"] = issuer,
                                ["aud"] = clientId,
                                ["typ"] = "ID",
                                ["azp"] = clientId,
                                ["nonce"] = nonce!,
                                ["session_state"] = sessionId,
                                ["at_hash"] = atHash,
                                ["sid"] = sessionId,
                            },
                            signingKey,
                            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256
                        ),
                        ["refresh_token"] = CreateJwt(
                            new Dictionary<string, object>
                            {
                                ["exp"] = now + 1800,
                                ["iat"] = now,
                                ["jti"] = Guid.NewGuid().ToString(),
                                ["iss"] = issuer,
                                ["aud"] = issuer,
                                ["sub"] = subject,
                                ["typ"] = "Refresh",
                                ["azp"] = clientId,
                                ["nonce"] = nonce!,
                                ["session_state"] = sessionId,
                                ["scope"] = "openid idir email profile",
                                ["sid"] = sessionId,
                            },
                            refreshKey,
                            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha512
                        ),
                    }
                )
                : JsonContent.Create(userInfo)
        );

        using var server = CreateServer(
            environmentName: "Production",
            configureServices: services =>
                services.PostConfigure<OpenIdConnectOptions>(
                    OpenIdConnectDefaults.AuthenticationScheme,
                    options =>
                    {
                        options.ClientId = clientId;
                        options.TokenValidationParameters.ValidAudience = clientId;
                        options.Backchannel = new HttpClient(backchannel);
                        var configuration = new OpenIdConnectConfiguration
                        {
                            Issuer = issuer,
                            AuthorizationEndpoint = $"{issuer}/protocol/openid-connect/auth",
                            TokenEndpoint = $"{issuer}/protocol/openid-connect/token",
                            UserInfoEndpoint = $"{issuer}/protocol/openid-connect/userinfo",
                            EndSessionEndpoint = $"{issuer}/protocol/openid-connect/logout",
                        };
                        configuration.SigningKeys.Add(signingKey);
                        options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(
                            configuration
                        );
                        if (restoreFullPrincipal)
                        {
                            // Reproduces the previous cookie: same retained tokens, every Keycloak claim kept.
                            var trimTicket = options.Events.OnTicketReceived;
                            options.Events.OnTicketReceived = async context =>
                            {
                                var fullPrincipal = context.Principal;
                                await trimTicket(context);
                                context.Principal = fullPrincipal;
                            };
                        }
                    }
                )
        );
        using var client = CreateProxiedClient(server, "/sheriff-scheduling/");
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        client.DefaultRequestHeaders.Add("X-Forwarded-Host", "jag.gov.bc.ca");
        client.DefaultRequestHeaders.Add("X-Forwarded-Port", "443");

        var challenge = await client.GetAsync("/api/auth/login?redirectUri=%2F", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        var authorizeQuery = QueryHelpers.ParseQuery(challenge.Headers.Location!.Query);
        nonce = authorizeQuery["nonce"].ToString();
        var callbackRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/signin-oidc")
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["code"] = Guid.NewGuid().ToString(),
                    ["state"] = authorizeQuery["state"].ToString(),
                    ["session_state"] = sessionId,
                }
            ),
        };
        callbackRequest.Headers.Add("Cookie", GetCombinedCookieHeader(challenge));

        var callback = await client.SendAsync(callbackRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        var authCookies = GetSetCookies(callback, "UnifiedAuth")
            .Where(cookie => !cookie.Split(';', 2)[0].EndsWith("=", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(authCookies);
        var pairs = authCookies.Select(cookie => cookie.Split(';', 2)[0]).ToArray();
        var protectedValue =
            pairs.Length == 1
                ? pairs[0].Split('=', 2)[1]
                : string.Concat(pairs.Skip(1).Select(pair => pair.Split('=', 2)[1]));
        var cookieOptions = server
            .Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var ticket = cookieOptions.TicketDataFormat.Unprotect(protectedValue)!;

        return new AuthCookieMeasurement(
            authCookies.Length,
            authCookies.Sum(cookie => cookie.Length),
            string.Join("; ", pairs).Length,
            protectedValue.Length,
            TicketSerializer.Default.Serialize(ticket).Length,
            ticket.Principal.Claims.Select(claim => claim.Type).ToArray(),
            ticket.Properties.GetTokens().ToDictionary(token => token.Name, token => token.Value.Length),
            authCookies.Select(cookie => GetCookieAttribute(cookie, "path")).ToArray()
        );
    }

    private sealed class KeycloakBackchannelHandler(Func<HttpRequestMessage, HttpContent> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = respond(request) });
    }

    private static HttpClient CreateProxiedClient(TestServer server, string? baseHref)
    {
        var client = server.CreateClient();
        if (baseHref is not null)
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Base-Href", baseHref);
        return client;
    }

    private static string[] GetSetCookies(HttpResponseMessage response, string namePrefix) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.Where(cookie => cookie.StartsWith(namePrefix, StringComparison.Ordinal)).ToArray()
            : [];

    private static string? GetCookieAttribute(string setCookie, string attribute) =>
        setCookie
            .Split(';', StringSplitOptions.TrimEntries)
            .Skip(1)
            .Select(part => part.Split('=', 2))
            .Where(pair => string.Equals(pair[0], attribute, StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Length > 1 ? pair[1] : string.Empty)
            .FirstOrDefault();

    private static TestServer CreateServer(
        HttpMessageHandler? refreshHandler = null,
        string pathBase = "",
        string endSessionEndpoint = "https://keycloak.example.com/logout",
        string? siteMinderLogoutUrl = "https://siteminder.example.com/clp-cgi/logoff.cgi?existing=1",
        string environmentName = "Development",
        Action<IServiceCollection>? configureServices = null
    )
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services
                    .AddControllers()
                    .AddApplicationPart(typeof(AuthenticationPipelineTestController).Assembly)
                    .AddApplicationPart(typeof(AuthController).Assembly);
                services.AddSingleton(
                    AuthenticationServiceCollectionExtensionTests.CreateConfiguration(siteMinderLogoutUrl)
                );
                services.AddSingleton<IUserAccountResolutionService>(new TestUserAccountResolutionService());
                services.AddAuthorization(options =>
                    options.AddPolicy("view", policy => policy.RequireClaim("permission", "view"))
                );
                services.AddUnifiedAuthentication(
                    new AuthenticationServiceCollectionExtensionTests.FakeHostEnvironment
                    {
                        EnvironmentName = environmentName,
                    },
                    AuthenticationServiceCollectionExtensionTests.CreateConfiguration(siteMinderLogoutUrl)
                );
                if (refreshHandler is not null)
                {
                    services.AddHttpClient("TokenRefresh").ConfigurePrimaryHttpMessageHandler(() => refreshHandler);
                }
                services.PostConfigure<OpenIdConnectOptions>(
                    OpenIdConnectDefaults.AuthenticationScheme,
                    options =>
                        options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(
                            new OpenIdConnectConfiguration
                            {
                                AuthorizationEndpoint = "https://keycloak.example.com/authorize",
                                TokenEndpoint = "https://keycloak.example.com/token",
                                EndSessionEndpoint = endSessionEndpoint,
                            }
                        )
                );
                configureServices?.Invoke(services);
            })
            .Configure(app =>
            {
                if (!string.IsNullOrEmpty(pathBase))
                    app.UsePathBase(pathBase);
                app.UseRouting();
                app.UseLegacyAuthCookieCleanup();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                });
            });

        return new TestServer(builder);
    }

    private static HttpClient CreateClientWithCookie(
        TestServer server,
        DateTimeOffset expiresAt,
        params Claim[] additionalClaims
    ) => CreateClientWithCookie(server, expiresAt, null, null, additionalClaims);

    private static HttpClient CreateClientWithCookie(
        TestServer server,
        DateTimeOffset expiresAt,
        string? refreshToken,
        string? idToken,
        params Claim[] additionalClaims
    )
    {
        var options = server
            .Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var properties = AuthenticationServiceCollectionExtensionTests.CreateProperties(
            expiresAt.ToString("O"),
            refreshToken,
            idToken
        );
        properties.IssuedUtc = DateTimeOffset.UtcNow.AddHours(-1);
        properties.ExpiresUtc = DateTimeOffset.UtcNow.AddHours(9);
        var ticket = new AuthenticationTicket(
            AuthenticationServiceCollectionExtensionTests.CreatePrincipal(additionalClaims),
            properties,
            CookieAuthenticationDefaults.AuthenticationScheme
        );
        var cookieValue = options.TicketDataFormat.Protect(ticket);
        var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{options.Cookie.Name}={cookieValue}");
        return client;
    }

    private static async Task<string> IssueAuthenticationCookieAsync(TestServer server)
    {
        using var client = server.CreateClient();
        var response = await client.GetAsync("/api/auth/test-sign-in", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return GetCombinedCookieHeader(response);
    }

    private static string GetCombinedCookieHeader(HttpResponseMessage response)
    {
        var cookiePairs = response
            .Headers.GetValues("Set-Cookie")
            .Select(header => header.Split(';', 2)[0])
            .Where(cookie => !cookie.EndsWith("=", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(cookiePairs);
        return string.Join("; ", cookiePairs);
    }

    private sealed class TestUserAccountResolutionService : IUserAccountResolutionService
    {
        public Task UpdateCurrentUserLastLoginAsync(
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public Task<User?> ResolveCurrentUserAsync(
            ClaimsPrincipal principal,
            bool recordLogin = false,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<User?>(null);
    }

    private sealed class PipelineRefreshHandler(Func<int, HttpResponseMessage>? responseFactory = null)
        : HttpMessageHandler
    {
        private int _callCount;
        public int CallCount => _callCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var call = Interlocked.Increment(ref _callCount);
            return Task.FromResult(responseFactory?.Invoke(call) ?? Success());
        }

        public static HttpResponseMessage Success(int expiresIn = 300) =>
            new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new
                    {
                        access_token = "access-is-never-stored",
                        refresh_token = new string('n', 1800),
                        id_token = new string('j', 1800),
                        expires_in = expiresIn,
                    }
                ),
            };
    }
}

[ApiController]
[Route("api")]
public sealed class AuthenticationPipelineTestController : ControllerBase
{
    [HttpGet("public")]
    public IActionResult Public() => Ok(new { ok = true });

    [HttpGet("protected")]
    [Authorize]
    public IActionResult Protected() => Ok(new { ok = true });

    [HttpGet("requires-permission")]
    [Authorize(Policy = "view")]
    public IActionResult RequiresPermission() => Ok(new { ok = true });

    [HttpGet("auth/test-sign-in")]
    public async Task<IActionResult> TestSignIn()
    {
        var properties = AuthenticationServiceCollectionExtensionTests.CreateProperties(
            DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"),
            new string('r', 1800),
            new string('i', 1800)
        );
        properties.IssuedUtc = DateTimeOffset.UtcNow.AddHours(-1);
        properties.ExpiresUtc = DateTimeOffset.UtcNow.AddHours(9);
        var claims = Enumerable
            .Range(1, 24)
            .Select(index => new Claim("permission", $"RepresentativePermission{index:D2}"))
            .ToArray();
        var principal = AuthenticationServiceCollectionExtensionTests.CreatePrincipal(claims);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
        return Ok();
    }
}
