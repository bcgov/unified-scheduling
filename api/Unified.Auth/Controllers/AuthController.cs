using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

    public AuthController(ILogger<AuthController> logger)
    {
        _logger = logger;
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
    /// Get a new authentication token.
    /// </summary>
    /// <returns>A token response with accessToken and expiresAt.</returns>
    [HttpGet("token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TokenResponse>> Token()
    {
        var accessToken = await HttpContext.GetTokenAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            "access_token"
        );
        var expiresAt = await HttpContext.GetTokenAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            "expires_at"
        );
        return Ok(new TokenResponse(accessToken, expiresAt));
    }
}
