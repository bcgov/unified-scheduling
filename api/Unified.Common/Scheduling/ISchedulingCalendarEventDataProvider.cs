using Unified.Common.Scheduling.Models;

namespace Unified.Common.Scheduling;

public interface ISchedulingCalendarEventDataProvider<TEventResponse>
{
    Task<IReadOnlyCollection<TEventResponse>> GetDataAsync(
        SchedulingCalendarEventQueryContext request,
        CancellationToken cancellationToken = default
    );
}
