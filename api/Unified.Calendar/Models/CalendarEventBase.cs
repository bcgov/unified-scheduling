namespace Unified.Common.Calendar.Scheduling.Models;

public record CalendarEventBase
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Notes { get; init; }
    public string? Color { get; init; } = string.Empty;
    public DateTimeOffset StartAtUtc { get; init; }
    public DateTimeOffset? EndAtUtc { get; init; }
    public string? TimeZoneId { get; init; }
    public bool AllDay { get; init; }
    public int LocationId { get; init; }
    public IReadOnlyCollection<Guid> UserIds { get; init; } = [];
}
