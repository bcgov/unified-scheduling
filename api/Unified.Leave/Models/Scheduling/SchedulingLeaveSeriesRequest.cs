using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.Leave.Models.Scheduling;

/// <summary>
/// Leave series create request built directly on the common
/// <see cref="CalendarEventSeriesRequest"/> contract so it can be built through
/// <see cref="Unified.Calendar.Services.CalendarEventSeriesService{TRequest,TEntity}"/>.
/// </summary>
public sealed record SchedulingLeaveSeriesRequest : CalendarEventSeriesRequest
{
    public int LeaveTypeId { get; init; }
}
