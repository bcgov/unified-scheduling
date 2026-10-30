namespace Unified.Leave.Models.Scheduling;

/// <summary>
/// Leave transport fields. All-day callers supply LeaveDate instead of synthetic timestamps.
/// The Leave module normalizes this contract before using Calendar's required UTC interval.
/// </summary>
public abstract record SchedulingLeaveRequest
{
    public int LeaveTypeId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Notes { get; init; }
    public string? Color { get; init; } = string.Empty;
    public DateTimeOffset? StartAtUtc { get; init; }
    public DateTimeOffset? EndAtUtc { get; init; }
    public DateOnly? LeaveDate { get; init; }
    public string? TimeZoneId { get; init; }
    public bool AllDay { get; init; }
    public int? LocationId { get; init; }
    public IReadOnlyCollection<Guid> UserIds { get; init; } = [];
}
