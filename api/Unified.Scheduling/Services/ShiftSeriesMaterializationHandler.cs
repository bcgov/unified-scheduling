using Microsoft.EntityFrameworkCore;
using Unified.Calendar.Services;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;

namespace Unified.Scheduling.Services;

public sealed class ShiftSeriesMaterializationHandler(UnifiedDbContext db) : IEventSeriesMaterializationHandler
{
    public string SourceModule => SchedulingConstants.SourceModule;

    public string EventTypeCode => SchedulingConstants.ShiftEventTypeCode;

    public async Task<IEventSeriesMaterializationContext> CreateContextAsync(
        EventSeries eventSeries,
        CancellationToken cancellationToken
    )
    {
        var shiftSeries =
            await db
                .ShiftSeries.AsNoTracking()
                .Include(series => series.Users)
                .SingleOrDefaultAsync(series => series.EventSeriesId == eventSeries.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Shift series for event series {eventSeries.Id} was not found.");

        return new ShiftSeriesMaterializationContext
        {
            ShiftSeriesId = shiftSeries.Id,
            UserIds = shiftSeries.Users.Select(user => user.UserId).Distinct().ToList(),
        };
    }

    /// <summary>
    /// Block recreation of a series if it is not in draft, or if any entry in that series is not
    /// </summary>
    /// <param name="eventSeries"></param>
    /// <param name="events">A list of events for the given series</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<bool> CanRecreateSeriesEntriesAsync(
        EventSeries eventSeries,
        IEnumerable<Event> events,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult(
            eventSeries.StatusTypeCode == CalendarEventStatusTypeCodes.Draft
                && events.All(e => e.StatusTypeCode == CalendarEventStatusTypeCodes.Draft)
        );
    }

    public async Task OnEventCreatedAsync(
        EventSeries eventSeries,
        Event eventEntity,
        SeriesEntry occurrence,
        IEventSeriesMaterializationContext context,
        CancellationToken cancellationToken
    )
    {
        var shiftContext = GetShiftSeriesContext(context);

        db.ShiftEntries.Add(
            new ShiftEntry
            {
                ShiftSeriesId = shiftContext.ShiftSeriesId,
                Event = eventEntity,
                Users = shiftContext.UserIds.Select(userId => new ShiftEntryUser { UserId = userId }).ToList(),
            }
        );

        await Task.CompletedTask;
    }

    public async Task OnEventRemovedAsync(
        EventSeries eventSeries,
        Event eventEntity,
        IEventSeriesMaterializationContext context,
        CancellationToken cancellationToken
    )
    {
        var shiftEntry = await db
            .ShiftEntries.Include(entry => entry.Users)
            .Include(entry => entry.Event)
            .SingleOrDefaultAsync(entry => entry.EventId == eventEntity.Id, cancellationToken);

        if (shiftEntry is null)
            return;

        var hasHistoricalLinks = await db.ShiftAssignmentEntries.AnyAsync(
            link => link.ShiftEntryId == shiftEntry.Id,
            cancellationToken
        );
        if (hasHistoricalLinks)
        {
            eventEntity.StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled;
            eventEntity.CancelledAt = DateTimeOffset.UtcNow;
            eventEntity.CancellationReason = "Removed from shift series during rematerialization.";
            return;
        }

        db.ShiftEntryUsers.RemoveRange(shiftEntry.Users);
        db.ShiftEntries.Remove(shiftEntry);
    }

    private static ShiftSeriesMaterializationContext GetShiftSeriesContext(
        IEventSeriesMaterializationContext context
    ) => (ShiftSeriesMaterializationContext)context;
}
