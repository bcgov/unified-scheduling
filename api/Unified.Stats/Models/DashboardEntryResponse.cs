namespace Unified.Stats.Models;

public sealed record DashboardEntryResponse
{
    public int Id { get; init; }
    public Guid UserId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public string? BadgeNumber { get; init; }
    public DateOnly Date { get; init; }
    public string WorkArea { get; init; } = string.Empty;
    public string Subcategory { get; init; } = string.Empty;
    public string Metric { get; init; } = string.Empty;
    public decimal Value { get; init; }
    public string Status { get; init; } = string.Empty;
}
