namespace Unified.TimeOff.Models.Scheduling;

public sealed record SchedulingTimeOffEntryResponse : BaseSchedulingEventResponse
{
    public int Id { get; init; }

    public int? TimeOffSeriesId { get; init; }

    public int LeaveTypeId { get; init; }

    public string? SeriesRecurrenceRule { get; init; }

    public string? SeriesTitle { get; init; }

    public DateTimeOffset? SeriesStartAtUtc { get; init; }

    public DateTimeOffset? SeriesEndAtUtc { get; init; }

    public string? SeriesTimeZoneId { get; init; }

    public IReadOnlyCollection<Guid> SeriesUserIds { get; init; } = [];

    public IReadOnlyCollection<Guid> AssignedUserIds { get; init; } = [];
}
