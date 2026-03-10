using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Unified.Auth.Models;

namespace Unified.Auth.Controllers;

/// <summary>
/// Auth controller for handling authentication and token management
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly IConfiguration _configuration;

    public AuthController(ILogger<AuthController> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Login with credentials and get authentication token
    /// </summary>
    /// <param name="redirectUri">The URI to redirect to after successful authentication.</param>
    /// <returns>A redirect response.</returns>
    [Authorize(AuthenticationSchemes = OpenIdConnectDefaults.AuthenticationScheme)]
    [HttpGet("login")]
    public async Task<IActionResult> Login(string redirectUri = "/api")
    {
        return Redirect(redirectUri);
    }

    /// <summary>
    /// Gets the current authenticated user's information
    /// </summary>
    [HttpGet("user")]
    [Authorize]
    public ActionResult<UserInfo> GetUserInfo()
    {
        var user = new UserInfo(
            User.Identity?.IsAuthenticated ?? false,
            User.Identity?.Name,
            User.Identity?.AuthenticationType,
            User.Claims.Select(c => new UserClaim(c.Type, c.Value)).ToList()
        );

        return Ok(user);
    }
}
