namespace Unified.Stats.Models;

public sealed record DashboardEntriesQueryParams
{
    public Guid? EmployeeId { get; init; }
    public int? GroupId { get; init; }
    public string? CategoryName { get; init; }
    public int? SubCategoryId { get; init; }
    public string? Status { get; init; }
    public string? NameSearch { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public int? LocationId { get; init; }
}
