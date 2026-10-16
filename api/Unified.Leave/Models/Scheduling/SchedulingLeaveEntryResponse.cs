using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.Leave.Models.Scheduling;

/// <summary>
/// Leave entry response built directly on the common <see cref="CalendarEventEntryResponse"/>
/// contract so it mirrors the shape produced through
/// <see cref="Unified.Calendar.Services.CalendarEventEntryService{TRequest,TEntity}"/>.
/// </summary>
public sealed record SchedulingLeaveEntryResponse : CalendarEventEntryResponse
{
    public int? LeaveSeriesId { get; init; }
    public int LeaveTypeId { get; init; }
}
