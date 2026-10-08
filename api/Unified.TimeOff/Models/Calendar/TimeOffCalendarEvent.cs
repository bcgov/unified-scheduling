namespace Unified.TimeOff.Models.Calendar;

public sealed record TimeOffCalendarEvent
{
    public required string Id { get; init; }
    public int TimeOffEntryId { get; init; }
    public int? TimeOffSeriesId { get; init; }
    public int LeaveTypeId { get; init; }
    public string? LeaveTypeName { get; init; }
    public int EventId { get; init; }
    public IReadOnlyCollection<Guid> UserIds { get; init; } = [];
    public required string Type { get; init; }
    public required string SourceModule { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? Notes { get; init; }
    public string? Color { get; init; }
    public required DateTimeOffset Start { get; init; }
    public DateTimeOffset? End { get; init; }
    public DateTimeOffset? SeriesStartAtUtc { get; init; }
    public DateTimeOffset? SeriesEndAtUtc { get; init; }
    public string? TimeZoneId { get; init; }
    public bool AllDay { get; init; }
    public bool IsException { get; init; }
    public required string EventTypeCode { get; init; }
    public required string StatusTypeCode { get; init; }
    public DateTimeOffset? CancelledAt { get; init; }
    public Guid? CancelledByUserId { get; init; }
    public string? CancellationReason { get; init; }
    public int? LocationId { get; init; }
}
