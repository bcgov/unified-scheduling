using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.UserManagement.Controllers;
using Unified.UserManagement.Models;

namespace Unified.Tests.UserManagement.Controllers;

public class AuthControllerTest
{
    private static AuthController CreateController(ClaimsPrincipal? user = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var controller = new AuthController(NullLogger<AuthController>.Instance, configuration)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        if (user is not null)
        {
            controller.ControllerContext.HttpContext.User = user;
        }

        return controller;
    }

    [Fact]
    public async Task Login_Should_Redirect_To_Provided_Uri()
    {
        var controller = CreateController();

        var result = await controller.Login("/api");

        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/api", redirectResult.Url);
    }

    [Fact]
    public void GetUserInfo_Should_Return_Authenticated_User_Details()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "test-user"),
            new(ClaimTypes.Email, "test-user@example.com"),
            new(ClaimTypes.Role, "Scheduler"),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var controller = CreateController(principal);

        var result = controller.GetUserInfo();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var userInfo = Assert.IsType<UserInfo>(okResult.Value);

        Assert.True(userInfo.IsAuthenticated);
        Assert.Equal("test-user", userInfo.Name);
        Assert.Equal("TestAuth", userInfo.AuthenticationType);
        Assert.Equal(3, userInfo.Claims.Count);
        Assert.Contains(userInfo.Claims, c => c.Type == ClaimTypes.Email && c.Value == "test-user@example.com");
    }

    [Fact]
    public void GetUserInfo_Should_Return_Unauthenticated_User_When_No_Identity()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        var controller = CreateController(principal);

        var result = controller.GetUserInfo();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var userInfo = Assert.IsType<UserInfo>(okResult.Value);

        Assert.False(userInfo.IsAuthenticated);
        Assert.Null(userInfo.Name);
        Assert.Null(userInfo.AuthenticationType);
        Assert.Empty(userInfo.Claims);
    }
}
