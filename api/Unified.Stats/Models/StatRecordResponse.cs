namespace Unified.Stats.Models;

public sealed record StatRecordResponse
{
    public int Id { get; init; }
    public DateOnly DateFrom { get; init; }
    public DateOnly DateTo { get; init; }
    public string PeriodType { get; init; } = string.Empty;
    public int LocationId { get; init; }
    public int SubCategoryMetricId { get; init; }
    public decimal Value { get; init; }
    public string? Comment { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset CreatedOn { get; init; }
    public Guid? CreatedById { get; init; }
    public DateTimeOffset? UpdatedOn { get; init; }
}
