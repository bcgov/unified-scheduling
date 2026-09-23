namespace Unified.Training.Models;

public sealed record TrainingProfileTypeResponse
{
    public int Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}
