using Unified.Common.Scheduling;
using Unified.Common.Scheduling.Models;
using Unified.LoanOut.Models.Calendar;
using Unified.Scheduling.Models;

namespace Unified.LoanOut.Services;

public interface ILoanOutCalendarDataProvider : ISchedulingCalendarEventDataProvider<SchedulingCalendarEventResponse>
{
    Task<IReadOnlyCollection<LoanOutCalendarUser>> GetUsersAsync(
        LoanOutUserSearchRequest request,
        CancellationToken cancellationToken = default
    );
}
