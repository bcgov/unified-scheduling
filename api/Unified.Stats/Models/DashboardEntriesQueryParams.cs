namespace Unified.Stats.Models;

public sealed record DashboardEntriesQueryParams
{
    public int? LocationId { get; init; }
    public Guid? EmployeeId { get; init; }
    public int? CategoryId { get; init; }
    public int? SubCategoryId { get; init; }
    public string? Status { get; init; }
    public string? Search { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
}
