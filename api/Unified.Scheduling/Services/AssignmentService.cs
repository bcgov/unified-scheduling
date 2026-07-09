using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Calendar.Services;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public sealed class AssignmentService(
    ILogger<AssignmentService> logger,
    UnifiedDbContext db,
    IEventSeriesMaterializationService eventSeriesMaterializationService,
    AssignmentSeriesMaterializationHandler assignmentSeriesMaterializationHandler,
    ICalendarLifecycleService calendarLifecycleService
) : IAssignmentService
{
    private static readonly EventSeriesMaterializationOptions AssignmentMaterializationOptions = new()
    {
        ValidationOptions = new RecurrenceValidationOptions
        {
            MaximumDuration = TimeSpan.FromDays(365),
            MaximumOccurrences = 400,
            RequireBoundedRule = true,
        },
    };

    public async Task<IReadOnlyCollection<AssignmentSeriesResponse>> GetAssignmentSeriesAsync(
        AssignmentSeriesQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    )
    {
        IQueryable<AssignmentSeries> query = db
            .AssignmentSeries.AsNoTracking()
            .Include(series => series.EventSeries)
            .Include(series => series.AssignmentCategoryType)
            .Include(series => series.AssignmentSubCategoryType)
            .Include(series => series.AssignmentType);

        if (queryParams?.EventSeriesId is int eventSeriesId)
            query = query.Where(series => series.EventSeriesId == eventSeriesId);

        if (queryParams?.AssignmentTypeId is int assignmentTypeId)
            query = query.Where(series => series.AssignmentTypeId == assignmentTypeId);
        if (queryParams?.LocationId is int locationId)
            query = query.Where(series => series.EventSeries != null && series.EventSeries.LocationId == locationId);
        if (!string.IsNullOrWhiteSpace(queryParams?.StatusTypeCode))
        {
            var statusTypeCode = queryParams.StatusTypeCode.Trim();
            query = query.Where(series =>
                series.EventSeries != null && series.EventSeries.StatusTypeCode == statusTypeCode
            );
        }
        if (queryParams?.StartAtUtc.HasValue == true || queryParams?.EndAtUtc.HasValue == true)
        {
            var rangeStart = queryParams?.StartAtUtc;
            var rangeEnd = queryParams?.EndAtUtc;
            query = query.Where(series =>
                series.AssignmentEntries.Any(entry =>
                    entry.Event != null
                    && entry.Event.StatusTypeCode == CalendarEventStatusTypeCodes.Active
                    && (!rangeEnd.HasValue || entry.Event.StartAtUtc < rangeEnd.Value)
                    && (!rangeStart.HasValue || (entry.Event.EndAtUtc ?? entry.Event.StartAtUtc) > rangeStart.Value)
                )
            );
        }

        var results = await query
            .OrderBy(series => series.EventSeriesId)
            .ThenBy(series => series.Id)
            .ToListAsync(cancellationToken);
        return await MapToAssignmentSeriesResponsesAsync(results, cancellationToken);
    }

    public async Task<AssignmentSeriesResponse?> GetAssignmentSeriesByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var result = await db
            .AssignmentSeries.AsNoTracking()
            .Include(series => series.EventSeries)
            .Include(series => series.AssignmentCategoryType)
            .Include(series => series.AssignmentSubCategoryType)
            .Include(series => series.AssignmentType)
            .SingleOrDefaultAsync(series => series.Id == id, cancellationToken);

        return result is null ? null : await MapToAssignmentSeriesResponseAsync(result, cancellationToken);
    }

    public async Task<AssignmentSeriesResponse> CreateAssignmentSeriesAsync(
        AssignmentSeriesRequest request,
        CancellationToken cancellationToken = default
    )
    {
        await ValidateActiveLookupTypesAsync(request, cancellationToken);
        logger.LogInformation("Creating assignment series starting at {StartAtUtc}.", request.StartAtUtc);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var eventSeries = MapToEventSeries(request);
        db.EventSeries.Add(eventSeries);
        await db.SaveChangesAsync(cancellationToken);

        var assignmentSeries = new AssignmentSeries
        {
            EventSeriesId = eventSeries.Id,
            AssignmentCategoryTypeId = request.AssignmentCategoryTypeId,
            AssignmentSubCategoryTypeId = request.AssignmentSubCategoryTypeId,
            AssignmentTypeId = request.AssignmentTypeId,
            Capacity = request.Capacity,
        };

        db.AssignmentSeries.Add(assignmentSeries);
        await db.SaveChangesAsync(cancellationToken);

        var materializationResult = await eventSeriesMaterializationService.MaterializeAsync(
            eventSeries,
            AssignmentMaterializationOptions,
            assignmentSeriesMaterializationHandler,
            cancellationToken
        );

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Created assignment series {AssignmentSeriesId}.", assignmentSeries.Id);
        return await MapToAssignmentSeriesResponseAsync(assignmentSeries, cancellationToken, materializationResult);
    }

    public async Task<AssignmentSeriesResponse?> UpdateAssignmentSeriesAsync(
        int id,
        AssignmentSeriesRequest request,
        CancellationToken cancellationToken = default
    )
    {
        await ValidateActiveLookupTypesAsync(request, cancellationToken);
        logger.LogInformation("Updating assignment series {AssignmentSeriesId}.", id);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var assignmentSeries = await db
            .AssignmentSeries.Include(series => series.EventSeries!)
                .ThenInclude(eventSeries => eventSeries.Events)
            .Include(series => series.AssignmentEntries)
                .ThenInclude(entry => entry.Event)
            .SingleOrDefaultAsync(series => series.Id == id, cancellationToken);

        if (assignmentSeries is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        ValidateAssignmentEventSeriesType(assignmentSeries.EventSeries!);
        var oldEventSeriesValues = CaptureEventSeriesValues(assignmentSeries.EventSeries!);
        var oldCategoryTypeId = assignmentSeries.AssignmentCategoryTypeId;
        var oldSubCategoryTypeId = assignmentSeries.AssignmentSubCategoryTypeId;
        var oldAssignmentTypeId = assignmentSeries.AssignmentTypeId;
        var oldCapacity = assignmentSeries.Capacity;
        var recurrenceChanged = HasRecurrenceChanged(assignmentSeries.EventSeries!, request);

        UpdateEventSeries(assignmentSeries.EventSeries!, request);
        assignmentSeries.AssignmentCategoryTypeId = request.AssignmentCategoryTypeId;
        assignmentSeries.AssignmentSubCategoryTypeId = request.AssignmentSubCategoryTypeId;
        assignmentSeries.AssignmentTypeId = request.AssignmentTypeId;
        assignmentSeries.Capacity = request.Capacity;

        EventSeriesMaterializationResult? materializationResult = null;

        if (recurrenceChanged)
        {
            await db.SaveChangesAsync(cancellationToken);
            materializationResult = await eventSeriesMaterializationService.RecreateAsync(
                assignmentSeries.EventSeries!,
                AssignmentMaterializationOptions,
                assignmentSeriesMaterializationHandler,
                cancellationToken
            );
        }
        else
        {
            foreach (
                var entry in assignmentSeries.AssignmentEntries.Where(entry =>
                    entry.Event?.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                )
            )
            {
                ApplyEventSeriesFieldUpdatePreservingOverrides(
                    entry.Event!,
                    oldEventSeriesValues,
                    assignmentSeries.EventSeries!
                );
                if (entry.AssignmentCategoryTypeId == oldCategoryTypeId)
                    entry.AssignmentCategoryTypeId = request.AssignmentCategoryTypeId;
                if (entry.AssignmentSubCategoryTypeId == oldSubCategoryTypeId)
                    entry.AssignmentSubCategoryTypeId = request.AssignmentSubCategoryTypeId;
                if (entry.AssignmentTypeId == oldAssignmentTypeId)
                    entry.AssignmentTypeId = request.AssignmentTypeId;
                if (entry.Capacity == oldCapacity)
                    entry.Capacity = request.Capacity;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Updated assignment series {AssignmentSeriesId}.", id);
        return await MapToAssignmentSeriesResponseAsync(assignmentSeries, cancellationToken, materializationResult);
    }

    public async Task<AssignmentSeriesResponse?> ExpireAssignmentSeriesAsync(
        int id,
        ExpireShiftRequest request,
        Guid? cancelledByUserId = null,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Expiring assignment series {AssignmentSeriesId}.", id);
        var assignmentSeries = await db
            .AssignmentSeries.Include(series => series.EventSeries!)
                .ThenInclude(eventSeries => eventSeries.Events)
            .SingleOrDefaultAsync(series => series.Id == id, cancellationToken);

        if (assignmentSeries is null)
            return null;

        ValidateAssignmentEventSeriesType(assignmentSeries.EventSeries!);
        calendarLifecycleService.CancelSeries(
            assignmentSeries.EventSeries!,
            assignmentSeries.EventSeries!.Events.ToList(),
            DateTimeOffset.UtcNow,
            cancelledByUserId,
            request.CancellationReason
        );

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Expired assignment series {AssignmentSeriesId}.", id);
        return await MapToAssignmentSeriesResponseAsync(assignmentSeries, cancellationToken);
    }

    public async Task<IReadOnlyCollection<AssignmentEntryResponse>> GetAssignmentEntriesAsync(
        AssignmentEntryQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    )
    {
        IQueryable<AssignmentEntry> query = IncludeAssignmentEntryGraph(db.AssignmentEntries.AsNoTracking());

        if (queryParams?.AssignmentSeriesId is int assignmentSeriesId)
            query = query.Where(entry => entry.AssignmentSeriesId == assignmentSeriesId);
        if (queryParams?.EventId is int eventId)
            query = query.Where(entry => entry.EventId == eventId);
        if (queryParams?.AssignmentTypeId is int assignmentTypeId)
            query = query.Where(entry => entry.AssignmentTypeId == assignmentTypeId);
        if (queryParams?.LocationId is int locationId)
            query = query.Where(entry => entry.Event != null && entry.Event.LocationId == locationId);
        if (!string.IsNullOrWhiteSpace(queryParams?.StatusTypeCode))
        {
            var statusTypeCode = queryParams.StatusTypeCode.Trim();
            query = query.Where(entry => entry.Event != null && entry.Event.StatusTypeCode == statusTypeCode);
        }
        if (queryParams?.StartAtUtc.HasValue == true || queryParams?.EndAtUtc.HasValue == true)
        {
            var rangeStart = queryParams?.StartAtUtc;
            var rangeEnd = queryParams?.EndAtUtc;
            query = query.Where(entry =>
                entry.Event != null
                && (!rangeEnd.HasValue || entry.Event.StartAtUtc < rangeEnd.Value)
                && (!rangeStart.HasValue || (entry.Event.EndAtUtc ?? entry.Event.StartAtUtc) > rangeStart.Value)
            );
        }

        var results = await query
            .OrderBy(entry => entry.EventId)
            .ThenBy(entry => entry.Id)
            .ToListAsync(cancellationToken);
        return results.Select(MapToAssignmentEntryResponse).ToList();
    }

    public async Task<AssignmentEntryResponse?> GetAssignmentEntryByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var result = await IncludeAssignmentEntryGraph(db.AssignmentEntries.AsNoTracking())
            .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);
        return result is null ? null : MapToAssignmentEntryResponse(result);
    }

    public async Task<AssignmentEntryResponse> CreateAssignmentEntryAsync(
        AssignmentEntryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        await ValidateActiveLookupTypesAsync(request, cancellationToken);
        logger.LogInformation("Creating assignment entry starting at {StartAtUtc}.", request.StartAtUtc);

        var assignmentSeries = request.AssignmentSeriesId.HasValue
            ? await GetValidatedAssignmentSeriesAsync(request.AssignmentSeriesId.Value, cancellationToken)
            : null;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var eventEntity = MapToEvent(request, assignmentSeries?.EventSeriesId);
        db.Events.Add(eventEntity);
        await db.SaveChangesAsync(cancellationToken);

        var assignmentEntry = new AssignmentEntry
        {
            AssignmentSeriesId = request.AssignmentSeriesId,
            EventId = eventEntity.Id,
            AssignmentCategoryTypeId = request.AssignmentCategoryTypeId,
            AssignmentSubCategoryTypeId = request.AssignmentSubCategoryTypeId,
            AssignmentTypeId = request.AssignmentTypeId,
            Capacity = request.Capacity,
        };

        db.AssignmentEntries.Add(assignmentEntry);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Created assignment entry {AssignmentEntryId}.", assignmentEntry.Id);
        return MapToAssignmentEntryResponse(assignmentEntry);
    }

    public async Task<AssignmentEntryResponse?> UpdateAssignmentEntryAsync(
        int id,
        AssignmentEntryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        await ValidateActiveLookupTypesAsync(request, cancellationToken);
        logger.LogInformation("Updating assignment entry {AssignmentEntryId}.", id);

        var assignmentEntry = await db
            .AssignmentEntries.Include(entry => entry.Event)
            .Include(entry => entry.ShiftAssignmentEntries)
                .ThenInclude(link => link.Users)
            .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);

        if (assignmentEntry is null)
            return null;

        var assignmentSeries = request.AssignmentSeriesId.HasValue
            ? await GetValidatedAssignmentSeriesAsync(request.AssignmentSeriesId.Value, cancellationToken)
            : null;

        ValidateAssignmentEventType(assignmentEntry.Event!);
        UpdateEvent(assignmentEntry.Event!, request, assignmentSeries?.EventSeriesId);
        assignmentEntry.AssignmentSeriesId = request.AssignmentSeriesId;
        assignmentEntry.AssignmentCategoryTypeId = request.AssignmentCategoryTypeId;
        assignmentEntry.AssignmentSubCategoryTypeId = request.AssignmentSubCategoryTypeId;
        assignmentEntry.AssignmentTypeId = request.AssignmentTypeId;
        assignmentEntry.Capacity = request.Capacity;

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated assignment entry {AssignmentEntryId}.", id);
        return MapToAssignmentEntryResponse(assignmentEntry);
    }

    public async Task<AssignmentEntryResponse?> ExpireAssignmentEntryAsync(
        int id,
        ExpireShiftRequest request,
        Guid? cancelledByUserId = null,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Expiring assignment entry {AssignmentEntryId}.", id);
        var assignmentEntry = await IncludeAssignmentEntryGraph(db.AssignmentEntries)
            .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);

        if (assignmentEntry is null)
            return null;

        ValidateAssignmentEventType(assignmentEntry.Event!);
        calendarLifecycleService.Cancel(
            assignmentEntry.Event!,
            DateTimeOffset.UtcNow,
            cancelledByUserId,
            request.CancellationReason
        );

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Expired assignment entry {AssignmentEntryId}.", id);
        return MapToAssignmentEntryResponse(assignmentEntry);
    }

    private async Task ValidateActiveLookupTypesAsync(
        IAssignmentRequestFields request,
        CancellationToken cancellationToken
    )
    {
        var now = DateTimeOffset.UtcNow;
        if (
            !await IsActiveCodeAsync(
                db.AssignmentCategoryTypes,
                request.AssignmentCategoryTypeId,
                now,
                cancellationToken
            )
        )
            throw new InvalidOperationException("Assignment category type is not active.");
        if (
            !await IsActiveCodeAsync(
                db.AssignmentSubCategoryTypes,
                request.AssignmentSubCategoryTypeId,
                now,
                cancellationToken
            )
        )
            throw new InvalidOperationException("Assignment subcategory type is not active.");
        if (!await IsActiveCodeAsync(db.AssignmentTypes, request.AssignmentTypeId, now, cancellationToken))
            throw new InvalidOperationException("Assignment type is not active.");
    }

    private static Task<bool> IsActiveCodeAsync<T>(
        DbSet<T> set,
        int id,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
        where T : Unified.Db.Models.Abstract.BaseCodeTypeEntity
    {
        return set.AnyAsync(
            code =>
                EF.Property<int>(code, "Id") == id
                && code.EffectiveDate <= now
                && (!code.ExpiryDate.HasValue || code.ExpiryDate > now),
            cancellationToken
        );
    }

    private async Task<AssignmentSeries> GetValidatedAssignmentSeriesAsync(
        int assignmentSeriesId,
        CancellationToken cancellationToken
    )
    {
        var assignmentSeries = await db
            .AssignmentSeries.Include(series => series.EventSeries)
            .SingleOrDefaultAsync(series => series.Id == assignmentSeriesId, cancellationToken);
        if (assignmentSeries is null)
            throw new KeyNotFoundException($"Assignment series {assignmentSeriesId} not found.");

        ValidateAssignmentEventSeriesType(assignmentSeries.EventSeries!);
        return assignmentSeries;
    }

    private async Task<IReadOnlyCollection<AssignmentSeriesResponse>> MapToAssignmentSeriesResponsesAsync(
        IReadOnlyCollection<AssignmentSeries> assignmentSeries,
        CancellationToken cancellationToken
    )
    {
        var entries = await LoadAssignmentSeriesEntriesAsync(assignmentSeries, cancellationToken);
        return assignmentSeries
            .Select(series => MapToAssignmentSeriesResponse(series, entries.GetValueOrDefault(series.Id, [])))
            .ToList();
    }

    private async Task<AssignmentSeriesResponse> MapToAssignmentSeriesResponseAsync(
        AssignmentSeries assignmentSeries,
        CancellationToken cancellationToken,
        EventSeriesMaterializationResult? materializationResult = null
    )
    {
        var entryIds = await LoadAssignmentSeriesEntriesAsync([assignmentSeries], cancellationToken);
        var ids = entryIds.GetValueOrDefault(assignmentSeries.Id, []);
        if (materializationResult is not null)
        {
            var currentEventIds = materializationResult
                .CreatedEventIds.Concat(materializationResult.UpdatedEventIds)
                .ToHashSet();
            ids = ids.OrderByDescending(entry => currentEventIds.Contains(entry.EventId))
                .ThenBy(entry => entry.Id)
                .ToList();
        }

        if (assignmentSeries.EventSeries is null)
            assignmentSeries.EventSeries = await db
                .EventSeries.AsNoTracking()
                .SingleOrDefaultAsync(series => series.Id == assignmentSeries.EventSeriesId, cancellationToken);

        return MapToAssignmentSeriesResponse(assignmentSeries, ids);
    }

    private async Task<Dictionary<int, List<AssignmentEntryResponse>>> LoadAssignmentSeriesEntriesAsync(
        IReadOnlyCollection<AssignmentSeries> assignmentSeries,
        CancellationToken cancellationToken
    )
    {
        if (assignmentSeries.Count == 0)
            return [];

        var assignmentSeriesIds = assignmentSeries.Select(series => series.Id).ToList();
        var entries = await IncludeAssignmentEntryGraph(db.AssignmentEntries.AsNoTracking())
            .Where(entry =>
                entry.AssignmentSeriesId.HasValue && assignmentSeriesIds.Contains(entry.AssignmentSeriesId.Value)
            )
            .Where(entry => entry.Event != null && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled)
            .OrderBy(entry => entry.Id)
            .ToListAsync(cancellationToken);

        return entries
            .GroupBy(entry => entry.AssignmentSeriesId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(MapToAssignmentEntryResponse).ToList());
    }

    private static AssignmentSeriesResponse MapToAssignmentSeriesResponse(
        AssignmentSeries assignmentSeries,
        IReadOnlyCollection<AssignmentEntryResponse> entries
    ) =>
        new()
        {
            Id = assignmentSeries.Id,
            EventSeriesId = assignmentSeries.EventSeriesId,
            Title = assignmentSeries.EventSeries?.Title,
            Description = assignmentSeries.EventSeries?.Description,
            Notes = assignmentSeries.EventSeries?.Notes,
            Color = assignmentSeries.EventSeries?.Color,
            RecurrenceRule = assignmentSeries.EventSeries?.RecurrenceRule,
            TimeZoneId = assignmentSeries.EventSeries?.TimeZoneId,
            StartAtUtc = assignmentSeries.EventSeries?.StartAtUtc,
            EndAtUtc = assignmentSeries.EventSeries?.EndAtUtc,
            AllDay = assignmentSeries.EventSeries?.AllDay ?? false,
            EventTypeCode = assignmentSeries.EventSeries?.EventTypeCode,
            StatusTypeCode = assignmentSeries.EventSeries?.StatusTypeCode,
            CancelledAt = assignmentSeries.EventSeries?.CancelledAt,
            CancelledByUserId = assignmentSeries.EventSeries?.CancelledByUserId,
            CancellationReason = assignmentSeries.EventSeries?.CancellationReason,
            LocationId = assignmentSeries.EventSeries?.LocationId,
            AssignmentCategoryTypeId = assignmentSeries.AssignmentCategoryTypeId,
            AssignmentCategoryTypeCode = assignmentSeries.AssignmentCategoryType?.Code,
            AssignmentSubCategoryTypeId = assignmentSeries.AssignmentSubCategoryTypeId,
            AssignmentSubCategoryTypeCode = assignmentSeries.AssignmentSubCategoryType?.Code,
            AssignmentTypeId = assignmentSeries.AssignmentTypeId,
            AssignmentTypeCode = assignmentSeries.AssignmentType?.Code,
            Capacity = assignmentSeries.Capacity,
            EventIds = entries.Select(entry => entry.EventId).ToList(),
            AssignmentEntryIds = entries.Select(entry => entry.Id).ToList(),
            Entries = entries,
        };

    private static IQueryable<AssignmentEntry> IncludeAssignmentEntryGraph(IQueryable<AssignmentEntry> query) =>
        query
            .Include(entry => entry.Event)
            .Include(entry => entry.AssignmentCategoryType)
            .Include(entry => entry.AssignmentSubCategoryType)
            .Include(entry => entry.AssignmentType)
            .Include(entry => entry.ShiftAssignmentEntries)
                .ThenInclude(link => link.Users);

    private static AssignmentEntryResponse MapToAssignmentEntryResponse(AssignmentEntry assignmentEntry)
    {
        var linkedShiftEntryIds = assignmentEntry
            .ShiftAssignmentEntries.Select(link => link.ShiftEntryId)
            .Distinct()
            .ToList();
        var assignedUserIds = assignmentEntry
            .ShiftAssignmentEntries.SelectMany(link => link.Users)
            .Select(user => user.UserId)
            .Distinct()
            .ToList();

        return new AssignmentEntryResponse
        {
            Id = assignmentEntry.Id,
            AssignmentSeriesId = assignmentEntry.AssignmentSeriesId,
            EventId = assignmentEntry.EventId,
            Title = assignmentEntry.Event?.Title,
            Description = assignmentEntry.Event?.Description,
            Notes = assignmentEntry.Event?.Notes,
            Color = assignmentEntry.Event?.Color,
            StartAtUtc = assignmentEntry.Event?.StartAtUtc,
            EndAtUtc = assignmentEntry.Event?.EndAtUtc,
            SeriesStartAtUtc = assignmentEntry.Event?.SeriesStartAtUtc,
            SeriesEndAtUtc = assignmentEntry.Event?.SeriesEndAtUtc,
            TimeZoneId = assignmentEntry.Event?.TimeZoneId,
            AllDay = assignmentEntry.Event?.AllDay ?? false,
            IsException = assignmentEntry.Event?.IsException ?? false,
            EventTypeCode = assignmentEntry.Event?.EventTypeCode,
            StatusTypeCode = assignmentEntry.Event?.StatusTypeCode,
            CancelledAt = assignmentEntry.Event?.CancelledAt,
            CancelledByUserId = assignmentEntry.Event?.CancelledByUserId,
            CancellationReason = assignmentEntry.Event?.CancellationReason,
            LocationId = assignmentEntry.Event?.LocationId,
            AssignmentCategoryTypeId = assignmentEntry.AssignmentCategoryTypeId,
            AssignmentCategoryTypeCode = assignmentEntry.AssignmentCategoryType?.Code,
            AssignmentSubCategoryTypeId = assignmentEntry.AssignmentSubCategoryTypeId,
            AssignmentSubCategoryTypeCode = assignmentEntry.AssignmentSubCategoryType?.Code,
            AssignmentTypeId = assignmentEntry.AssignmentTypeId,
            AssignmentTypeCode = assignmentEntry.AssignmentType?.Code,
            Capacity = assignmentEntry.Capacity,
            AssignedUserCount = assignedUserIds.Count,
            LinkedShiftEntryIds = linkedShiftEntryIds,
            AssignedUserIds = assignedUserIds,
        };
    }

    private static EventSeries MapToEventSeries(AssignmentSeriesRequest request) =>
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
            EventTypeCode = SchedulingConstants.AssignmentEventTypeCode,
            StatusTypeCode = CalendarEventStatusTypeCodes.Active,
            LocationId = request.LocationId,
        };

    private static void UpdateEventSeries(EventSeries eventSeries, AssignmentSeriesRequest request)
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

    private static bool HasRecurrenceChanged(EventSeries eventSeries, AssignmentSeriesRequest request) =>
        !StringEqualsNormalized(eventSeries.RecurrenceRule, request.RecurrenceRule)
        || eventSeries.StartAtUtc != request.StartAtUtc
        || eventSeries.EndAtUtc != request.EndAtUtc
        || !StringEqualsNormalized(eventSeries.TimeZoneId, request.TimeZoneId)
        || eventSeries.AllDay != request.AllDay;

    private static EventSeriesCopiedValues CaptureEventSeriesValues(EventSeries eventSeries) =>
        new(eventSeries.Title, eventSeries.Description, eventSeries.Notes, eventSeries.Color, eventSeries.LocationId);

    private static void ApplyEventSeriesFieldUpdatePreservingOverrides(
        Event eventEntity,
        EventSeriesCopiedValues oldValues,
        EventSeries eventSeries
    )
    {
        if (eventEntity.Title == oldValues.Title)
            eventEntity.Title = eventSeries.Title;
        if (eventEntity.Description == oldValues.Description)
            eventEntity.Description = eventSeries.Description;
        if (eventEntity.Notes == oldValues.Notes)
            eventEntity.Notes = eventSeries.Notes;
        if (eventEntity.Color == oldValues.Color)
            eventEntity.Color = eventSeries.Color;
        if (eventEntity.LocationId == oldValues.LocationId)
            eventEntity.LocationId = eventSeries.LocationId;
    }

    private static bool StringEqualsNormalized(string? left, string? right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.Ordinal);

    private static Event MapToEvent(AssignmentEntryRequest request, int? eventSeriesId) =>
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
            EventTypeCode = SchedulingConstants.AssignmentEventTypeCode,
            StatusTypeCode = CalendarEventStatusTypeCodes.Active,
            SourceModule = SchedulingConstants.SourceModule,
            LocationId = request.LocationId,
        };

    private static void UpdateEvent(Event eventEntity, AssignmentEntryRequest request, int? eventSeriesId)
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

    private static void ValidateAssignmentEventSeriesType(EventSeries eventSeries)
    {
        if (eventSeries.EventTypeCode != SchedulingConstants.AssignmentEventTypeCode)
            throw new InvalidOperationException($"Event series {eventSeries.Id} is not an assignment event series.");
    }

    private static void ValidateAssignmentEventType(Event eventEntity)
    {
        if (eventEntity.EventTypeCode != SchedulingConstants.AssignmentEventTypeCode)
            throw new InvalidOperationException($"Event {eventEntity.Id} is not an assignment event.");

        if (eventEntity.SourceModule != SchedulingConstants.SourceModule)
            throw new InvalidOperationException($"Event {eventEntity.Id} is not owned by Scheduling.");
    }

    private sealed record EventSeriesCopiedValues(
        string Title,
        string? Description,
        string? Notes,
        string? Color,
        int? LocationId
    );
}
