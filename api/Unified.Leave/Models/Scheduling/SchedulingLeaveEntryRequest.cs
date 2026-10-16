using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.Leave.Models.Scheduling;

/// <summary>
/// Leave entry create request built directly on the common <see cref="CalendarEventEntryRequest"/>
/// contract so it can be built through <see cref="Unified.Calendar.Services.CalendarEventEntryService{TRequest,TEntity}"/>.
/// </summary>
public sealed record SchedulingLeaveEntryRequest : CalendarEventEntryRequest
{
    public int LeaveTypeId { get; init; }
}
