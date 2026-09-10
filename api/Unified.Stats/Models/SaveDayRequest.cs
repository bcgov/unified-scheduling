namespace Unified.Stats.Models;

public sealed record SaveDayRequest
{
    public required DateOnly Date { get; init; }
    public required int LocationId { get; init; }
    /// <summary>
    /// The employee whose records are being saved. Null for location-level entries (GroupId 3)
    /// where data is per-location rather than per-employee.
    /// </summary>
    public Guid? UserId { get; init; }
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

    /// <summary>
    /// Optional per-record location override. When set, the record is stored with this
    /// LocationId instead of the request-level LocationId (used when work was performed
    /// outside the user's home location).
    /// </summary>
    public int? LocationId { get; init; }
}
