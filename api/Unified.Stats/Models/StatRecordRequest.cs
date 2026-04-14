namespace Unified.Stats.Models;

public sealed record StatRecordRequest
{
    public required DateOnly DateFrom { get; init; }
    public required DateOnly DateTo { get; init; }
    public required string PeriodType { get; init; }
    public required int LocationId { get; init; }
    public required int SubCategoryMetricId { get; init; }
    public required decimal Value { get; init; }
    public string? Comment { get; init; }
}
