using Unified.Db.Models.UserManagement;

namespace Unified.UserManagement.Models;

public sealed record UserRequestDto
{
    public required string IdirName { get; init; }
    public required bool IsEnabled { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public required Gender Gender { get; init; }
    public required int HomeLocationId { get; init; }
    public required string Rank { get; init; }
    public string BadgeNumber { get; init; } = string.Empty;
}
