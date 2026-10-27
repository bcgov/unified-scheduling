using Unified.Calendar.Services;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.LoanOut;
using Unified.LoanOut.Constants;
using Unified.LoanOut.Models;

namespace Unified.LoanOut.Services;

public sealed class LoanOutSeriesMaterializationHandler(UnifiedDbContext db)
    : IEventSeriesMaterializationHandler<LoanOutSeriesMaterializationContext>
{
    public string SourceModule => LoanOutConstants.SourceModule;

    public string EventTypeCode => LoanOutConstants.LoanOutEventTypeCode;

    public Task OnMaterializedEventsDeletingAsync(
        EventSeries eventSeries,
        IReadOnlyCollection<Event> existingEvents,
        LoanOutSeriesMaterializationContext context,
        CancellationToken cancellationToken
    )
    {
        var existingEventIds = existingEvents.Select(eventEntity => eventEntity.Id).ToHashSet();
        var existingEntries = context.ExistingEntries.Where(entry => existingEventIds.Contains(entry.EventId)).ToList();
        db.Set<LoanOutEntryUser>().RemoveRange(existingEntries.SelectMany(entry => entry.Users));
        db.LoanOutEntries.RemoveRange(existingEntries);
        return Task.CompletedTask;
    }

    public Task OnMaterializedEventCreatedAsync(
        EventSeries eventSeries,
        Event eventEntity,
        SeriesEntry occurrence,
        LoanOutSeriesMaterializationContext context,
        CancellationToken cancellationToken
    )
    {
        db.LoanOutEntries.Add(
            new LoanOutEntry
            {
                LoanOutSeries = context.LoanOutSeries,
                LoanOutLocationId = context.LoanOutSeries.LoanOutLocationId,
                Event = eventEntity,
                Users = context.UserIds.Select(userId => new LoanOutEntryUser { UserId = userId }).ToList(),
            }
        );

        return Task.CompletedTask;
    }
}
