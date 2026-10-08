using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Calendar.Services;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.TimeOff;
using Unified.TimeOff.Mappings;
using Unified.TimeOff.Models;
using Unified.TimeOff.Models.Scheduling;

namespace Unified.TimeOff.Services;

public sealed class SchedulingTimeOffService(
    ILogger<SchedulingTimeOffService> logger,
    UnifiedDbContext db,
    CalendarLifecycleService calendarLifecycleService,
    IEventSeriesMaterializationService eventSeriesMaterializationService,
    TimeOffSeriesMaterializationHandler timeOffSeriesMaterializationHandler
) : ISchedulingTimeOffService
{
    private static readonly RecurrenceValidationOptions TimeOffRecurrenceValidationOptions = new()
    {
        MaximumDuration = TimeSpan.FromDays(365),
        MaximumOccurrences = 400,
        RequireBoundedRule = true,
    };

    public async Task<SchedulingTimeOffEntryResponse?> GetTimeOffEntryByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await LoadTimeOffEntryAsync(id, tracked: false, cancellationToken);
        return entity is null ? null : SchedulingTimeOffResponseMapper.ToSchedulingTimeOffEntryResponse(entity);
    }

    public async Task<IReadOnlyList<SchedulingTimeOffEntryResponse>> GetTimeOffEntriesByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        var entities = await db
            .TimeOffEntries.AsNoTracking()
            .Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .Include(entry => entry.TimeOffSeries)
            .Where(entry =>
                entry.Users.Any(user => user.UserId == userId)
                || (
                    entry.TimeOffSeriesId.HasValue
                    && db.TimeOffSeriesUsers.Any(seriesUser =>
                        seriesUser.TimeOffSeriesId == entry.TimeOffSeriesId.Value && seriesUser.UserId == userId
                    )
                )
            )
            .Include(entry => entry.TimeOffSeries!)
                .ThenInclude(series => series.EventSeries)
            .Include(entry => entry.TimeOffSeries!)
                .ThenInclude(series => series.Users)
            .OrderByDescending(entry => entry.Event!.StartAtUtc)
            .ToListAsync(cancellationToken);

        return entities.Select(SchedulingTimeOffResponseMapper.ToSchedulingTimeOffEntryResponse).ToList();
    }

    public async Task<IReadOnlyList<SchedulingTimeOffSeriesResponse>> GetTimeOffSeriesByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        var entities = await db
            .TimeOffSeries.AsNoTracking()
            .Include(series => series.EventSeries!)
                .ThenInclude(eventSeries => eventSeries.Events)
            .Include(series => series.Users)
            .Include(series => series.TimeOffEntries)
            .Where(series => series.Users.Any(user => user.UserId == userId))
            .OrderByDescending(series => series.EventSeries!.StartAtUtc)
            .ToListAsync(cancellationToken);

        return entities
            .Select(series =>
                SchedulingTimeOffResponseMapper.ToSchedulingTimeOffSeriesResponse(
                    series,
                    series
                        .EventSeries!.Events.Where(eventEntity =>
                            eventEntity.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                        )
                        .Select(eventEntity => eventEntity.Id)
                        .ToList(),
                    series.TimeOffEntries.Select(entry => entry.Id).ToList()
                )
            )
            .ToList();
    }

    public async Task<SchedulingTimeOffEntryResponse> CreateTimeOffEntryAsync(
        SchedulingTimeOffEntryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var userIds = request.UserIds.Distinct().ToList();
        

        var eventEntity = SchedulingTimeOffEventMapper.ToEvent(request, eventSeriesId: null);
        CalendarEventExceptionHelper.UpdateExceptionFlag(eventEntity);

        var entity = new TimeOffEntry
        {
            LeaveTypeId = request.LeaveTypeId,
            Event = eventEntity,
            Users = userIds.Select(userId => new TimeOffEntryUser { UserId = userId }).ToList(),
        };

        db.TimeOffEntries.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created time off entry {TimeOffEntryId}.", entity.Id);
        return SchedulingTimeOffResponseMapper.ToSchedulingTimeOffEntryResponse(entity);
    }

    public async Task<SchedulingTimeOffSeriesResponse?> GetTimeOffSeriesByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug("Retrieving time off series {TimeOffSeriesId}.", id);

        var entity = await LoadTimeOffSeriesAsync(id, tracked: false, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Time off series {TimeOffSeriesId} was not found.", id);
            return null;
        }

        return await MapTimeOffSeriesResponseAsync(entity, cancellationToken);
    }

    public async Task<SchedulingTimeOffSeriesResponse> CreateTimeOffSeriesAsync(
        SchedulingTimeOffSeriesRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var userIds = request.UserIds.Distinct().ToList();

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );

        var eventSeries = SchedulingTimeOffEventMapper.ToEventSeries(request);
        var entity = new TimeOffSeries
        {
            EventSeries = eventSeries,
            LeaveTypeId = request.LeaveTypeId,
            CreatedOn = DateTimeOffset.UtcNow,
            Users = userIds
                .Select(userId => new TimeOffSeriesUser { UserId = userId, CreatedOn = DateTimeOffset.UtcNow })
                .ToList(),
        };

        db.TimeOffSeries.Add(entity);
        await eventSeriesMaterializationService.MaterializeAsync(
            eventSeries,
            TimeOffRecurrenceValidationOptions,
            timeOffSeriesMaterializationHandler,
            new TimeOffSeriesMaterializationContext { TimeOffSeries = entity, UserIds = userIds },
            cancellationToken
        );

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Created time off series {TimeOffSeriesId}.", entity.Id);
        return await MapTimeOffSeriesResponseAsync(entity, cancellationToken);
    }

    public async Task<SchedulingTimeOffSeriesResponse?> UpdateTimeOffSeriesAsync(
        int id,
        SchedulingTimeOffSeriesRequest request,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Updating time off series {TimeOffSeriesId}.", id);

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );

        var entity = await LoadTimeOffSeriesAsync(id, tracked: true, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Time off series {TimeOffSeriesId} was not found for update.", id);
            return null;
        }

        var eventSeries = entity.EventSeries!;
        SchedulingTimeOffGuards.EnsureTimeOffEventSeriesType(eventSeries);
        SchedulingTimeOffGuards.EnsureTimeOffSeriesIsDraft(eventSeries);
        var existingEntries = entity.TimeOffEntries.ToList();

        var oldValues = CaptureCopiedValues(eventSeries, entity.LeaveTypeId);
        var newUserIds = request.UserIds.Distinct().ToList();
        var recurrenceChanged =
            !StringEqualsNormalized(eventSeries.RecurrenceRule, request.RecurrenceRule)
            || eventSeries.StartAtUtc != request.StartAtUtc
            || eventSeries.EndAtUtc != request.EndAtUtc
            || !StringEqualsNormalized(eventSeries.TimeZoneId, request.TimeZoneId)
            || eventSeries.AllDay != request.AllDay;
        var oldUserIds = entity.Users.Select(user => user.UserId).Distinct().Order().ToList();

        SchedulingTimeOffEventMapper.ApplyToEventSeries(eventSeries, request);
        var newValues = CaptureCopiedValues(eventSeries, request.LeaveTypeId);
        entity.LeaveTypeId = request.LeaveTypeId;
        SyncSeriesUsers(entity, newUserIds);

        if (recurrenceChanged)
        {
            if (existingEntries.Any(entry => entry.Event?.StatusTypeCode != CalendarEventStatusTypeCodes.Draft))
                throw new InvalidOperationException(
                    "Materialized time-off entries cannot be recreated in the current state."
                );

            await eventSeriesMaterializationService.RegenerateDraftSeriesAsync(
                eventSeries,
                TimeOffRecurrenceValidationOptions,
                timeOffSeriesMaterializationHandler,
                new TimeOffSeriesMaterializationContext
                {
                    TimeOffSeries = entity,
                    UserIds = newUserIds,
                    ExistingEntries = existingEntries,
                },
                cancellationToken
            );
        }
        else
        {
            foreach (
                var entry in existingEntries.Where(entry =>
                    db.Entry(entry).State != EntityState.Deleted && entry.Event is not null
                )
            )
            {
                var eventEntity = entry.Event!;
                ApplyCopiedFieldUpdatesPreservingOverrides(eventEntity, oldValues, newValues);
                if (entry.LeaveTypeId == oldValues.LeaveTypeId)
                    entry.LeaveTypeId = newValues.LeaveTypeId;
                if (entry.Users.Select(user => user.UserId).Order().SequenceEqual(oldUserIds))
                    SyncUsers(entry, newUserIds);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Updated time off series {TimeOffSeriesId}.", id);
        return await MapTimeOffSeriesResponseAsync(entity, cancellationToken);
    }

    public async Task<bool> DeleteTimeOffSeriesAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Deleting time off series {TimeOffSeriesId}.", id);

        var entity = await LoadTimeOffSeriesAsync(id, tracked: true, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Time off series {TimeOffSeriesId} was not found for delete.", id);
            return false;
        }

        var eventSeries = entity.EventSeries!;
        SchedulingTimeOffGuards.EnsureTimeOffEventSeriesType(eventSeries);
        SchedulingTimeOffGuards.EnsureTimeOffSeriesIsDraft(eventSeries);

        var seriesEventIds = eventSeries.Events.Select(eventEntity => eventEntity.Id).ToHashSet();
        var seriesEntries = await db
            .TimeOffEntries.Include(entry => entry.Users)
            .Include(entry => entry.Event)
            .Where(entry => seriesEventIds.Contains(entry.EventId))
            .ToListAsync(cancellationToken);
        if (seriesEntries.Any(entry => entry.Event?.StatusTypeCode != CalendarEventStatusTypeCodes.Draft))
            throw new InvalidOperationException(
                "Time off series can only be deleted while all entries are in draft status."
            );

        db.Set<TimeOffEntryUser>().RemoveRange(seriesEntries.SelectMany(entry => entry.Users));
        db.TimeOffEntries.RemoveRange(seriesEntries);
        db.Events.RemoveRange(eventSeries.Events);
        db.TimeOffSeriesUsers.RemoveRange(entity.Users);
        db.TimeOffSeries.Remove(entity);
        db.EventSeries.Remove(eventSeries);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted time off series {TimeOffSeriesId}.", id);
        return true;
    }

    public async Task<SchedulingTimeOffEntryResponse?> UpdateTimeOffEntryAsync(
        int id,
        SchedulingTimeOffEntryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Updating time off entry {TimeOffEntryId}.", id);

        var entity = await LoadTimeOffEntryAsync(id, tracked: true, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Time off entry {TimeOffEntryId} was not found for update.", id);
            return null;
        }

        var eventEntity = entity.Event!;
        SchedulingTimeOffGuards.EnsureTimeOffEventType(eventEntity);
        SchedulingTimeOffGuards.EnsureTimeOffEntryIsDraft(eventEntity);
        SchedulingTimeOffEventMapper.ApplyToEvent(
            eventEntity,
            request,
            entity.TimeOffSeriesId.HasValue ? eventEntity.EventSeriesId : null
        );
        CalendarEventExceptionHelper.UpdateExceptionFlag(eventEntity);
        entity.LeaveTypeId = request.LeaveTypeId;
        SyncUsers(entity, request.UserIds);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated time off entry {TimeOffEntryId}.", id);
        return SchedulingTimeOffResponseMapper.ToSchedulingTimeOffEntryResponse(entity);
    }

    public async Task<bool> DeleteTimeOffEntryAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Deleting time off entry {TimeOffEntryId}.", id);

        var entity = await LoadTimeOffEntryAsync(id, tracked: true, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Time off entry {TimeOffEntryId} was not found for delete.", id);
            return false;
        }

        SchedulingTimeOffGuards.EnsureTimeOffEventType(entity.Event!);
        if (entity.TimeOffSeriesId.HasValue)
            throw new InvalidOperationException("Recurring time-off occurrences must be deleted through their series.");
        if (!calendarLifecycleService.CanDelete(entity.Event!))
            throw new InvalidOperationException("Time off entry can only be deleted while in draft status.");

        db.Set<TimeOffEntryUser>().RemoveRange(entity.Users);
        db.TimeOffEntries.Remove(entity);
        db.Events.Remove(entity.Event!);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted time off entry {TimeOffEntryId}.", id);
        return true;
    }

    private Task<TimeOffEntry?> LoadTimeOffEntryAsync(int id, bool tracked, CancellationToken cancellationToken)
    {
        var query = tracked ? db.TimeOffEntries : db.TimeOffEntries.AsNoTracking();
        return query
            .Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);
    }

    private Task<TimeOffSeries?> LoadTimeOffSeriesAsync(int id, bool tracked, CancellationToken cancellationToken)
    {
        var query = tracked ? db.TimeOffSeries : db.TimeOffSeries.AsNoTracking();
        return query
            .Include(series => series.EventSeries!)
                .ThenInclude(eventSeries => eventSeries.Events)
            .Include(series => series.Users)
            .Include(series => series.TimeOffEntries)
                .ThenInclude(entry => entry.Event)
            .Include(series => series.TimeOffEntries)
                .ThenInclude(entry => entry.Users)
            .SingleOrDefaultAsync(series => series.Id == id, cancellationToken);
    }

    private async Task<SchedulingTimeOffSeriesResponse> MapTimeOffSeriesResponseAsync(
        TimeOffSeries entity,
        CancellationToken cancellationToken
    )
    {
        var eventIds = entity
            .EventSeries!.Events.Where(eventEntity =>
                eventEntity.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
            )
            .OrderBy(eventEntity => eventEntity.StartAtUtc)
            .Select(eventEntity => eventEntity.Id)
            .ToList();
        var entryIds = entity
            .TimeOffEntries.Where(entry => eventIds.Contains(entry.EventId))
            .OrderBy(entry => entry.Id)
            .Select(entry => entry.Id)
            .ToList();

        return SchedulingTimeOffResponseMapper.ToSchedulingTimeOffSeriesResponse(entity, eventIds, entryIds);
    }

    private static EventSeriesCopiedValues CaptureCopiedValues(EventSeries eventSeries, int leaveTypeId) =>
        new(
            eventSeries.Title,
            eventSeries.Description,
            eventSeries.Notes,
            eventSeries.Color,
            eventSeries.LocationId,
            leaveTypeId
        );

    private static void ApplyCopiedFieldUpdatesPreservingOverrides(
        Event eventEntity,
        EventSeriesCopiedValues oldValues,
        EventSeriesCopiedValues newValues
    )
    {
        if (eventEntity.Title == oldValues.Title)
            eventEntity.Title = newValues.Title;
        if (eventEntity.Description == oldValues.Description)
            eventEntity.Description = newValues.Description;
        if (eventEntity.Notes == oldValues.Notes)
            eventEntity.Notes = newValues.Notes;
        if (eventEntity.Color == oldValues.Color)
            eventEntity.Color = newValues.Color;
        if (eventEntity.LocationId == oldValues.LocationId)
            eventEntity.LocationId = newValues.LocationId;
    }

    private static bool StringEqualsNormalized(string? left, string? right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.Ordinal);

    private void SyncSeriesUsers(TimeOffSeries entity, IReadOnlyCollection<Guid> userIds)
    {
        var requestedUserIds = userIds.ToHashSet();
        var usersToRemove = entity.Users.Where(user => !requestedUserIds.Contains(user.UserId)).ToList();
        db.TimeOffSeriesUsers.RemoveRange(usersToRemove);
        foreach (var user in usersToRemove)
            entity.Users.Remove(user);

        var existingUserIds = entity.Users.Select(user => user.UserId).ToHashSet();
        foreach (var userId in requestedUserIds.Where(userId => !existingUserIds.Contains(userId)))
            entity.Users.Add(new TimeOffSeriesUser { TimeOffSeriesId = entity.Id, UserId = userId });
    }

    private void SyncUsers(TimeOffEntry entity, IReadOnlyCollection<Guid> userIds)
    {
        var requestedUserIds = userIds.ToHashSet();

        var usersToRemove = entity.Users.Where(user => !requestedUserIds.Contains(user.UserId)).ToList();
        db.Set<TimeOffEntryUser>().RemoveRange(usersToRemove);
        foreach (var user in usersToRemove)
            entity.Users.Remove(user);

        var existingUserIds = entity.Users.Select(user => user.UserId).ToHashSet();
        foreach (var userId in requestedUserIds.Where(userId => !existingUserIds.Contains(userId)))
            entity.Users.Add(new TimeOffEntryUser { TimeOffEntryId = entity.Id, UserId = userId });
    }

    private sealed record EventSeriesCopiedValues(
        string Title,
        string? Description,
        string? Notes,
        string? Color,
        int? LocationId,
        int LeaveTypeId
    );
}
