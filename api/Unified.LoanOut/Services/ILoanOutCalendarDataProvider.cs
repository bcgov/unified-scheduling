using Unified.LoanOut.Models.Calendar;

namespace Unified.LoanOut.Services;

public interface ILoanOutCalendarDataProvider
{
    Task<IReadOnlyCollection<LoanOutCalendarEvent>> GetEventsAsync(
        LoanOutCalendarEventQueryContext queryContext,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyCollection<LoanOutCalendarUser>> GetUsersAsync(
        LoanOutUserSearchRequest request,
        CancellationToken cancellationToken = default
    );
}
