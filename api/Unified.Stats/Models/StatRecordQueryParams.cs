namespace Unified.Stats.Models;

public sealed record StatRecordQueryParams
{
    public int? LocationId { get; init; }
    public int? SubCategoryMetricId { get; init; }
    public string? PeriodType { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public string? Status { get; init; }
}
