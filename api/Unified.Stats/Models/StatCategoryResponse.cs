namespace Unified.Stats.Models;

public sealed record StatCategoryResponse
{
    public int Id { get; init; }
    public int GroupId { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsArchived { get; init; }
    public bool IsHighSecurity { get; init; }
    public int DisplayOrder { get; init; }
}
