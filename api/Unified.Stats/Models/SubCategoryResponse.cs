namespace Unified.Stats.Models;

public sealed record SubCategoryResponse
{
    public int Id { get; init; }
    public int CategoryId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
}
