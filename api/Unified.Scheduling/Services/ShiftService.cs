using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Calendar.Services;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public sealed class ShiftService(
    ILogger<ShiftService> logger,
    UnifiedDbContext db,
    IEventSeriesMaterializationService eventSeriesMaterializationService,
    ShiftSeriesMaterializationHandler shiftSeriesMaterializationHandler,
    ICalendarDateTimeService calendarDateTimeService,
    ICalendarLifecycleService calendarLifecycleService
) : IShiftService
{
    private static readonly EventSeriesMaterializationOptions ShiftMaterializationOptions = new()
    {
        ValidationOptions = new RecurrenceValidationOptions
        {
            MaximumDuration = TimeSpan.FromDays(365),
            MaximumOccurrences = 400,
            RequireBoundedRule = true,
        },
    };

    public async Task<IReadOnlyCollection<ShiftSeriesResponse>> GetShiftSeriesAsync(
        ShiftSeriesQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug(
            "Querying shift series with EventSeriesId {EventSeriesId} and UserId {UserId}.",
            queryParams?.EventSeriesId,
            queryParams?.UserId
        );

        IQueryable<ShiftSeries> query = db.ShiftSeries.AsNoTracking().Include(shiftSeries => shiftSeries.Users);

        if (queryParams?.EventSeriesId is int eventSeriesId)
            query = query.Where(shiftSeries => shiftSeries.EventSeriesId == eventSeriesId);

        if (queryParams?.UserId is Guid userId)
            query = query.Where(shiftSeries => shiftSeries.Users.Any(user => user.UserId == userId));

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
        ValidateRequestedStatusIsDraft(request.StatusTypeCode, "Shift series");

        var userIds = GetDistinctUserIds(request.UserIds);
        logger.LogInformation(
            "Creating shift series for users {UserIds} starting at {StartAtUtc}.",
            string.Join(",", userIds),
            request.StartAtUtc
        );

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var eventSeries = MapToEventSeries(request);
        db.EventSeries.Add(eventSeries);
        await db.SaveChangesAsync(cancellationToken);

        var entity = new ShiftSeries
        {
            EventSeriesId = eventSeries.Id,
            Users = userIds.Select(userId => new ShiftSeriesUser { UserId = userId }).ToList(),
        };

        db.ShiftSeries.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        var materializationResult = await eventSeriesMaterializationService.MaterializeAsync(
            eventSeries,
            ShiftMaterializationOptions,
            shiftSeriesMaterializationHandler,
            cancellationToken
        );

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Created shift series {ShiftSeriesId}.", entity.Id);

        return await MapToShiftSeriesResponseAsync(entity, cancellationToken, materializationResult);
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
            .SingleOrDefaultAsync(shiftSeries => shiftSeries.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Shift series {ShiftSeriesId} was not found for update.", id);
            return null;
        }

        ValidateShiftEventSeriesType(entity.EventSeries!);
        var eventSeries = entity.EventSeries!;
        ValidateShiftSeriesIsDraft(eventSeries);

        UpdateEventSeries(eventSeries, request);

        SyncShiftSeriesUsers(entity, request.UserIds);
        await db.SaveChangesAsync(cancellationToken);

        var materializationResult = await eventSeriesMaterializationService.RecreateAsync(
            eventSeries,
            ShiftMaterializationOptions,
            shiftSeriesMaterializationHandler,
            cancellationToken
        );

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Updated shift series {ShiftSeriesId}.", id);

        return await MapToShiftSeriesResponseAsync(entity, cancellationToken, materializationResult);
    }

    public async Task<ShiftSeriesResponse?> PublishShiftSeriesAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Publishing shift series {ShiftSeriesId}.", id);

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

        ValidateShiftEventSeriesType(entity.EventSeries!);
        var eventSeries = entity.EventSeries!;
        calendarLifecycleService.PublishSeries(eventSeries, eventSeries.Events.ToList());

        await db.SaveChangesAsync(cancellationToken);

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

        ValidateShiftEventSeriesType(entity.EventSeries!);
        var eventSeries = entity.EventSeries!;
        var cancelledAt = DateTimeOffset.UtcNow;
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

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

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
        ValidateShiftEventSeriesType(eventSeries);

        var childEvents = await db
            .Events.Where(eventEntity =>
                eventEntity.EventSeriesId == eventSeries.Id
                && eventEntity.EventTypeCode == SchedulingConstants.ShiftEventTypeCode
                && eventEntity.SourceModule == SchedulingConstants.SourceModule
            )
            .ToListAsync(cancellationToken);

        if (!calendarLifecycleService.CanDeleteSeries(eventSeries, childEvents))
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
        var retainedShiftEntries = shiftEntries.Except(draftShiftEntries).ToList();
        foreach (var retainedShiftEntry in retainedShiftEntries)
        {
            retainedShiftEntry.ShiftSeriesId = null;
        }

        foreach (var retainedChildEvent in childEvents.Except(draftChildEvents))
        {
            retainedChildEvent.EventSeriesId = null;
        }

        db.ShiftEntryUsers.RemoveRange(draftShiftEntries.SelectMany(shiftEntry => shiftEntry.Users));
        db.ShiftEntries.RemoveRange(draftShiftEntries);
        db.ShiftSeriesUsers.RemoveRange(entity.Users);
        db.ShiftSeries.Remove(entity);
        db.Events.RemoveRange(draftChildEvents);
        db.EventSeries.Remove(eventSeries);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Deleted shift series {ShiftSeriesId}.", id);

        return true;
    }

    public async Task<IReadOnlyCollection<ShiftEntryResponse>> GetShiftEntriesAsync(
        ShiftEntryQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug(
            "Querying shift entries with ShiftSeriesId {ShiftSeriesId}, EventId {EventId}, and UserId {UserId}.",
            queryParams?.ShiftSeriesId,
            queryParams?.EventId,
            queryParams?.UserId
        );

        IQueryable<ShiftEntry> query = db.ShiftEntries.AsNoTracking().Include(shiftEntry => shiftEntry.Users);

        if (queryParams?.ShiftSeriesId is int shiftSeriesId)
            query = query.Where(shiftEntry => shiftEntry.ShiftSeriesId == shiftSeriesId);

        if (queryParams?.EventId is int eventId)
            query = query.Where(shiftEntry => shiftEntry.EventId == eventId);

        if (queryParams?.UserId is Guid userId)
            query = query.Where(shiftEntry => shiftEntry.Users.Any(user => user.UserId == userId));

        var results = await query
            .OrderBy(shiftEntry => shiftEntry.EventId)
            .ThenBy(shiftEntry => shiftEntry.Id)
            .ToListAsync(cancellationToken);

        logger.LogDebug("Shift entry query returned {ShiftEntryCount} records.", results.Count);

        return results.Select(MapToShiftEntryResponse).ToList();
    }

    public async Task<ShiftEntryResponse?> GetShiftEntryByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Retrieving shift entry {ShiftEntryId}.", id);

        var result = await db
            .ShiftEntries.AsNoTracking()
            .Include(shiftEntry => shiftEntry.Users)
            .Where(shiftEntry => shiftEntry.Id == id)
            .SingleOrDefaultAsync(cancellationToken);

        if (result is null)
            logger.LogInformation("Shift entry {ShiftEntryId} was not found.", id);

        return result is null ? null : MapToShiftEntryResponse(result);
    }

    public async Task<ShiftEntryResponse> CreateShiftEntryAsync(
        ShiftEntryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ValidateRequestedStatusIsDraft(request.StatusTypeCode, "Shift entry");

        var userIds = GetDistinctUserIds(request.UserIds);
        logger.LogInformation(
            "Creating shift entry for shift series {ShiftSeriesId} and users {UserIds} starting at {StartAtUtc}.",
            request.ShiftSeriesId,
            string.Join(",", userIds),
            request.StartAtUtc
        );

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var shiftSeries = request.ShiftSeriesId.HasValue
            ? await GetValidatedShiftSeriesAsync(request.ShiftSeriesId.Value, cancellationToken)
            : null;

        var eventEntity = MapToEvent(request, shiftSeries?.EventSeriesId);
        CalendarEventExceptionHelper.UpdateExceptionFlag(eventEntity);
        db.Events.Add(eventEntity);
        await db.SaveChangesAsync(cancellationToken);

        var entity = new ShiftEntry
        {
            ShiftSeriesId = request.ShiftSeriesId,
            EventId = eventEntity.Id,
            Users = userIds.Select(userId => new ShiftEntryUser { UserId = userId }).ToList(),
        };

        db.ShiftEntries.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Created shift entry {ShiftEntryId}.", entity.Id);

        return MapToShiftEntryResponse(entity);
    }

    public async Task<ShiftEntryResponse?> UpdateShiftEntryAsync(
        int id,
        ShiftEntryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Updating shift entry {ShiftEntryId}.", id);

        var entity = await db
            .ShiftEntries.Include(shiftEntry => shiftEntry.Event)
            .Include(shiftEntry => shiftEntry.Users)
            .SingleOrDefaultAsync(shiftEntry => shiftEntry.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Shift entry {ShiftEntryId} was not found for update.", id);
            return null;
        }

        var shiftSeries = request.ShiftSeriesId.HasValue
            ? await GetValidatedShiftSeriesAsync(request.ShiftSeriesId.Value, cancellationToken)
            : null;

        ValidateShiftEventType(entity.Event!);
        UpdateEvent(entity.Event!, request, shiftSeries?.EventSeriesId);
        CalendarEventExceptionHelper.UpdateExceptionFlag(entity.Event!);

        entity.ShiftSeriesId = request.ShiftSeriesId;
        SyncShiftEntryUsers(entity, request.UserIds);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated shift entry {ShiftEntryId}.", id);

        return MapToShiftEntryResponse(entity);
    }

    public async Task<ShiftEntryResponse?> PublishShiftEntryAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Publishing shift entry {ShiftEntryId}.", id);

        var entity = await db
            .ShiftEntries.Include(shiftEntry => shiftEntry.Event)
            .Include(shiftEntry => shiftEntry.Users)
            .SingleOrDefaultAsync(shiftEntry => shiftEntry.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Shift entry {ShiftEntryId} was not found for publish.", id);
            return null;
        }

        ValidateShiftEventType(entity.Event!);
        calendarLifecycleService.Publish(entity.Event!);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Published shift entry {ShiftEntryId}.", id);

        return MapToShiftEntryResponse(entity);
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

        ValidateShiftEventType(entity.Event!);
        calendarLifecycleService.Cancel(
            entity.Event!,
            DateTimeOffset.UtcNow,
            cancelledByUserId,
            request.CancellationReason
        );
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Expired shift entry {ShiftEntryId}.", id);

        return MapToShiftEntryResponse(entity);
    }

    public async Task<bool> DeleteShiftEntryAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Deleting shift entry {ShiftEntryId}.", id);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

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
        ValidateShiftEventType(eventEntity);

        if (!calendarLifecycleService.CanDelete(eventEntity))
            throw new InvalidOperationException("Shift entry can only be deleted while in draft status.");

        db.ShiftEntryUsers.RemoveRange(entity.Users);
        db.ShiftEntries.Remove(entity);
        db.Events.Remove(eventEntity);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Deleted shift entry {ShiftEntryId}.", id);

        return true;
    }

    public async Task<SchedulingCalendarDataResponse> GetSchedulingCalendarDataAsync(
        SchedulingCalendarRequest request,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug(
            "Querying scheduling calendar shift events for local date range {StartDate} to {EndDate}, timezone {TimeZoneId}, location {LocationId}, and users {UserIds}.",
            request.StartDate,
            request.EndDate,
            request.TimeZoneId,
            request.LocationId,
            request.UserIds is null ? null : string.Join(",", request.UserIds)
        );

        var locationTimeZoneId = await GetLocationTimeZoneIdAsync(request.LocationId, cancellationToken);
        var timeZone = calendarDateTimeService.ResolveTimeZone(request.TimeZoneId, locationTimeZoneId);
        var utcRange = calendarDateTimeService.ConvertInclusiveLocalDateRangeToUtcRange(
            request.StartDate,
            request.EndDate,
            timeZone
        );

        IQueryable<ShiftEntry> query = db
            .ShiftEntries.AsNoTracking()
            .Include(shiftEntry => shiftEntry.Event)
            .Include(shiftEntry => shiftEntry.Users)
            .Where(shiftEntry => shiftEntry.Event != null)
            .Where(shiftEntry => shiftEntry.Event!.SourceModule == SchedulingConstants.SourceModule)
            .Where(shiftEntry => shiftEntry.Event!.EventTypeCode == SchedulingConstants.ShiftEventTypeCode)
            .Where(shiftEntry => shiftEntry.Event!.StartAtUtc < utcRange.EndAtUtc)
            .Where(shiftEntry =>
                shiftEntry.Event!.EndAtUtc == null
                    ? shiftEntry.Event.StartAtUtc >= utcRange.StartAtUtc
                        && shiftEntry.Event.StartAtUtc < utcRange.EndAtUtc
                    : shiftEntry.Event.EndAtUtc > utcRange.StartAtUtc
            );

        if (request.LocationId.HasValue)
            query = query.Where(shiftEntry =>
                shiftEntry.Event!.LocationId == null || shiftEntry.Event.LocationId == request.LocationId.Value
            );

        if (request.UserIds is { Count: > 0 })
            query = query.Where(shiftEntry => shiftEntry.Users.Any(user => request.UserIds.Contains(user.UserId)));

        var shiftEntries = await query
            .OrderBy(shiftEntry => shiftEntry.Event!.StartAtUtc)
            .ThenBy(shiftEntry => shiftEntry.Id)
            .ToListAsync(cancellationToken);

        var response = new SchedulingCalendarDataResponse
        {
            Events = shiftEntries.Select(MapToSchedulingCalendarShiftEventResponse).ToList(),
        };

        logger.LogDebug(
            "Scheduling calendar shift event query returned {ShiftEventCount} events.",
            response.Events.Count
        );

        return response;
    }

    private async Task<string?> GetLocationTimeZoneIdAsync(int? locationId, CancellationToken cancellationToken)
    {
        if (!locationId.HasValue)
            return null;

        return await db
            .Locations.AsNoTracking()
            .Where(location => location.Id == locationId.Value)
            .Select(location => location.Timezone)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static SchedulingCalendarShiftEventResponse MapToSchedulingCalendarShiftEventResponse(ShiftEntry shiftEntry)
    {
        var eventEntity = shiftEntry.Event!;
        var userIds = shiftEntry.Users.Select(user => user.UserId).Distinct().ToList();

        return new SchedulingCalendarShiftEventResponse
        {
            Id = $"scheduling.shift-entry.{shiftEntry.Id}",
            ShiftEntryId = shiftEntry.Id,
            ShiftSeriesId = shiftEntry.ShiftSeriesId,
            EventId = shiftEntry.EventId,
            UserIds = userIds,
            Type = "scheduling.shift",
            SourceModule = SchedulingConstants.SourceModule,
            Title = eventEntity.Title,
            Description = eventEntity.Description,
            Notes = eventEntity.Notes,
            Color = eventEntity.Color,
            Start = eventEntity.StartAtUtc,
            End = eventEntity.EndAtUtc,
            SeriesStartAtUtc = eventEntity.SeriesStartAtUtc,
            SeriesEndAtUtc = eventEntity.SeriesEndAtUtc,
            TimeZoneId = eventEntity.TimeZoneId,
            AllDay = eventEntity.AllDay,
            IsException = eventEntity.IsException,
            EventTypeCode = SchedulingConstants.ShiftEventTypeCode,
            StatusTypeCode = eventEntity.StatusTypeCode,
            CancelledAt = eventEntity.CancelledAt,
            CancelledByUserId = eventEntity.CancelledByUserId,
            CancellationReason = eventEntity.CancellationReason,
            LocationId = eventEntity.LocationId,
            ResourceIds = userIds.Select(userId => userId.ToString()).ToList(),
        };
    }

    private async Task<IReadOnlyCollection<ShiftSeriesResponse>> MapToShiftSeriesResponsesAsync(
        IReadOnlyCollection<ShiftSeries> shiftSeries,
        CancellationToken cancellationToken
    )
    {
        var entryIds = await LoadShiftSeriesEntryIdsAsync(shiftSeries, cancellationToken);

        return shiftSeries
            .Select(series => MapToShiftSeriesResponse(series, entryIds.GetValueOrDefault(series.Id, [])))
            .ToList();
    }

    private async Task<ShiftSeriesResponse> MapToShiftSeriesResponseAsync(
        ShiftSeries shiftSeries,
        CancellationToken cancellationToken,
        EventSeriesMaterializationResult? materializationResult = null
    )
    {
        var entryIds = await LoadShiftSeriesEntryIdsAsync([shiftSeries], cancellationToken);
        var ids = entryIds.GetValueOrDefault(shiftSeries.Id, []);

        if (materializationResult is not null)
        {
            var currentMaterializedEventIds = materializationResult
                .CreatedEventIds.Concat(materializationResult.UpdatedEventIds)
                .ToHashSet();
            if (currentMaterializedEventIds.Count > 0)
                ids = ids.OrderByDescending(entry => currentMaterializedEventIds.Contains(entry.EventId))
                    .ThenBy(entry => entry.ShiftEntryId)
                    .ToList();
        }

        return MapToShiftSeriesResponse(shiftSeries, ids);
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
            .Include(entry => entry.Event)
            .Where(entry => entry.ShiftSeriesId.HasValue && shiftSeriesIds.Contains(entry.ShiftSeriesId.Value))
            .Where(entry =>
                entry.Event != null
                && entry.Event.EventSeriesId.HasValue
                && eventSeriesIds.Contains(entry.Event.EventSeriesId.Value)
            )
            .OrderBy(entry => entry.Id)
            .Select(entry => new ShiftSeriesEntryIds(entry.ShiftSeriesId!.Value, entry.Id, entry.EventId))
            .ToListAsync(cancellationToken);

        return ids.GroupBy(entry => entry.ShiftSeriesId).ToDictionary(group => group.Key, group => group.ToList());
    }

    private static ShiftSeriesResponse MapToShiftSeriesResponse(
        ShiftSeries shiftSeries,
        IReadOnlyCollection<ShiftSeriesEntryIds> entryIds
    ) =>
        new()
        {
            Id = shiftSeries.Id,
            EventSeriesId = shiftSeries.EventSeriesId,
            UserIds = shiftSeries.Users.Select(user => user.UserId).Distinct().ToList(),
            EventIds = entryIds.Select(entry => entry.EventId).ToList(),
            ShiftEntryIds = entryIds.Select(entry => entry.ShiftEntryId).ToList(),
        };

    private sealed record ShiftSeriesEntryIds(int ShiftSeriesId, int ShiftEntryId, int EventId);

    private static ShiftEntryResponse MapToShiftEntryResponse(ShiftEntry shiftEntry)
    {
        return new ShiftEntryResponse
        {
            Id = shiftEntry.Id,
            ShiftSeriesId = shiftEntry.ShiftSeriesId,
            EventId = shiftEntry.EventId,
            UserIds = shiftEntry.Users.Select(user => user.UserId).Distinct().ToList(),
        };
    }

    private void SyncShiftSeriesUsers(ShiftSeries shiftSeries, IReadOnlyCollection<Guid> userIds)
    {
        var requestedUserIds = GetDistinctUserIds(userIds);
        var usersToRemove = shiftSeries.Users.Where(user => !requestedUserIds.Contains(user.UserId)).ToList();
        db.ShiftSeriesUsers.RemoveRange(usersToRemove);
        foreach (var user in usersToRemove)
            shiftSeries.Users.Remove(user);

        var existingUserIds = shiftSeries.Users.Select(user => user.UserId).ToHashSet();
        var usersToAdd = requestedUserIds
            .Where(userId => !existingUserIds.Contains(userId))
            .Select(userId => new ShiftSeriesUser { ShiftSeriesId = shiftSeries.Id, UserId = userId })
            .ToList();
        foreach (var user in usersToAdd)
            shiftSeries.Users.Add(user);
    }

    private void SyncShiftEntryUsers(ShiftEntry shiftEntry, IReadOnlyCollection<Guid> userIds)
    {
        var requestedUserIds = GetDistinctUserIds(userIds);
        var usersToRemove = shiftEntry.Users.Where(user => !requestedUserIds.Contains(user.UserId)).ToList();
        db.ShiftEntryUsers.RemoveRange(usersToRemove);
        foreach (var user in usersToRemove)
            shiftEntry.Users.Remove(user);

        var existingUserIds = shiftEntry.Users.Select(user => user.UserId).ToHashSet();
        var usersToAdd = requestedUserIds
            .Where(userId => !existingUserIds.Contains(userId))
            .Select(userId => new ShiftEntryUser { ShiftEntryId = shiftEntry.Id, UserId = userId })
            .ToList();
        foreach (var user in usersToAdd)
            shiftEntry.Users.Add(user);
    }

    private static IReadOnlyCollection<Guid> GetDistinctUserIds(IReadOnlyCollection<Guid> userIds)
    {
        return userIds.Distinct().ToList();
    }

    private static EventSeries MapToEventSeries(ShiftSeriesRequest request) =>
        new()
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Notes = request.Notes?.Trim(),
            Color = request.Color?.Trim(),
            RecurrenceRule = request.RecurrenceRule,
            TimeZoneId = request.TimeZoneId?.Trim(),
            StartAtUtc = request.StartAtUtc,
            EndAtUtc = request.EndAtUtc,
            AllDay = request.AllDay,
            EventTypeCode = SchedulingConstants.ShiftEventTypeCode,
            StatusTypeCode = NormalizeStatusTypeCode(request.StatusTypeCode),
            LocationId = request.LocationId,
        };

    private static void UpdateEventSeries(EventSeries eventSeries, ShiftSeriesRequest request)
    {
        eventSeries.Title = request.Title.Trim();
        eventSeries.Description = request.Description?.Trim();
        eventSeries.Notes = request.Notes?.Trim();
        eventSeries.Color = request.Color?.Trim();
        eventSeries.RecurrenceRule = request.RecurrenceRule;
        eventSeries.TimeZoneId = request.TimeZoneId?.Trim();
        eventSeries.StartAtUtc = request.StartAtUtc;
        eventSeries.EndAtUtc = request.EndAtUtc;
        eventSeries.AllDay = request.AllDay;
        eventSeries.LocationId = request.LocationId;
    }

    private async Task<ShiftSeries> GetValidatedShiftSeriesAsync(int shiftSeriesId, CancellationToken cancellationToken)
    {
        var shiftSeries = await db
            .ShiftSeries.Include(series => series.EventSeries)
            .SingleOrDefaultAsync(series => series.Id == shiftSeriesId, cancellationToken);

        if (shiftSeries is null)
            throw new KeyNotFoundException($"Shift series {shiftSeriesId} not found.");

        ValidateShiftEventSeriesType(shiftSeries.EventSeries!);
        return shiftSeries;
    }

    private static Event MapToEvent(ShiftEntryRequest request, int? eventSeriesId) =>
        new()
        {
            EventSeriesId = eventSeriesId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Notes = request.Notes?.Trim(),
            Color = request.Color?.Trim(),
            StartAtUtc = request.StartAtUtc,
            EndAtUtc = request.EndAtUtc,
            SeriesStartAtUtc = request.SeriesStartAtUtc,
            SeriesEndAtUtc = request.SeriesEndAtUtc,
            TimeZoneId = request.TimeZoneId?.Trim(),
            AllDay = request.AllDay,
            IsException = request.IsException,
            EventTypeCode = SchedulingConstants.ShiftEventTypeCode,
            StatusTypeCode = NormalizeStatusTypeCode(request.StatusTypeCode),
            SourceModule = SchedulingConstants.SourceModule,
            LocationId = request.LocationId,
        };

    private static void UpdateEvent(Event eventEntity, ShiftEntryRequest request, int? eventSeriesId)
    {
        eventEntity.EventSeriesId = eventSeriesId;
        eventEntity.Title = request.Title.Trim();
        eventEntity.Description = request.Description?.Trim();
        eventEntity.Notes = request.Notes?.Trim();
        eventEntity.Color = request.Color?.Trim();
        eventEntity.StartAtUtc = request.StartAtUtc;
        eventEntity.EndAtUtc = request.EndAtUtc;
        eventEntity.TimeZoneId = request.TimeZoneId?.Trim();
        eventEntity.AllDay = request.AllDay;
        eventEntity.SourceModule = SchedulingConstants.SourceModule;
        eventEntity.LocationId = request.LocationId;
    }

    private static string NormalizeStatusTypeCode(string? statusTypeCode)
    {
        return string.IsNullOrWhiteSpace(statusTypeCode) ? CalendarEventStatusTypeCodes.Draft : statusTypeCode.Trim();
    }

    private static void ValidateRequestedStatusIsDraft(string? statusTypeCode, string entityName)
    {
        var normalizedStatusTypeCode = NormalizeStatusTypeCode(statusTypeCode);
        if (normalizedStatusTypeCode != CalendarEventStatusTypeCodes.Draft)
            throw new InvalidOperationException($"{entityName} must be created in draft status.");
    }

    private static void ValidateShiftSeriesIsDraft(EventSeries eventSeries)
    {
        if (eventSeries.StatusTypeCode != CalendarEventStatusTypeCodes.Draft)
            throw new InvalidOperationException("Shift series must be in draft status to allow edits.");
    }

    private static void ValidateShiftEventSeriesType(EventSeries eventSeries)
    {
        if (eventSeries.EventTypeCode != SchedulingConstants.ShiftEventTypeCode)
            throw new InvalidOperationException($"Event series {eventSeries.Id} is not a shift event series.");
    }

    private static void ValidateShiftEventType(Event eventEntity)
    {
        if (eventEntity.EventTypeCode != SchedulingConstants.ShiftEventTypeCode)
            throw new InvalidOperationException($"Event {eventEntity.Id} is not a shift event.");

        if (eventEntity.SourceModule != SchedulingConstants.SourceModule)
            throw new InvalidOperationException($"Event {eventEntity.Id} is not owned by Scheduling.");
    }
}
