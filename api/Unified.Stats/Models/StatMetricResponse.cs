namespace Unified.Stats.Models;

public sealed record StatMetricResponse
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string UnitOfMeasure { get; init; } = string.Empty;
}
