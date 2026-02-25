using System.Text.Json.Serialization;

namespace Unified.Auth.Models;

public sealed record TokenResponse(
    string? AccessToken,
    string? ExpiresAt
);
