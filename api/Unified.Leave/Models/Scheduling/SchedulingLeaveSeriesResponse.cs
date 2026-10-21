using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.Leave.Models.Scheduling;

/// <summary>
/// Leave series response built directly on the common
/// <see cref="CalendarEventSeriesResponse"/> contract so it mirrors the shape
/// produced through <see cref="Unified.Calendar.Services.CalendarEventSeriesService{TRequest,TEntity}"/>.
/// </summary>
public sealed record SchedulingLeaveSeriesResponse : CalendarEventSeriesResponse
{
    public int LeaveTypeId { get; init; }

    public IReadOnlyCollection<int> LeaveEntryIds { get; init; } = [];
}
