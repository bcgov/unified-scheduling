using Unified.Db.Models.UserManagement;

namespace Unified.UserManagement.Models;

public sealed record UserRequestDto
{
    public  string IdirName { get; init; } = String.Empty;
    public  bool IsEnabled { get; init; } = true;
    public  string FirstName { get; init; } = String.Empty;
    public  string LastName { get; init; } = String.Empty;
    public  string Email { get; init; } = String.Empty;
    public  Gender Gender { get; init; }
    public  int HomeLocationId { get; init; }
    public  string Rank { get; init; } = String.Empty;
    public  string BadgeNumber { get; init; } = String.Empty;
}
