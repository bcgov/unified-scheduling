using Unified.Db.Models.UserManagement;

namespace Unified.UserManagement.Models;

public sealed record UserResponse(
    Guid Id,
    string IdirName,
    Guid? IdirId,
    bool IsEnabled,
    string FirstName,
    string LastName,
    string Email,
    Gender Gender,
    string? Rank,
    string? BadgeNumber,
    int? HomeLocationId,
    DateTimeOffset? LastLogin
);
