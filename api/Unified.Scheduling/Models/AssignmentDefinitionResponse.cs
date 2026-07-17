namespace Unified.Scheduling.Models;

public sealed record AssignmentDefinitionResponse
{
    public int Id { get; init; }
    public int LocationId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int AssignmentCategoryTypeId { get; init; }
    public string AssignmentCategoryTypeDescription { get; init; } = string.Empty;
    public int AssignmentSubCategoryTypeId { get; init; }
    public string AssignmentSubCategoryTypeDescription { get; init; } = string.Empty;
    public string? Color { get; init; }
    public string? DefaultStartTime { get; init; }
    public string? DefaultEndTime { get; init; }
    public int DefaultCapacity { get; init; }
}
