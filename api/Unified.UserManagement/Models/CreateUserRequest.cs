using Unified.Db.Models.UserManagement;

namespace Unified.UserManagement.Models;

public sealed record CreateUserRequest(
    string IdirName,
    Guid? IdirId,
    bool IsEnabled,
    string FirstName,
    string LastName,
    string Email,
    Gender Gender,
    string Rank,
    string BadgeNumber,
    int? HomeLocationId
);
