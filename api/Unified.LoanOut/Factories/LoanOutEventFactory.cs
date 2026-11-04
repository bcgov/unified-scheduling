using Unified.Calendar.Factories;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.LoanOut;
using Unified.LoanOut.Models;

namespace Unified.LoanOut.Factories;

public sealed class LoanOutEventFactory
    : ICalendarEventEntryFactory<LoanOutEntryRequest, LoanOutEntry>,
        ICalendarEventSeriesFactory<LoanOutSeriesRequest, LoanOutSeries>
{
    public LoanOutEntry CreateEntry(LoanOutEntryRequest request, Event eventEntity) =>
        new()
        {
            Event = eventEntity,
            LoanOutSeriesId = request.LoanOutSeriesId,
            LoanOutLocationId = request.LoanOutLocationId,
            Users = request.UserIds.Distinct().Select(userId => new LoanOutEntryUser { UserId = userId }).ToList(),
        };

    public Event GetEvent(LoanOutEntry entity) =>
        entity.Event ?? throw new InvalidOperationException($"LoanOut entry {entity.Id} has no associated event.");

    public LoanOutEntry UpdateEntry(LoanOutEntryRequest request, LoanOutEntry existing)
    {
        existing.LoanOutSeriesId = request.LoanOutSeriesId;
        existing.LoanOutLocationId = request.LoanOutLocationId;
        SyncEntryUsers(existing, request.UserIds);
        return existing;
    }

    public static void SyncEntryUsers(LoanOutEntry entity, IReadOnlyCollection<Guid> userIds)
    {
        var requestedUserIds = userIds.Distinct().ToHashSet();
        entity.Users = entity.Users.Where(user => requestedUserIds.Contains(user.UserId)).ToList();

        var existingUserIds = entity.Users.Select(user => user.UserId).ToHashSet();
        foreach (var userId in requestedUserIds.Where(userId => !existingUserIds.Contains(userId)))
            entity.Users.Add(new LoanOutEntryUser { UserId = userId });
    }

    public LoanOutSeries CreateSeries(LoanOutSeriesRequest request, EventSeries eventSeries) =>
        new()
        {
            EventSeries = eventSeries,
            LoanOutLocationId = request.LoanOutLocationId,
            Users = request.UserIds.Distinct().Select(userId => new LoanOutSeriesUser { UserId = userId }).ToList(),
        };

    public EventSeries GetEventSeries(LoanOutSeries entity) =>
        entity.EventSeries
        ?? throw new InvalidOperationException($"LoanOut series {entity.Id} has no associated event series.");

    public LoanOutSeries UpdateSeries(LoanOutSeriesRequest request, LoanOutSeries existing)
    {
        existing.LoanOutLocationId = request.LoanOutLocationId;
        SyncSeriesUsers(existing, request.UserIds);
        return existing;
    }

    public static void SyncSeriesUsers(LoanOutSeries entity, IReadOnlyCollection<Guid> userIds)
    {
        var requestedUserIds = userIds.Distinct().ToHashSet();
        entity.Users = entity.Users.Where(user => requestedUserIds.Contains(user.UserId)).ToList();

        var existingUserIds = entity.Users.Select(user => user.UserId).ToHashSet();
        var createdOn = DateTimeOffset.UtcNow;
        foreach (var userId in requestedUserIds.Where(userId => !existingUserIds.Contains(userId)))
            entity.Users.Add(new LoanOutSeriesUser { UserId = userId, CreatedOn = createdOn });
    }
}
