namespace Unified.Auth.Services;

/// <summary>
/// Interface for authentication service
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Authenticate user with credentials
    /// </summary>
    /// <param name="username">Username</param>
    /// <param name="password">Password</param>
    /// <returns>Authentication token</returns>
    Task<string> AuthenticateAsync(string username, string password);

    /// <summary>
    /// Validate and refresh authentication token
    /// </summary>
    /// <param name="refreshToken">Refresh token</param>
    /// <returns>New authentication token</returns>
    Task<string> RefreshTokenAsync(string refreshToken);

    /// <summary>
    /// Validate authentication token
    /// </summary>
    /// <param name="token">Authentication token</param>
    /// <returns>True if token is valid</returns>
    Task<bool> ValidateTokenAsync(string token);
}
