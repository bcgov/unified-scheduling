namespace Unified.UserManagement.Models;

public sealed record UserLocationResponseDto
{
    public int Id { get; init; }
    public int LocationId { get; init; }
    public DateTimeOffset? EffectiveFrom { get; init; }
    public DateTimeOffset EffectiveUntil { get; init; }
}
