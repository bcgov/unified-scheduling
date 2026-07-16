using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.Db.Models.UserManagement;
using Unified.UserManagement.Controllers;
using Unified.UserManagement.Models;

namespace Unified.Tests.UserManagement.Controllers;

public class AuthControllerTest
{
    private static readonly RecordingAuthenticationService AuthenticationService = new();

    private static AuthController CreateController(
        ClaimsPrincipal? user = null,
        RecordingUserAccountResolutionService? userAccountResolutionService = null
    )
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(AuthenticationService)
            .BuildServiceProvider();
        var controller = new AuthController(
            NullLogger<AuthController>.Instance,
            configuration,
            userAccountResolutionService ?? new RecordingUserAccountResolutionService()
        )
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services },
                RouteData = new RouteData(),
                ActionDescriptor = new ControllerActionDescriptor(),
            },
        };
        controller.Url = new UrlHelper(controller.ControllerContext);

        if (user is not null)
        {
            controller.ControllerContext.HttpContext.User = user;
        }

        return controller;
    }

    [Theory]
    [InlineData("/", "/")]
    [InlineData("/api", "/api")]
    [InlineData("/api?tab=one", "/api?tab=one")]
    [InlineData("~/api", "/api")]
    public async Task Login_When_RedirectUri_Is_Local_ChallengesWithLocalRedirect(
        string redirectUri,
        string expectedRedirectUri
    )
    {
        AuthenticationService.Reset();
        var controller = CreateController();

        var result = await controller.Login(redirectUri);

        var challengeResult = Assert.IsType<ChallengeResult>(result);
        Assert.Contains(OpenIdConnectDefaults.AuthenticationScheme, challengeResult.AuthenticationSchemes);
        Assert.Equal(expectedRedirectUri, challengeResult.Properties?.RedirectUri);
        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, AuthenticationService.SignedOutScheme);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("//example.com")]
    [InlineData("/\\example.com")]
    [InlineData("~//example.com")]
    [InlineData("~/\\example.com")]
    [InlineData("/api\r\nLocation: https://example.com")]
    public async Task Login_When_RedirectUri_Is_Not_Local_Returns_BadRequest(string redirectUri)
    {
        AuthenticationService.Reset();
        var controller = CreateController();

        var result = await controller.Login(redirectUri);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Null(AuthenticationService.SignedOutScheme);
    }

    [Fact]
    public void Logout_Should_Return_SignOut_With_Cookie_And_OIDC_Schemes()
    {
        var controller = CreateController();

        var result = controller.Logout();

        var signOutResult = Assert.IsType<SignOutResult>(result);
        Assert.Contains(CookieAuthenticationDefaults.AuthenticationScheme, signOutResult.AuthenticationSchemes);
        Assert.Contains(OpenIdConnectDefaults.AuthenticationScheme, signOutResult.AuthenticationSchemes);
        Assert.Equal("/", signOutResult.Properties?.RedirectUri);
    }

    [Fact]
    public async Task GetUserInfo_Should_Return_Authenticated_User_Details()
    {
        var userId = Guid.NewGuid();
        var userAccountResolutionService = new RecordingUserAccountResolutionService();
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "test-user"),
            new(ClaimTypes.Email, "test-user@example.com"),
            new("idir_username", "testidir"),
            new(ClaimTypes.Role, "Scheduler"),
            new(UnifiedClaimTypes.UserId, userId.ToString()),
            new(UnifiedClaimTypes.Permission, Permissions.StatsRecordsEnterForOthers.ToString()),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var controller = CreateController(principal, userAccountResolutionService);

        var result = await controller.GetUserInfo(TestContext.Current.CancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var userInfo = Assert.IsType<UserInfo>(okResult.Value);

        Assert.True(userInfo.IsAuthenticated);
        Assert.True(userInfo.IsRegistered);
        Assert.Equal("test-user", userInfo.Name);
        Assert.Equal("TestAuth", userInfo.AuthenticationType);
        Assert.Equal(6, userInfo.Claims.Count);
        Assert.Contains(userInfo.Claims, c => c.Type == ClaimTypes.Email && c.Value == "test-user@example.com");
        Assert.Contains(userInfo.Claims, c => c.Type == "idir_username" && c.Value == "testidir");
        Assert.Equal(userId, userInfo.UserId);
        Assert.Contains(Permissions.StatsRecordsEnterForOthers, userInfo.Permissions);
        Assert.Equal(1, userAccountResolutionService.UpdateCurrentUserLastLoginCallCount);
        Assert.Same(principal, userAccountResolutionService.LastPrincipal);
        Assert.Equal(TestContext.Current.CancellationToken, userAccountResolutionService.LastCancellationToken);
    }

    [Fact]
    public async Task GetUserInfo_Should_Return_Null_UserId_When_Claim_Missing()
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, "test-user") };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var controller = CreateController(principal);

        var result = await controller.GetUserInfo(TestContext.Current.CancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var userInfo = Assert.IsType<UserInfo>(okResult.Value);

        Assert.True(userInfo.IsAuthenticated);
        Assert.False(userInfo.IsRegistered);
        Assert.Null(userInfo.UserId);
        Assert.Empty(userInfo.Permissions);
    }

    [Fact]
    public async Task GetUserInfo_Should_Return_Unauthenticated_User_When_No_Identity()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        var controller = CreateController(principal);

        var result = await controller.GetUserInfo(TestContext.Current.CancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var userInfo = Assert.IsType<UserInfo>(okResult.Value);

        Assert.False(userInfo.IsAuthenticated);
        Assert.False(userInfo.IsRegistered);
        Assert.Null(userInfo.UserId);
    }

    private sealed class RecordingAuthenticationService : IAuthenticationService
    {
        public string? SignedOutScheme { get; private set; }

        public void Reset()
        {
            SignedOutScheme = null;
        }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties
        )
        {
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignedOutScheme = scheme;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingUserAccountResolutionService : IUserAccountResolutionService
    {
        public int UpdateCurrentUserLastLoginCallCount { get; private set; }

        public ClaimsPrincipal? LastPrincipal { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public Task UpdateCurrentUserLastLoginAsync(
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default
        )
        {
            UpdateCurrentUserLastLoginCallCount++;
            LastPrincipal = principal;
            LastCancellationToken = cancellationToken;

            return Task.CompletedTask;
        }

        public Task<User?> ResolveCurrentUserAsync(
            ClaimsPrincipal principal,
            bool recordLogin = false,
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult<User?>(null);
        }
    }
}
