namespace Unified.Stats.Models;

public sealed record StatGroupResponse
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
}
