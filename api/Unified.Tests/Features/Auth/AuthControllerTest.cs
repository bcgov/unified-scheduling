using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Auth.Controllers;

namespace Unified.Tests.Features.Auth;

public class AuthControllerTest
{
    private readonly AuthController _controller;

    public AuthControllerTest()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService, TestAuthenticationService>();

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        _controller = new AuthController(NullLogger<AuthController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
    }

    [Fact]
    public async Task Login_Should_Redirect_To_Provided_Uri()
    {
        var result = await _controller.Login("/api");

        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/api", redirectResult.Url);
    }

    [Fact]
    public async Task Token_Should_Return_Ok_With_AccessToken_And_ExpiresAt()
    {
        var result = await _controller.Token();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var payload = okResult.Value!;
        var payloadType = payload.GetType();

        var accessToken = payloadType.GetProperty("access_token")?.GetValue(payload)?.ToString();
        var expiresAt = payloadType.GetProperty("expires_at")?.GetValue(payload)?.ToString();

        Assert.Equal("test-access-token", accessToken);
        Assert.Equal("2099-01-01T00:00:00Z", expiresAt);
    }

    [Fact]
    public async Task Token_Should_Return_Ok_With_Null_Tokens_When_No_AccessToken_Found()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService, NoTokenAuthenticationService>();

        var controller = new AuthController(NullLogger<AuthController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() },
            },
        };

        var result = await controller.Token();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var payload = okResult.Value!;
        var payloadType = payload.GetType();

        var accessToken = payloadType.GetProperty("access_token")?.GetValue(payload);
        var expiresAt = payloadType.GetProperty("expires_at")?.GetValue(payload);

        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode ?? StatusCodes.Status200OK);
        Assert.Null(accessToken);
        Assert.Null(expiresAt);
    }

    private sealed class TestAuthenticationService : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            var tokens = new List<AuthenticationToken>
            {
                new() { Name = "access_token", Value = "test-access-token" },
                new() { Name = "expires_at", Value = "2099-01-01T00:00:00Z" },
            };

            var properties = new AuthenticationProperties();
            properties.StoreTokens(tokens);

            var principal = new ClaimsPrincipal(new ClaimsIdentity("TestAuth"));
            var ticket = new AuthenticationTicket(principal, properties, scheme ?? "Cookies");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties
        ) => Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;
    }

    private sealed class NoTokenAuthenticationService : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            var properties = new AuthenticationProperties();
            var principal = new ClaimsPrincipal(new ClaimsIdentity("TestAuth"));
            var ticket = new AuthenticationTicket(principal, properties, scheme ?? "Cookies");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties
        ) => Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;
    }
}
