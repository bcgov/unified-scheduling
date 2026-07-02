using Unified.Db.Models.UserManagement;

namespace Unified.UserManagement.Models;

public sealed record UserResponse
{
    public required Guid Id { get; init; }
    public required string IdirName { get; init; }
    public Guid? IdirId { get; init; }
    public required bool IsEnabled { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public required Gender Gender { get; init; }
    public string? Rank { get; init; }
    public string? BadgeNumber { get; init; }
    public int? HomeLocationId { get; init; }
    public DateTimeOffset? LastLogin { get; init; }
    public string? PhotoUrl { get; init; }
    public DateTimeOffset? LastPhotoUpdate { get; init; }
}
