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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Unified.Infrastructure;

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
    }

    private static TestServer CreateServer(HttpMessageHandler? refreshHandler = null)
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddControllers().AddApplicationPart(typeof(AuthenticationPipelineTestController).Assembly);
                services.AddAuthorization(options =>
                    options.AddPolicy("view", policy => policy.RequireClaim("permission", "view"))
                );
                services.AddUnifiedAuthentication(
                    new AuthenticationServiceCollectionExtensionTests.FakeHostEnvironment(),
                    AuthenticationServiceCollectionExtensionTests.CreateConfiguration()
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
                                EndSessionEndpoint = "https://keycloak.example.com/logout",
                            }
                        )
                );
            })
            .Configure(app =>
            {
                app.UseRouting();
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
    [HttpGet("protected")]
    [Authorize]
    public IActionResult Protected() => Ok(new { ok = true });

    [HttpGet("requires-permission")]
    [Authorize(Policy = "view")]
    public IActionResult RequiresPermission() => Ok(new { ok = true });

    [HttpGet("auth/login")]
    public IActionResult Login(string redirectUri) =>
        Challenge(
            new AuthenticationProperties { RedirectUri = redirectUri },
            OpenIdConnectDefaults.AuthenticationScheme
        );

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
