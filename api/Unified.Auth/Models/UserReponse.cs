namespace Unified.Auth.Models;

public sealed record UserResponse(
    Guid Id,
    string IdirName,
    Guid? IdirId,
    bool IsEnabled,
    string FirstName,
    string LastName,
    string Email,
    int? HomeLocationId,
    DateTimeOffset? LastLogin
);
