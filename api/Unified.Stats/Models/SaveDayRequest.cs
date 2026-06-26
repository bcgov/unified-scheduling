namespace Unified.Stats.Models;

public sealed record SaveDayRequest
{
    public required DateOnly Date { get; init; }
    public required int LocationId { get; init; }
    public required Guid UserId { get; init; }
    public required string Status { get; init; }
    public required int GroupId { get; init; }
    public required IReadOnlyList<SaveDayRecordItem> Records { get; init; }
}

public sealed record SaveDayRecordItem
{
    /// <summary>Null for new records; set to the existing record ID for updates.</summary>
    public int? Id { get; init; }
    public required int SubCategoryMetricId { get; init; }
    public required decimal Value { get; init; }
    public string? Comment { get; init; }
}
