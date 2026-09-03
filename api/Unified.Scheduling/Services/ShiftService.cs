using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Calendar.Conflicts;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Mappings;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public sealed class ShiftService(
    ILogger<ShiftService> logger,
    UnifiedDbContext db,
    IEventSeriesMaterializationService eventSeriesMaterializationService,
    IRecurrenceExpander recurrenceExpander,
    ShiftSeriesMaterializationHandler shiftSeriesMaterializationHandler,
    IShiftAssignmentService shiftAssignmentService,
    CalendarLifecycleService calendarLifecycleService,
    TimeProvider timeProvider,
    ICalendarConflictService calendarConflictService
) : IShiftService
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
            .Include(shiftSeries => shiftSeries.EventSeries)
            .Include(shiftSeries => shiftSeries.Users);

        if (queryParams?.EventSeriesId is int eventSeriesId)
            query = query.Where(shiftSeries => shiftSeries.EventSeriesId == eventSeriesId);

        if (queryParams?.UserId is Guid userId)
            query = query.Where(shiftSeries => shiftSeries.Users.Any(user => user.UserId == userId));

        if (queryParams?.LocationId is int locationId)
            query = query.Where(shiftSeries =>
                shiftSeries.EventSeries != null && shiftSeries.EventSeries.LocationId == locationId
            );

        if (queryParams?.StartAtUtc is DateTimeOffset rangeStart && queryParams.EndAtUtc is DateTimeOffset rangeEnd)
        {
            query = query.Where(shiftSeries =>
                shiftSeries.EventSeries != null
                && shiftSeries.EventSeries.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                && shiftSeries.EventSeries.StartAtUtc < rangeEnd
            );
            var candidates = await query
                .OrderBy(shiftSeries => shiftSeries.EventSeriesId)
                .ThenBy(shiftSeries => shiftSeries.Id)
                .ToListAsync(cancellationToken);
            var recurrenceResults = candidates
                .Where(shiftSeries =>
                    shiftSeries.EventSeries is not null
                    && recurrenceExpander.ExpandWithin(shiftSeries.EventSeries, rangeStart, rangeEnd).Count > 0
                )
                .ToList();

            logger.LogDebug("Shift series query returned {ShiftSeriesCount} records.", recurrenceResults.Count);
            return await MapToShiftSeriesResponsesAsync(recurrenceResults, cancellationToken);
        }

        if (queryParams?.StartAtUtc.HasValue == true || queryParams?.EndAtUtc.HasValue == true)
        {
            var partialRangeStart = queryParams?.StartAtUtc;
            var partialRangeEnd = queryParams?.EndAtUtc;
            query = query.Where(shiftSeries =>
                shiftSeries.ShiftEntries.Any(entry =>
                    entry.Event != null
                    && (!partialRangeEnd.HasValue || entry.Event.StartAtUtc < partialRangeEnd.Value)
                    && (
                        !partialRangeStart.HasValue
                        || (entry.Event.EndAtUtc.HasValue && entry.Event.EndAtUtc.Value > partialRangeStart.Value)
                    )
                )
            );
        }

        var results = await query
            .OrderBy(shiftSeries => shiftSeries.EventSeriesId)
            .ThenBy(shiftSeries => shiftSeries.Id)
            .ToListAsync(cancellationToken);

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
            .Include(shiftSeries => shiftSeries.EventSeries)
            .Include(shiftSeries => shiftSeries.Users)
            .Where(shiftSeries => shiftSeries.Id == id)
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
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        logger.LogInformation(
            "Creating shift series for users {UserIds} starting at {StartAtUtc}.",
            string.Join(",", userIds),
            request.StartAtUtc
        );

        var eventSeries = ShiftEventMapper.ToEventSeries(request);
        var entity = new ShiftSeries
        {
            EventSeries = eventSeries,
            Users = userIds.Select(userId => new ShiftSeriesUser { UserId = userId }).ToList(),
        };

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
        var conflictCandidates = await SchedulingConflictParticipantProvider.GetParticipantsForShiftEntriesAsync(
            db,
            entity.ShiftEntries.Select(entry => entry.Id).ToList(),
            cancellationToken
        );
        await calendarConflictService.EnsureNoUnresolvedConflictsAsync(conflictCandidates, cancellationToken);
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
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var entity = await db
            .ShiftSeries.Include(shiftSeries => shiftSeries.EventSeries!)
                .ThenInclude(eventSeries => eventSeries.Events)
            .Include(shiftSeries => shiftSeries.Users)
            .Include(shiftSeries => shiftSeries.ShiftEntries)
                .ThenInclude(shiftEntry => shiftEntry.Event)
            .Include(shiftSeries => shiftSeries.ShiftEntries)
                .ThenInclude(shiftEntry => shiftEntry.Users)
            .Include(shiftSeries => shiftSeries.ShiftEntries)
                .ThenInclude(shiftEntry => shiftEntry.ShiftAssignmentEntries)
                    .ThenInclude(link => link.Users)
            .Include(shiftSeries => shiftSeries.ShiftEntries)
                .ThenInclude(shiftEntry => shiftEntry.ShiftAssignmentEntries)
                    .ThenInclude(link => link.AssignmentEntry)
                        .ThenInclude(entry => entry!.Event)
            .Include(shiftSeries => shiftSeries.ShiftEntries)
                .ThenInclude(shiftEntry => shiftEntry.ShiftAssignmentEntries)
                    .ThenInclude(link => link.ShiftAssignmentSeriesLink)
            .SingleOrDefaultAsync(shiftSeries => shiftSeries.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Shift series {ShiftSeriesId} was not found for update.", id);
            return null;
        }

        ShiftGuards.EnsureShiftEventSeriesType(entity.EventSeries!);
        var eventSeries = entity.EventSeries!;
        ShiftGuards.EnsureShiftSeriesIsDraft(eventSeries);
        var oldEventSeriesValues = ShiftSeriesUpdatePlanner.CaptureCopiedValues(eventSeries);
        var oldUserIds = entity.Users.Select(user => user.UserId).Distinct().Order().ToList();
        var recurrenceChanged = ShiftSeriesUpdatePlanner.HasRecurrenceChanged(eventSeries, request);
        var newUserIds = ShiftUserSync.GetDistinctUserIds(request.UserIds);

        ValidatePropagatedShiftEntryUsers(entity, oldUserIds, newUserIds);

        ShiftEventMapper.ApplyToEventSeries(eventSeries, request);

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
            ApplySeriesNonRecurrenceUpdatesToChildren(entity, oldEventSeriesValues, oldUserIds, newUserIds);
        }

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
        await calendarConflictService.InvalidateResolvedOverridesAsync(
            entity.ShiftEntries.Select(entry => entry.EventId).ToList(),
            cancellationToken: cancellationToken
        );
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Updated shift series {ShiftSeriesId}.", id);

        return await MapToShiftSeriesResponseAsync(entity, cancellationToken);
    }

    private void ApplySeriesNonRecurrenceUpdatesToChildren(
        ShiftSeries shiftSeries,
        EventSeriesCopiedValues oldCopiedValues,
        IReadOnlyCollection<Guid> oldUserIds,
        IReadOnlyCollection<Guid> newUserIds
    )
    {
        foreach (
            var shiftEntry in shiftSeries.ShiftEntries.Where(entry =>
                entry.Event?.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
            )
        )
        {
            ShiftSeriesUpdatePlanner.ApplyCopiedFieldUpdatesPreservingOverrides(
                shiftEntry.Event!,
                oldCopiedValues,
                shiftSeries.EventSeries!
            );

            if (ShiftUserSync.UserSetsEqual(shiftEntry.Users.Select(user => user.UserId), oldUserIds))
                ShiftUserSync.SyncEntryUsers(db, shiftEntry, newUserIds);
        }
    }

    public async Task<ShiftSeriesResponse?> PublishShiftSeriesAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Publishing shift series {ShiftSeriesId}.", id);

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );

        var entity = await db
            .ShiftSeries.Include(shiftSeries => shiftSeries.EventSeries!)
                .ThenInclude(eventSeries => eventSeries.Events)
            .Include(shiftSeries => shiftSeries.Users)
            .SingleOrDefaultAsync(shiftSeries => shiftSeries.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Shift series {ShiftSeriesId} was not found for publish.", id);
            return null;
        }

        ShiftGuards.EnsureShiftEventSeriesType(entity.EventSeries!);
        var eventSeries = entity.EventSeries!;
        calendarLifecycleService.PublishSeries(eventSeries, eventSeries.Events.ToList());

        await db.SaveChangesAsync(cancellationToken);

        var shiftEntryIds = await db
            .ShiftEntries.Where(shiftEntry => shiftEntry.ShiftSeriesId == id)
            .Select(shiftEntry => shiftEntry.Id)
            .ToListAsync(cancellationToken);
        var conflictCandidates = await SchedulingConflictParticipantProvider.GetParticipantsForShiftEntriesAsync(
            db,
            shiftEntryIds,
            cancellationToken
        );
        await calendarConflictService.EnsureNoUnresolvedConflictsAsync(conflictCandidates, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Published shift series {ShiftSeriesId}.", id);

        return await MapToShiftSeriesResponseAsync(entity, cancellationToken);
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
            .ShiftSeries.Include(shiftSeries => shiftSeries.EventSeries!)
                .ThenInclude(eventSeries => eventSeries.Events)
            .Include(shiftSeries => shiftSeries.Users)
            .SingleOrDefaultAsync(shiftSeries => shiftSeries.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Shift series {ShiftSeriesId} was not found for expire.", id);
            return null;
        }

        ShiftGuards.EnsureShiftEventSeriesType(entity.EventSeries!);
        var eventSeries = entity.EventSeries!;
        var cancelledAt = timeProvider.GetUtcNow();
        calendarLifecycleService.CancelSeries(
            eventSeries,
            eventSeries.Events.ToList(),
            cancelledAt,
            cancelledByUserId,
            request.CancellationReason
        );

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Expired shift series {ShiftSeriesId}.", id);

        return await MapToShiftSeriesResponseAsync(entity, cancellationToken);
    }

    public async Task<bool> DeleteShiftSeriesAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Deleting shift series {ShiftSeriesId}.", id);

        var entity = await db
            .ShiftSeries.Include(shiftSeries => shiftSeries.EventSeries)
            .Include(shiftSeries => shiftSeries.Users)
            .SingleOrDefaultAsync(shiftSeries => shiftSeries.Id == id, cancellationToken);
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

        var shiftEntries = await db
            .ShiftEntries.Include(shiftEntry => shiftEntry.Event)
            .Include(shiftEntry => shiftEntry.Users)
            .Where(shiftEntry => shiftEntry.ShiftSeriesId == entity.Id)
            .ToListAsync(cancellationToken);

        var draftChildEvents = childEvents
            .Where(eventEntity => eventEntity.StatusTypeCode == CalendarEventStatusTypeCodes.Draft)
            .ToList();
        var draftChildEventIds = draftChildEvents.Select(eventEntity => eventEntity.Id).ToHashSet();
        var draftShiftEntries = shiftEntries
            .Where(shiftEntry => draftChildEventIds.Contains(shiftEntry.EventId))
            .ToList();
        var draftShiftEntryIds = draftShiftEntries.Select(shiftEntry => shiftEntry.Id).ToHashSet();
        var draftAssignmentLinks = await db
            .ShiftAssignmentEntries.Include(link => link.Users)
            .Where(link => draftShiftEntryIds.Contains(link.ShiftEntryId))
            .ToListAsync(cancellationToken);
        var seriesLinks = await db
            .ShiftAssignmentSeriesLinks.Include(link => link.Users)
            .Include(link => link.EntryLinks)
                .ThenInclude(link => link.Users)
            .Where(link => link.ShiftSeriesId == entity.Id)
            .ToListAsync(cancellationToken);
        var retainedShiftEntries = shiftEntries.Except(draftShiftEntries).ToList();
        foreach (var retainedShiftEntry in retainedShiftEntries)
        {
            retainedShiftEntry.ShiftSeriesId = null;
        }

        foreach (var retainedChildEvent in childEvents.Except(draftChildEvents))
        {
            retainedChildEvent.EventSeriesId = null;
        }

        var retainedAssignmentLinks = seriesLinks
            .SelectMany(link => link.EntryLinks)
            .Where(link => !draftShiftEntryIds.Contains(link.ShiftEntryId))
            .ToList();
        var suppressedAssignmentLinks = retainedAssignmentLinks.Where(link => link.Users.Count == 0).ToList();
        foreach (var retainedAssignmentLink in retainedAssignmentLinks.Except(suppressedAssignmentLinks))
        {
            retainedAssignmentLink.ShiftAssignmentSeriesLinkId = null;
            retainedAssignmentLink.ShiftAssignmentSeriesLink = null;
            retainedAssignmentLink.IsException = false;
        }

        RemoveShiftAssignmentLinks(draftAssignmentLinks.Concat(suppressedAssignmentLinks).ToList());
        db.ShiftAssignmentSeriesLinkUsers.RemoveRange(seriesLinks.SelectMany(link => link.Users));
        db.ShiftAssignmentSeriesLinks.RemoveRange(seriesLinks);
        db.ShiftEntryUsers.RemoveRange(draftShiftEntries.SelectMany(shiftEntry => shiftEntry.Users));
        db.ShiftEntries.RemoveRange(draftShiftEntries);
        db.ShiftSeriesUsers.RemoveRange(entity.Users);
        db.ShiftSeries.Remove(entity);
        db.Events.RemoveRange(draftChildEvents);
        db.EventSeries.Remove(eventSeries);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted shift series {ShiftSeriesId}.", id);

        return true;
    }

    public async Task<IReadOnlyCollection<ShiftEntryResponse>> GetShiftEntriesAsync(
        ShiftEntryQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug(
            "Querying shift entries with ShiftSeriesId {ShiftSeriesId}, EventId {EventId}, UserId {UserId}, LocationId {LocationId}, StartAtUtc {StartAtUtc}, and EndAtUtc {EndAtUtc}.",
            queryParams?.ShiftSeriesId,
            queryParams?.EventId,
            queryParams?.UserId,
            queryParams?.LocationId,
            queryParams?.StartAtUtc,
            queryParams?.EndAtUtc
        );

        IQueryable<ShiftEntry> query = db
            .ShiftEntries.AsNoTracking()
            .Include(shiftEntry => shiftEntry.Event)
            .Include(shiftEntry => shiftEntry.Users)
            .Include(shiftEntry => shiftEntry.ShiftAssignmentEntries)
                .ThenInclude(link => link.Users)
            .Include(shiftEntry => shiftEntry.ShiftAssignmentEntries)
                .ThenInclude(link => link.AssignmentEntry)
                    .ThenInclude(entry => entry!.Event);

        if (queryParams?.ShiftSeriesId is int shiftSeriesId)
            query = query.Where(shiftEntry => shiftEntry.ShiftSeriesId == shiftSeriesId);

        if (queryParams?.EventId is int eventId)
            query = query.Where(shiftEntry => shiftEntry.EventId == eventId);

        if (queryParams?.UserId is Guid userId)
            query = query.Where(shiftEntry => shiftEntry.Users.Any(user => user.UserId == userId));

        if (queryParams?.LocationId is int locationId)
            query = query.Where(shiftEntry => shiftEntry.Event != null && shiftEntry.Event.LocationId == locationId);

        if (queryParams?.StartAtUtc is DateTimeOffset rangeStart)
        {
            query = query.Where(shiftEntry =>
                shiftEntry.Event != null
                && shiftEntry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                && (
                    shiftEntry.Event.EndAtUtc.HasValue
                        ? shiftEntry.Event.EndAtUtc.Value > rangeStart
                        : shiftEntry.Event.StartAtUtc >= rangeStart
                )
            );
        }

        if (queryParams?.EndAtUtc is DateTimeOffset rangeEnd)
        {
            query = query.Where(shiftEntry =>
                shiftEntry.Event != null
                && shiftEntry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                && shiftEntry.Event.StartAtUtc < rangeEnd
            );
        }

        var results = await query
            .OrderBy(shiftEntry => shiftEntry.EventId)
            .ThenBy(shiftEntry => shiftEntry.Id)
            .ToListAsync(cancellationToken);

        logger.LogDebug("Shift entry query returned {ShiftEntryCount} records.", results.Count);

        return results
            .Select(entry =>
                ShiftResponseMapper.ToShiftEntryResponse(
                    entry,
                    ShiftResponseMapper.ToAssignmentLinkResponses(entry.ShiftAssignmentEntries)
                )
            )
            .ToList();
    }

    public async Task<ShiftEntryResponse?> GetShiftEntryByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Retrieving shift entry {ShiftEntryId}.", id);

        var result = await db
            .ShiftEntries.AsNoTracking()
            .Include(shiftEntry => shiftEntry.Users)
            .Include(shiftEntry => shiftEntry.Event)
            .Include(shiftEntry => shiftEntry.ShiftAssignmentEntries)
                .ThenInclude(link => link.Users)
            .Include(shiftEntry => shiftEntry.ShiftAssignmentEntries)
                .ThenInclude(link => link.AssignmentEntry)
                    .ThenInclude(entry => entry!.Event)
            .Where(shiftEntry => shiftEntry.Id == id)
            .SingleOrDefaultAsync(cancellationToken);

        if (result is null)
            logger.LogInformation("Shift entry {ShiftEntryId} was not found.", id);

        return result is null
            ? null
            : ShiftResponseMapper.ToShiftEntryResponse(
                result,
                ShiftResponseMapper.ToAssignmentLinkResponses(result.ShiftAssignmentEntries)
            );
    }

    public async Task<ShiftEntryResponse> CreateShiftEntryAsync(
        ShiftEntryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var userIds = ShiftUserSync.GetDistinctUserIds(request.UserIds);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        logger.LogInformation(
            "Creating shift entry for shift series {ShiftSeriesId} and users {UserIds} starting at {StartAtUtc}.",
            request.ShiftSeriesId,
            string.Join(",", userIds),
            request.StartAtUtc
        );

        var shiftSeries = request.ShiftSeriesId.HasValue
            ? await GetValidatedShiftSeriesAsync(request.ShiftSeriesId.Value, cancellationToken)
            : null;

        var eventEntity = ShiftEventMapper.ToEvent(request, shiftSeries?.EventSeriesId);
        CalendarEventExceptionHelper.UpdateExceptionFlag(eventEntity);

        var entity = new ShiftEntry
        {
            ShiftSeries = shiftSeries,
            Event = eventEntity,
            Users = userIds.Select(userId => new ShiftEntryUser { UserId = userId }).ToList(),
        };

        db.ShiftEntries.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await shiftAssignmentService.ReplaceShiftEntryLinksAsync(
            entity.Id,
            request.AssignmentEntryLinks,
            cancellationToken
        );
        var conflictCandidates = await SchedulingConflictParticipantProvider.GetParticipantsForShiftEntriesAsync(
            db,
            [entity.Id],
            cancellationToken
        );
        await calendarConflictService.EnsureNoUnresolvedConflictsAsync(conflictCandidates, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Created shift entry {ShiftEntryId}.", entity.Id);

        return ShiftResponseMapper.ToShiftEntryResponse(entity);
    }

    public async Task<ShiftEntryResponse?> UpdateShiftEntryAsync(
        int id,
        ShiftEntryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Updating shift entry {ShiftEntryId}.", id);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var entity = await db
            .ShiftEntries.Include(shiftEntry => shiftEntry.Event)
            .Include(shiftEntry => shiftEntry.Users)
            .Include(shiftEntry => shiftEntry.ShiftAssignmentEntries)
                .ThenInclude(link => link.Users)
            .Include(shiftEntry => shiftEntry.ShiftAssignmentEntries)
                .ThenInclude(link => link.AssignmentEntry)
                    .ThenInclude(entry => entry!.Event)
            .Include(shiftEntry => shiftEntry.ShiftAssignmentEntries)
                .ThenInclude(link => link.ShiftAssignmentSeriesLink)
            .SingleOrDefaultAsync(shiftEntry => shiftEntry.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Shift entry {ShiftEntryId} was not found for update.", id);
            return null;
        }

        var shiftSeries = request.ShiftSeriesId.HasValue
            ? await GetValidatedShiftSeriesAsync(request.ShiftSeriesId.Value, cancellationToken)
            : null;

        ShiftGuards.EnsureShiftEventType(entity.Event!);
        ShiftGuards.EnsureShiftEntryIsDraft(entity.Event!);
        ShiftAssignmentGuards.EnsureShiftEntryUpdatePreservesLinks(
            entity,
            request.StartAtUtc,
            request.EndAtUtc,
            request.UserIds,
            request.ShiftSeriesId,
            request.AssignmentEntryLinks.Select(link => link.AssignmentEntryId).ToList()
        );
        ShiftEventMapper.ApplyToEvent(entity.Event!, request, shiftSeries?.EventSeriesId);
        CalendarEventExceptionHelper.UpdateExceptionFlag(entity.Event!);

        entity.ShiftSeriesId = request.ShiftSeriesId;
        ShiftUserSync.SyncEntryUsers(db, entity, request.UserIds);

        await db.SaveChangesAsync(cancellationToken);
        await shiftAssignmentService.ReplaceShiftEntryLinksAsync(
            entity.Id,
            request.AssignmentEntryLinks,
            cancellationToken
        );
        var conflictCandidates = await SchedulingConflictParticipantProvider.GetParticipantsForShiftEntriesAsync(
            db,
            [entity.Id],
            cancellationToken
        );
        await calendarConflictService.EnsureNoUnresolvedConflictsAsync(conflictCandidates, cancellationToken);
        await calendarConflictService.InvalidateResolvedOverridesAsync(
            [entity.EventId],
            cancellationToken: cancellationToken
        );
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Updated shift entry {ShiftEntryId}.", id);

        return ShiftResponseMapper.ToShiftEntryResponse(entity);
    }

    public async Task<ShiftEntryResponse?> PublishShiftEntryAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Publishing shift entry {ShiftEntryId}.", id);

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );

        var entity = await db
            .ShiftEntries.Include(shiftEntry => shiftEntry.Event)
            .Include(shiftEntry => shiftEntry.Users)
            .SingleOrDefaultAsync(shiftEntry => shiftEntry.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Shift entry {ShiftEntryId} was not found for publish.", id);
            return null;
        }

        ShiftGuards.EnsureShiftEventType(entity.Event!);
        calendarLifecycleService.Publish(entity.Event!);
        await db.SaveChangesAsync(cancellationToken);

        var conflictCandidates = await SchedulingConflictParticipantProvider.GetParticipantsForShiftEntriesAsync(
            db,
            [entity.Id],
            cancellationToken
        );
        await calendarConflictService.EnsureNoUnresolvedConflictsAsync(conflictCandidates, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Published shift entry {ShiftEntryId}.", id);

        return ShiftResponseMapper.ToShiftEntryResponse(entity);
    }

    public async Task<ShiftEntryResponse?> ExpireShiftEntryAsync(
        int id,
        ExpireShiftRequest request,
        Guid? cancelledByUserId = null,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Expiring shift entry {ShiftEntryId}.", id);

        var entity = await db
            .ShiftEntries.Include(shiftEntry => shiftEntry.Event)
            .Include(shiftEntry => shiftEntry.Users)
            .SingleOrDefaultAsync(shiftEntry => shiftEntry.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Shift entry {ShiftEntryId} was not found for expire.", id);
            return null;
        }

        ShiftGuards.EnsureShiftEventType(entity.Event!);
        calendarLifecycleService.Cancel(
            entity.Event!,
            timeProvider.GetUtcNow(),
            cancelledByUserId,
            request.CancellationReason
        );
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Expired shift entry {ShiftEntryId}.", id);

        return ShiftResponseMapper.ToShiftEntryResponse(entity);
    }

    public async Task<bool> DeleteShiftEntryAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Deleting shift entry {ShiftEntryId}.", id);

        var entity = await db
            .ShiftEntries.Include(shiftEntry => shiftEntry.Event)
            .Include(shiftEntry => shiftEntry.Users)
            .SingleOrDefaultAsync(shiftEntry => shiftEntry.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Shift entry {ShiftEntryId} was not found for delete.", id);
            return false;
        }

        var eventEntity = entity.Event!;
        ShiftGuards.EnsureShiftEventType(eventEntity);

        if (!calendarLifecycleService.CanDelete(eventEntity))
            throw new InvalidOperationException("Shift entry can only be deleted while in draft status.");

        var assignmentLinks = await db
            .ShiftAssignmentEntries.Include(link => link.Users)
            .Where(link => link.ShiftEntryId == entity.Id)
            .ToListAsync(cancellationToken);
        RemoveShiftAssignmentLinks(assignmentLinks);
        db.ShiftEntryUsers.RemoveRange(entity.Users);
        db.ShiftEntries.Remove(entity);
        db.Events.Remove(eventEntity);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted shift entry {ShiftEntryId}.", id);

        return true;
    }

    private async Task<IReadOnlyCollection<ShiftSeriesResponse>> MapToShiftSeriesResponsesAsync(
        IReadOnlyCollection<ShiftSeries> shiftSeries,
        CancellationToken cancellationToken
    )
    {
        var entryIds = await LoadShiftSeriesEntryIdsAsync(shiftSeries, cancellationToken);

        return shiftSeries
            .Select(series =>
                ShiftResponseMapper.ToShiftSeriesResponse(
                    series,
                    series.EventSeries,
                    entryIds.GetValueOrDefault(series.Id, [])
                )
            )
            .ToList();
    }

    private async Task<ShiftSeriesResponse> MapToShiftSeriesResponseAsync(
        ShiftSeries shiftSeries,
        CancellationToken cancellationToken
    )
    {
        var entryIds = await LoadShiftSeriesEntryIdsAsync([shiftSeries], cancellationToken);
        var ids = entryIds.GetValueOrDefault(shiftSeries.Id, []);

        return ShiftResponseMapper.ToShiftSeriesResponse(shiftSeries, shiftSeries.EventSeries, ids);
    }

    private async Task<Dictionary<int, List<ShiftSeriesEntryIds>>> LoadShiftSeriesEntryIdsAsync(
        IReadOnlyCollection<ShiftSeries> shiftSeries,
        CancellationToken cancellationToken
    )
    {
        if (shiftSeries.Count == 0)
            return [];

        var eventSeriesIdsByShiftSeriesId = shiftSeries.ToDictionary(
            series => series.Id,
            series => series.EventSeriesId
        );
        var shiftSeriesIds = eventSeriesIdsByShiftSeriesId.Keys.ToList();
        var eventSeriesIds = eventSeriesIdsByShiftSeriesId.Values.ToList();

        var ids = await db
            .ShiftEntries.AsNoTracking()
            .Where(entry => entry.ShiftSeriesId.HasValue && shiftSeriesIds.Contains(entry.ShiftSeriesId.Value))
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

    private async Task<ShiftSeries> GetValidatedShiftSeriesAsync(int shiftSeriesId, CancellationToken cancellationToken)
    {
        var shiftSeries = await db
            .ShiftSeries.Include(series => series.EventSeries)
            .SingleOrDefaultAsync(series => series.Id == shiftSeriesId, cancellationToken);

        if (shiftSeries is null)
            throw new KeyNotFoundException($"Shift series {shiftSeriesId} not found.");

        ShiftGuards.EnsureShiftEventSeriesType(shiftSeries.EventSeries!);
        return shiftSeries;
    }

    private static void ValidatePropagatedShiftEntryUsers(
        ShiftSeries shiftSeries,
        IReadOnlyCollection<Guid> oldSeriesUserIds,
        IReadOnlyCollection<Guid> newSeriesUserIds
    )
    {
        foreach (
            var shiftEntry in shiftSeries.ShiftEntries.Where(entry =>
                entry.Event?.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                && ShiftUserSync.UserSetsEqual(entry.Users.Select(user => user.UserId), oldSeriesUserIds)
            )
        )
            ShiftAssignmentGuards.EnsureShiftEntryUpdatePreservesLinks(
                shiftEntry,
                shiftEntry.Event!.StartAtUtc,
                shiftEntry.Event.EndAtUtc,
                newSeriesUserIds,
                shiftSeries.Id
            );
    }

    private void RemoveShiftAssignmentLinks(IReadOnlyCollection<ShiftAssignmentEntry> links)
    {
        db.ShiftAssignmentEntryUsers.RemoveRange(links.SelectMany(link => link.Users));
        db.ShiftAssignmentEntries.RemoveRange(links);
    }
}
