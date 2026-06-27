using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Controllers;

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
    /// Log out the current user by clearing the authentication cookie and signing out of the identity provider
    /// </summary>
    [HttpGet("logout")]
    public async Task<IActionResult> Logout()
    {
        return SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme
        );
    }

    /// <summary>
    /// Gets the current authenticated user's information including their permissions
    /// </summary>
    [HttpGet("user")]
    [Authorize]
    public ActionResult<UserInfo> GetUserInfo()
    {
        var claims = User.Claims.Select(c => new UserClaim(c.Type, c.Value)).ToList();

        var permissions = claims
            .Where(c => c.Type == UnifiedClaimTypes.Permission)
            .Select(c => Enum.TryParse<Permissions>(c.Value, out var parsed) ? parsed : (Permissions?)null)
            .Where(p => p.HasValue)
            .Select(p => p!.Value)
            .ToList();

        var userIdValue = User.FindFirst(UnifiedClaimTypes.UserId)?.Value;
        var userId = Guid.TryParse(userIdValue, out var parsed) ? parsed : (Guid?)null;

        var homeLocationIdValue = User.FindFirst(UnifiedClaimTypes.HomeLocationId)?.Value;
        var homeLocationId = int.TryParse(homeLocationIdValue, out var parsedLocationId)
            ? parsedLocationId
            : (int?)null;

        var firstName = User.FindFirst(UnifiedClaimTypes.FirstName)?.Value;
        var lastName = User.FindFirst(UnifiedClaimTypes.LastName)?.Value;
        var name = string.Join(" ", new[] { firstName, lastName }.Where(s => !string.IsNullOrWhiteSpace(s)));

        var user = new UserInfo(
            User.Identity?.IsAuthenticated ?? false,
            string.IsNullOrWhiteSpace(name) ? User.Identity?.Name : name,
            User.Identity?.AuthenticationType,
            claims,
            permissions,
            userId,
            homeLocationId
        );

        return Ok(user);
    }
}
