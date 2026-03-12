using System.Text.Json.Serialization;

namespace Unified.UserManagement.Models;

public sealed record TokenResponse(string? AccessToken, string? ExpiresAt);
