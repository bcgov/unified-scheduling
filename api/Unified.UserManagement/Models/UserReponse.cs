namespace Unified.UserManagement.Models;

public sealed record UserResponse(
    Guid Id,
    string IdirName,
    Guid? IdirId,
    bool IsEnabled,
    string FirstName,
    string LastName,
    string Email,
    string? BadgeNumber,
    int? HomeLocationId,
    DateTimeOffset? LastLogin
);
