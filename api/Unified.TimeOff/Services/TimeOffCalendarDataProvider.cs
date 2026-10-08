using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.TimeOff;
using Unified.TimeOff.Constants;
using Unified.TimeOff.Mappings;
using Unified.TimeOff.Models.Calendar;

namespace Unified.TimeOff.Services;

public sealed class TimeOffCalendarDataProvider(UnifiedDbContext db) : ITimeOffCalendarDataProvider
{
    public async Task<IReadOnlyCollection<TimeOffCalendarEvent>> GetEventsAsync(
        CalendarEventQueryContext queryContext,
        CancellationToken cancellationToken = default
    )
    {
        var entriesQuery = db
            .TimeOffEntries.AsNoTracking()
            .Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .Include(entry => entry.TimeOffSeries!)
                .ThenInclude(series => series.EventSeries)
            .Where(entry =>
                entry.Event != null
                && entry.Event.SourceModule == TimeOffConstants.SourceModule
                && entry.Event.EventTypeCode == TimeOffConstants.TimeOffEventTypeCode
                && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                && entry.Event.StartAtUtc < queryContext.EndAtUtc
                && (entry.Event.EndAtUtc ?? entry.Event.StartAtUtc) > queryContext.StartAtUtc
            );

        if (queryContext.LocationId.HasValue)
            entriesQuery = entriesQuery.Where(entry =>
                entry.Event!.LocationId == null || entry.Event.LocationId == queryContext.LocationId.Value
            );

        if (queryContext.UserIds.Count > 0)
            entriesQuery = entriesQuery.Where(entry =>
                entry.Users.Any(user => queryContext.UserIds.Contains(user.UserId))
                || (
                    entry.TimeOffSeriesId.HasValue
                    && db.TimeOffSeriesUsers.Any(seriesUser =>
                        seriesUser.TimeOffSeriesId == entry.TimeOffSeriesId.Value
                        && queryContext.UserIds.Contains(seriesUser.UserId)
                    )
                )
            );

        var entries = await entriesQuery.ToListAsync(cancellationToken);

        var seriesQuery = db
            .TimeOffSeries.AsNoTracking()
            .Include(series => series.Users)
            .Include(series => series.EventSeries)
            .Where(series =>
                series.EventSeries!.Events.Any(eventEntity =>
                    eventEntity.SourceModule == TimeOffConstants.SourceModule
                    && eventEntity.EventTypeCode == TimeOffConstants.TimeOffEventTypeCode
                    && eventEntity.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                    && eventEntity.StartAtUtc < queryContext.EndAtUtc
                    && (eventEntity.EndAtUtc ?? eventEntity.StartAtUtc) > queryContext.StartAtUtc
                )
            );

        if (queryContext.LocationId.HasValue)
            seriesQuery = seriesQuery.Where(series =>
                series.EventSeries!.LocationId == null || series.EventSeries.LocationId == queryContext.LocationId.Value
            );

        if (queryContext.UserIds.Count > 0)
            seriesQuery = seriesQuery.Where(series =>
                series.Users.Any(user => queryContext.UserIds.Contains(user.UserId))
            );

        var seriesEntries = await seriesQuery
            .Select(series => new
            {
                Series = series,
                Events = series.EventSeries!.Events.Where(eventEntity =>
                    eventEntity.SourceModule == TimeOffConstants.SourceModule
                    && eventEntity.EventTypeCode == TimeOffConstants.TimeOffEventTypeCode
                    && eventEntity.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                    && eventEntity.StartAtUtc < queryContext.EndAtUtc
                    && (eventEntity.EndAtUtc ?? eventEntity.StartAtUtc) > queryContext.StartAtUtc
                ),
            })
            .ToListAsync(cancellationToken);

        var knownEventIds = entries.Select(entry => entry.EventId).ToHashSet();
        foreach (var seriesData in seriesEntries)
        {
            var assignedUserIds = seriesData.Series.Users.Select(user => user.UserId).Distinct().ToList();
            foreach (var eventEntity in seriesData.Events.Where(item => !knownEventIds.Contains(item.Id)))
            {
                entries.Add(
                    new TimeOffEntry
                    {
                        TimeOffSeriesId = seriesData.Series.Id,
                        TimeOffSeries = seriesData.Series,
                        LeaveTypeId = seriesData.Series.LeaveTypeId,
                        EventId = eventEntity.Id,
                        Event = eventEntity,
                        Users = assignedUserIds.Select(userId => new TimeOffEntryUser { UserId = userId }).ToList(),
                    }
                );
                knownEventIds.Add(eventEntity.Id);
            }
        }

        var leaveTypeIds = entries.Select(entry => entry.LeaveTypeId).Distinct().ToList();
        var leaveTypeNames = await db
            .LeaveTypes.AsNoTracking()
            .Where(leaveType => leaveTypeIds.Contains(leaveType.Id))
            .ToDictionaryAsync(leaveType => leaveType.Id, leaveType => leaveType.Code, cancellationToken);

        return entries
            .Select(entry =>
                SchedulingTimeOffResponseMapper.ToCalendarEventResponse(
                    entry,
                    leaveTypeNames.GetValueOrDefault(entry.LeaveTypeId)
                )
            )
            .ToList();
    }
}
