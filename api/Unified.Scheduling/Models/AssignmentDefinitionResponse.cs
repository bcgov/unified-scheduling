namespace Unified.Scheduling.Models;

public sealed record AssignmentDefinitionResponse
{
    public int Id { get; init; }
    public int LocationId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public int SubCategoryId { get; init; }
    public string SubCategoryName { get; init; } = string.Empty;
    public string? Color { get; init; }
    public string? DefaultStartTime { get; init; }
    public string? DefaultEndTime { get; init; }
    public int DefaultCapacity { get; init; }
    public DateTimeOffset EffectiveDateUtc { get; init; }
    public DateTimeOffset? ExpiryDateUtc { get; init; }
}
