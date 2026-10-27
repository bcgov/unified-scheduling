using Microsoft.EntityFrameworkCore;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.LoanOut.Constants;
using Unified.LoanOut.Mappers;
using Unified.LoanOut.Models;
using Unified.LoanOut.Models.Calendar;

namespace Unified.LoanOut.Services;

public sealed class LoanOutCalendarDataProvider(
    UnifiedDbContext db,
    LoanOutCalendarMapper mapper,
    ITimeZoneService timeZoneService
) : ILoanOutCalendarDataProvider
{
    public async Task<IReadOnlyCollection<LoanOutCalendarEvent>> GetEventsAsync(
        LoanOutCalendarEventQueryContext queryContext,
        CancellationToken cancellationToken = default
    )
    {
        var query = db
            .LoanOutEntries.AsNoTracking()
            .Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .Where(entry => entry.Event != null && entry.Event.SourceModule == LoanOutConstants.SourceModule)
            .Where(entry => entry.Event!.EventTypeCode == LoanOutConstants.LoanOutEventTypeCode)
            .Where(entry => entry.Event!.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled)
            .Where(entry =>
                entry.Event!.StartAtUtc < queryContext.EndAtUtc
                && (entry.Event.EndAtUtc ?? entry.Event.StartAtUtc) > queryContext.StartAtUtc
            );
        if (queryContext.LocationId.HasValue)
            query = query.Where(entry =>
                entry.Event!.LocationId == null || entry.Event.LocationId == queryContext.LocationId.Value
            );
        if (queryContext.UserIds is { Count: > 0 })
            query = query.Where(entry => entry.Users.Any(user => queryContext.UserIds.Contains(user.UserId)));

        var entries = await query.ToListAsync(cancellationToken);

        return entries.Select(mapper.ToLoanOutCalendarEvent).ToList();
    }

    public async Task<IReadOnlyCollection<LoanOutCalendarUser>> GetUsersAsync(
        LoanOutUserSearchRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var dateRange = timeZoneService.ConvertInclusiveLocalDateRangeToUtcRange(
            request.StartDate,
            request.EndDate,
            timeZoneService.ResolveOrUtc(request.TimeZoneId)
        );

        var entries = await db
            .LoanOutEntries.AsNoTracking()
            .Include(entry => entry.Event)
                .ThenInclude(calendarEvent => calendarEvent!.EventSeries)
            .Include(entry => entry.Users)
                .ThenInclude(entryUser => entryUser.User)
            .Where(entry => entry.Event != null && entry.Event.SourceModule == LoanOutConstants.SourceModule)
            .Where(entry => entry.Event!.EventTypeCode == LoanOutConstants.LoanOutEventTypeCode)
            .Where(entry => entry.Event!.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled)
            .Where(entry =>
                entry.Event!.StartAtUtc < dateRange.EndAtUtc
                && (entry.Event.EndAtUtc ?? entry.Event.StartAtUtc) > dateRange.StartAtUtc
            )
            .Where(entry => entry.LoanOutLocationId == request.LocationId || request.LocationId == null)
            .ToListAsync(cancellationToken);

        return entries
            .SelectMany(entry =>
                entry
                    .Users.Where(entryUser => entryUser.User != null)
                    .Select(entryUser => new { Entry = entry, User = entryUser.User! })
            )
            .GroupBy(entryUser => entryUser.User.Id)
            .Select(userEntries =>
                mapper.ToLoanOutCalendarUser(
                    userEntries.First().User,
                    userEntries.Select(entryUser => entryUser.Entry).ToList()
                )
            )
            .OrderBy(user => user.LastName)
            .ThenBy(user => user.FirstName)
            .ToList();
    }
}
