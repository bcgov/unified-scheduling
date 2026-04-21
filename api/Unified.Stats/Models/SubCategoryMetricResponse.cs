namespace Unified.Stats.Models;

public sealed record SubCategoryMetricResponse
{
    public int Id { get; init; }
    public int SubCategoryId { get; init; }
    public int MetricId { get; init; }
    public int DisplayOrder { get; init; }
}
