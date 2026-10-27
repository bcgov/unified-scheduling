using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Calendar.Conflicts;
using Unified.Calendar.Services;
using Unified.Common.Calendar.Conflicts;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Factories;
using Unified.Scheduling.Mappings;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

internal sealed class ShiftSeriesService(
    ILogger<ShiftSeriesService> logger,
    UnifiedDbContext db,
    ICalendarEventSeriesService<ShiftSeriesRequest, ShiftSeries> calendarEventSeriesService,
    IEventSeriesMaterializationService eventSeriesMaterializationService,
    IRecurrenceExpander recurrenceExpander,
    ShiftSeriesMaterializationHandler shiftSeriesMaterializationHandler,
    IShiftAssignmentService shiftAssignmentService,
    CalendarLifecycleService calendarLifecycleService,
    ITimeZoneService timeZoneService,
    TimeProvider timeProvider,
    ICalendarConflictService calendarConflictService,
    ShiftConflictService shiftConflictService
) : IShiftSeriesService
{
    private static readonly RecurrenceValidationOptions ShiftRecurrenceValidationOptions = new()
    {
        MaximumDuration = TimeSpan.FromDays(365),
        MaximumOccurrences = 400,
        RequireBoundedRule = true,
    };

    public async Task<IReadOnlyCollection<ShiftSeriesResponse>> GetShiftSeriesAsync(
        ShiftSeriesQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug(
            "Querying shift series with EventSeriesId {EventSeriesId}, UserId {UserId}, LocationId {LocationId}, StartAtUtc {StartAtUtc}, and EndAtUtc {EndAtUtc}.",
            queryParams?.EventSeriesId,
            queryParams?.UserId,
            queryParams?.LocationId,
            queryParams?.StartAtUtc,
            queryParams?.EndAtUtc
        );
        IQueryable<ShiftSeries> query = db
            .ShiftSeries.AsNoTracking()
            .Include(series => series.EventSeries)
            .Include(series => series.Users);
        if (queryParams?.EventSeriesId is int eventSeriesId)
            query = query.Where(series => series.EventSeriesId == eventSeriesId);
        if (queryParams?.UserId is Guid userId)
            query = query.Where(series => series.Users.Any(user => user.UserId == userId));
        if (queryParams?.LocationId is int locationId)
            query = query.Where(series => series.EventSeries != null && series.EventSeries.LocationId == locationId);
        List<ShiftSeries> results;
        if (queryParams?.StartAtUtc is DateTimeOffset rangeStart && queryParams.EndAtUtc is DateTimeOffset rangeEnd)
        {
            query = query.Where(series =>
                series.EventSeries != null
                && series.EventSeries.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                && series.EventSeries.StartAtUtc < rangeEnd
            );
            var candidates = await query
                .OrderBy(series => series.EventSeriesId)
                .ThenBy(series => series.Id)
                .ToListAsync(cancellationToken);
            results = candidates
                .Where(series =>
                    series.EventSeries is not null
                    && recurrenceExpander.CountWithin(series.EventSeries, rangeStart, rangeEnd, stopAfter: 1) > 0
                )
                .ToList();
        }
        else
        {
            if (queryParams?.StartAtUtc.HasValue == true || queryParams?.EndAtUtc.HasValue == true)
            {
                var start = queryParams?.StartAtUtc;
                var end = queryParams?.EndAtUtc;
                query = query.Where(series =>
                    series.ShiftEntries.Any(entry =>
                        entry.Event != null
                        && (!end.HasValue || entry.Event.StartAtUtc < end.Value)
                        && (
                            !start.HasValue
                            || (entry.Event.EndAtUtc.HasValue && entry.Event.EndAtUtc.Value > start.Value)
                        )
                    )
                );
            }
            results = await query
                .OrderBy(series => series.EventSeriesId)
                .ThenBy(series => series.Id)
                .ToListAsync(cancellationToken);
        }
        logger.LogDebug("Shift series query returned {ShiftSeriesCount} records.", results.Count);
        return await MapToShiftSeriesResponsesAsync(results, cancellationToken);
    }

    public async Task<ShiftSeriesResponse?> GetShiftSeriesByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug("Retrieving shift series {ShiftSeriesId}.", id);
        var result = await db
            .ShiftSeries.AsNoTracking()
            .Include(series => series.EventSeries)
            .Include(series => series.Users)
            .Where(series => series.Id == id)
            .SingleOrDefaultAsync(cancellationToken);
        if (result is null)
            logger.LogInformation("Shift series {ShiftSeriesId} was not found.", id);
        return result is null ? null : await MapToShiftSeriesResponseAsync(result, cancellationToken);
    }

    public async Task<ShiftSeriesResponse> CreateShiftSeriesAsync(
        ShiftSeriesRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var userIds = ShiftUserSync.GetDistinctUserIds(request.UserIds);
        logger.LogInformation(
            "Creating shift series for users {UserIds} starting at {StartAtUtc}.",
            string.Join(",", userIds),
            request.StartAtUtc
        );
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        var entity = await calendarEventSeriesService.BuildEventAsync(
            request with
            {
                UserIds = userIds,
            },
            SchedulingConstants.SourceModule,
            SchedulingConstants.ShiftEventTypeCode,
            cancellationToken: cancellationToken
        );
        var eventSeries =
            entity.EventSeries
            ?? throw new InvalidOperationException("The shift-series factory did not create an event series.");
        entity.Users = userIds.Select(userId => new ShiftSeriesUser { UserId = userId }).ToList();
        db.EventSeries.Add(eventSeries);
        db.ShiftSeries.Add(entity);
        await eventSeriesMaterializationService.MaterializeAsync(
            eventSeries,
            ShiftRecurrenceValidationOptions,
            shiftSeriesMaterializationHandler,
            new ShiftSeriesMaterializationContext { ShiftSeries = entity, UserIds = userIds },
            cancellationToken
        );
        await db.SaveChangesAsync(cancellationToken);
        await shiftAssignmentService.ReplaceShiftSeriesLinksAsync(
            entity.Id,
            request.AssignmentSeriesLinks,
            cancellationToken
        );
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Created shift series {ShiftSeriesId}.", entity.Id);
        return await MapToShiftSeriesResponseAsync(entity, cancellationToken);
    }

    public async Task<ShiftSeriesResponse?> UpdateShiftSeriesAsync(
        int id,
        ShiftSeriesRequest request,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Updating shift series {ShiftSeriesId}.", id);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );
        var entity = await db
            .ShiftSeries.Include(series => series.EventSeries!)
                .ThenInclude(eventSeries => eventSeries.Events)
            .Include(series => series.Users)
            .Include(series => series.ShiftEntries)
                .ThenInclude(entry => entry.Event)
            .Include(series => series.ShiftEntries)
                .ThenInclude(entry => entry.Users)
            .Include(series => series.ShiftEntries)
                .ThenInclude(entry => entry.ShiftAssignmentEntries)
                    .ThenInclude(link => link.Users)
            .Include(series => series.ShiftEntries)
                .ThenInclude(entry => entry.ShiftAssignmentEntries)
                    .ThenInclude(link => link.AssignmentEntry)
                        .ThenInclude(assignment => assignment!.Event)
            .Include(series => series.ShiftEntries)
                .ThenInclude(entry => entry.ShiftAssignmentEntries)
                    .ThenInclude(link => link.ShiftAssignmentSeriesLink)
            .SingleOrDefaultAsync(series => series.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Shift series {ShiftSeriesId} was not found for update.", id);
            return null;
        }
        var eventSeries = entity.EventSeries!;
        var oldValues = ShiftSeriesUpdatePlanner.CaptureCopiedValues(eventSeries);
        var oldUserIds = entity.Users.Select(user => user.UserId).Distinct().Order().ToList();
        var recurrenceChanged = calendarEventSeriesService.HasRecurrenceChanged(entity, request);
        var newUserIds = ShiftUserSync.GetDistinctUserIds(request.UserIds);
        var updated = await calendarEventSeriesService.UpdateEventAsync(
            request,
            entity,
            SchedulingConstants.SourceModule,
            SchedulingConstants.ShiftEventTypeCode,
            validators:
            [
                (input, _) =>
                {
                    if (string.IsNullOrWhiteSpace(input.RecurrenceRule))
                        throw new ArgumentException("A recurring shift series cannot be converted to a single shift.");

                    return Task.CompletedTask;
                },
            ],
            cancellationToken: cancellationToken
        );
        if (updated is null)
            throw new InvalidOperationException("Failed to update shift series.");
        ShiftUserSync.SyncSeriesUsers(db, entity, newUserIds);
        if (recurrenceChanged)
        {
            await shiftAssignmentService.ReplaceShiftSeriesLinksAsync(entity.Id, [], cancellationToken);
            await eventSeriesMaterializationService.RegenerateDraftSeriesAsync(
                eventSeries,
                ShiftRecurrenceValidationOptions,
                shiftSeriesMaterializationHandler,
                new ShiftSeriesMaterializationContext
                {
                    ShiftSeries = entity,
                    UserIds = newUserIds,
                    ExistingEntries = entity.ShiftEntries.ToList(),
                },
                cancellationToken
            );
        }
        else
        {
            ApplySeriesNonRecurrenceUpdatesToChildren(entity, oldValues, oldUserIds, newUserIds);
        }
        var currentEntries = entity.ShiftEntries.Where(entry => db.Entry(entry).State != EntityState.Deleted).ToList();
        ValidatePropagatedShiftEntries(entity, currentEntries);
        await db.SaveChangesAsync(cancellationToken);
        await shiftAssignmentService.ReplaceShiftSeriesLinksAsync(
            entity.Id,
            request.AssignmentSeriesLinks,
            cancellationToken
        );
        var conflictCandidates = await SchedulingConflictParticipantProvider.GetParticipantsForShiftEntriesAsync(
            db,
            entity.ShiftEntries.Select(entry => entry.Id).ToList(),
            cancellationToken
        );
        await calendarConflictService.EnsureNoUnresolvedConflictsAsync(conflictCandidates, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Updated shift series {ShiftSeriesId}.", id);
        return await MapToShiftSeriesResponseAsync(entity, cancellationToken);
    }

    private void ApplySeriesNonRecurrenceUpdatesToChildren(
        ShiftSeries shiftSeries,
        EventSeriesCopiedValues oldValues,
        IReadOnlyCollection<Guid> oldUserIds,
        IReadOnlyCollection<Guid> newUserIds
    )
    {
        foreach (
            var entry in shiftSeries.ShiftEntries.Where(entry =>
                entry.Event is not null
                && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                && entry.Event.LocationId == oldValues.LocationId
            )
        )
        {
            ShiftSeriesUpdatePlanner.ApplyCopiedFieldUpdatesPreservingOverrides(
                entry.Event!,
                oldValues,
                shiftSeries.EventSeries!
            );
            if (ShiftUserSync.UserSetsEqual(entry.Users.Select(user => user.UserId), oldUserIds))
                ShiftUserSync.SyncEntryUsers(db, entry, newUserIds);
        }
    }

    public async Task<ShiftSeriesResponse?> PublishShiftSeriesAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Publishing shift series {ShiftSeriesId}.", id);
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        var entity = await PublishSeriesAsync(id, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Shift series {ShiftSeriesId} was not found for publish.", id);
            return null;
        }
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Published shift series {ShiftSeriesId}.", id);
        return await MapToShiftSeriesResponseAsync(entity, cancellationToken);
    }

    private async Task<ShiftSeries?> PublishSeriesAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await db
            .ShiftSeries.Include(series => series.EventSeries!)
                .ThenInclude(eventSeries => eventSeries.Events)
            .Include(series => series.Users)
            .Include(series => series.ShiftEntries)
                .ThenInclude(entry => entry.Event)
            .Include(series => series.ShiftEntries)
                .ThenInclude(entry => entry.Users)
            .SingleOrDefaultAsync(series => series.Id == id, cancellationToken);
        if (entity is null)
            return null;
        ShiftGuards.EnsureShiftEventSeriesType(entity.EventSeries!);
        var eventSeries = entity.EventSeries!;
        var draftEntries = entity
            .ShiftEntries.Where(entry => entry.Event?.StatusTypeCode == CalendarEventStatusTypeCodes.Draft)
            .ToList();
        await shiftConflictService.EnsureShiftsDoNotConflictAsync(
            draftEntries,
            draftEntries.Select(entry => entry.Id).ToList(),
            cancellationToken
        );
        calendarLifecycleService.PublishSeries(eventSeries, eventSeries.Events.ToList());
        var entryIds = await db
            .ShiftEntries.Where(entry => entry.ShiftSeriesId == id)
            .Select(entry => entry.Id)
            .ToListAsync(cancellationToken);
        await PublishLinkedDraftAssignmentsAsync(entryIds, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        var conflictCandidates = await SchedulingConflictParticipantProvider.GetParticipantsForShiftEntriesAsync(
            db,
            entryIds,
            cancellationToken
        );
        await calendarConflictService.EnsureNoUnresolvedConflictsAsync(conflictCandidates, cancellationToken);
        return entity;
    }

    public async Task<ShiftSeriesResponse?> ExpireShiftSeriesAsync(
        int id,
        ExpireShiftRequest request,
        Guid? cancelledByUserId = null,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Expiring shift series {ShiftSeriesId}.", id);
        var entity = await db
            .ShiftSeries.Include(series => series.EventSeries!)
                .ThenInclude(eventSeries => eventSeries.Events)
            .Include(series => series.Users)
            .SingleOrDefaultAsync(series => series.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Shift series {ShiftSeriesId} was not found for expire.", id);
            return null;
        }
        var linkedEventIds = await LoadLinkedAssignmentEventIdsAsync(id, cancellationToken);
        ShiftGuards.EnsureShiftEventSeriesType(entity.EventSeries!);
        var eventSeries = entity.EventSeries!;
        calendarLifecycleService.CancelSeries(
            eventSeries,
            eventSeries.Events.ToList(),
            timeProvider.GetUtcNow(),
            cancelledByUserId,
            request.CancellationReason
        );
        await db.SaveChangesAsync(cancellationToken);
        await InvalidateLinkedOverridesAsync(linkedEventIds, cancelledByUserId, cancellationToken);
        logger.LogInformation("Expired shift series {ShiftSeriesId}.", id);
        return await MapToShiftSeriesResponseAsync(entity, cancellationToken);
    }

    public async Task<bool> DeleteShiftSeriesAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Deleting shift series {ShiftSeriesId}.", id);
        var entity = await db
            .ShiftSeries.Include(series => series.EventSeries)
            .Include(series => series.Users)
            .SingleOrDefaultAsync(series => series.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Shift series {ShiftSeriesId} was not found for delete.", id);
            return false;
        }
        var eventSeries = entity.EventSeries!;
        ShiftGuards.EnsureShiftEventSeriesType(eventSeries);
        var childEvents = await db
            .Events.Where(eventEntity =>
                eventEntity.EventSeriesId == eventSeries.Id
                && eventEntity.EventTypeCode == SchedulingConstants.ShiftEventTypeCode
                && eventEntity.SourceModule == SchedulingConstants.SourceModule
            )
            .ToListAsync(cancellationToken);
        if (!calendarLifecycleService.CanDelete(eventSeries))
            throw new InvalidOperationException(
                "Shift series can only be deleted while the series is in draft status."
            );
        var entries = await db
            .ShiftEntries.Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .Where(entry => entry.ShiftSeriesId == entity.Id)
            .ToListAsync(cancellationToken);
        var draftEvents = childEvents
            .Where(eventEntity => eventEntity.StatusTypeCode == CalendarEventStatusTypeCodes.Draft)
            .ToList();
        var draftEventIds = draftEvents.Select(eventEntity => eventEntity.Id).ToHashSet();
        var draftEntries = entries.Where(entry => draftEventIds.Contains(entry.EventId)).ToList();
        var preservedDraftEntries = draftEntries
            .Where(entry => entry.Event!.LocationId != eventSeries.LocationId)
            .ToList();
        draftEntries = draftEntries.Except(preservedDraftEntries).ToList();
        var draftEntryIds = draftEntries.Select(entry => entry.Id).ToHashSet();
        var draftLinks = await db
            .ShiftAssignmentEntries.Include(link => link.Users)
            .Where(link => draftEntryIds.Contains(link.ShiftEntryId))
            .ToListAsync(cancellationToken);
        var seriesLinks = await db
            .ShiftAssignmentSeriesLinks.Include(link => link.Users)
            .Include(link => link.EntryLinks)
                .ThenInclude(link => link.Users)
            .Where(link => link.ShiftSeriesId == entity.Id)
            .ToListAsync(cancellationToken);
        foreach (var retainedEntry in entries.Except(draftEntries))
            retainedEntry.ShiftSeriesId = null;
        foreach (var retainedEvent in childEvents.Except(draftEvents))
            retainedEvent.EventSeriesId = null;
        var retainedLinks = seriesLinks
            .SelectMany(link => link.EntryLinks)
            .Where(link => !draftEntryIds.Contains(link.ShiftEntryId))
            .ToList();
        var suppressedLinks = retainedLinks.Where(link => link.Users.Count == 0).ToList();
        foreach (var retainedLink in retainedLinks.Except(suppressedLinks))
        {
            retainedLink.ShiftAssignmentSeriesLinkId = null;
            retainedLink.ShiftAssignmentSeriesLink = null;
            retainedLink.IsException = false;
        }
        RemoveShiftAssignmentLinks(draftLinks.Concat(suppressedLinks).ToList());
        db.ShiftAssignmentSeriesLinkUsers.RemoveRange(seriesLinks.SelectMany(link => link.Users));
        db.ShiftAssignmentSeriesLinks.RemoveRange(seriesLinks);
        db.ShiftEntryUsers.RemoveRange(draftEntries.SelectMany(entry => entry.Users));
        db.ShiftEntries.RemoveRange(draftEntries);
        db.ShiftSeriesUsers.RemoveRange(entity.Users);
        db.ShiftSeries.Remove(entity);
        db.Events.RemoveRange(draftEvents);
        db.EventSeries.Remove(eventSeries);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted shift series {ShiftSeriesId}.", id);
        return true;
    }

    public async Task<ShiftSeries?> GetShiftSeriesEntityForEntryAsync(
        int id,
        CancellationToken cancellationToken = default
    ) =>
        await db
            .ShiftSeries.Include(series => series.EventSeries)
            .SingleOrDefaultAsync(series => series.Id == id, cancellationToken);

    private async Task<IReadOnlyCollection<ShiftSeriesResponse>> MapToShiftSeriesResponsesAsync(
        IReadOnlyCollection<ShiftSeries> series,
        CancellationToken cancellationToken
    )
    {
        var entryIds = await LoadShiftSeriesEntryIdsAsync(series, cancellationToken);
        return series
            .Select(item =>
                ShiftResponseMapper.ToShiftSeriesResponse(
                    item,
                    item.EventSeries,
                    entryIds.GetValueOrDefault(item.Id, [])
                )
            )
            .ToList();
    }

    private async Task<ShiftSeriesResponse> MapToShiftSeriesResponseAsync(
        ShiftSeries series,
        CancellationToken cancellationToken
    )
    {
        var ids = await LoadShiftSeriesEntryIdsAsync([series], cancellationToken);
        return ShiftResponseMapper.ToShiftSeriesResponse(
            series,
            series.EventSeries,
            ids.GetValueOrDefault(series.Id, [])
        );
    }

    private async Task<Dictionary<int, List<ShiftSeriesEntryIds>>> LoadShiftSeriesEntryIdsAsync(
        IReadOnlyCollection<ShiftSeries> series,
        CancellationToken cancellationToken
    )
    {
        if (series.Count == 0)
            return [];
        var seriesIds = series.Select(item => item.Id).ToList();
        var eventSeriesIds = series.Select(item => item.EventSeriesId).ToList();
        var ids = await db
            .ShiftEntries.AsNoTracking()
            .Where(entry => entry.ShiftSeriesId.HasValue && seriesIds.Contains(entry.ShiftSeriesId.Value))
            .Where(entry =>
                entry.Event != null
                && entry.Event.EventSeriesId.HasValue
                && eventSeriesIds.Contains(entry.Event.EventSeriesId.Value)
                && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
            )
            .OrderBy(entry => entry.Id)
            .Select(entry => new ShiftSeriesEntryIds(entry.ShiftSeriesId!.Value, entry.Id, entry.EventId))
            .ToListAsync(cancellationToken);
        return ids.GroupBy(entry => entry.ShiftSeriesId).ToDictionary(group => group.Key, group => group.ToList());
    }

    private async Task<IReadOnlyCollection<int>> LoadLinkedAssignmentEventIdsAsync(
        int seriesId,
        CancellationToken cancellationToken
    ) =>
        await db
            .ShiftAssignmentEntries.Where(link => link.ShiftEntry!.ShiftSeriesId == seriesId)
            .Select(link => link.AssignmentEntry!.EventId)
            .Distinct()
            .ToListAsync(cancellationToken);

    private Task InvalidateLinkedOverridesAsync(
        IReadOnlyCollection<int> eventIds,
        Guid? updatedById,
        CancellationToken cancellationToken
    ) =>
        calendarConflictService.InvalidateResolvedOverridesAsync(
            eventIds
                .Select(eventId => new CalendarConflictEventIdentity(
                    SchedulingConstants.SourceModule,
                    eventId.ToString()
                ))
                .ToList(),
            updatedById,
            cancellationToken
        );

    private async Task PublishLinkedDraftAssignmentsAsync(
        IReadOnlyCollection<int> entryIds,
        CancellationToken cancellationToken
    )
    {
        var events = await db
            .ShiftAssignmentEntries.Where(link => entryIds.Contains(link.ShiftEntryId))
            .Select(link => link.AssignmentEntry!.Event!)
            .Where(eventEntity => eventEntity.StatusTypeCode == CalendarEventStatusTypeCodes.Draft)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var eventEntity in events)
            calendarLifecycleService.Publish(eventEntity);
    }

    private void ValidatePropagatedShiftEntries(ShiftSeries series, IReadOnlyCollection<ShiftEntry> entries)
    {
        foreach (
            var entry in entries.Where(entry => entry.Event?.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled)
        )
            ShiftAssignmentGuards.EnsureShiftEntryUpdatePreservesLinks(
                entry,
                entry.Event!.StartAtUtc,
                entry.Event.EndAtUtc,
                entry.Event.TimeZoneId,
                entry.Users.Select(user => user.UserId).ToList(),
                series.Id,
                timeZoneService
            );
    }

    private void RemoveShiftAssignmentLinks(IReadOnlyCollection<ShiftAssignmentEntry> links)
    {
        db.ShiftAssignmentEntryUsers.RemoveRange(links.SelectMany(link => link.Users));
        db.ShiftAssignmentEntries.RemoveRange(links);
    }
}
