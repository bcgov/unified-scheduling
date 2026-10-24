using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Calendar.Conflicts;
using Unified.Calendar.Services;
using Unified.Common.Calendar.Conflicts;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Mappings;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public sealed class AssignmentSeriesService(
    ILogger<AssignmentSeriesService> logger,
    UnifiedDbContext db,
    ICalendarEventService<AssignmentSeriesRequest, AssignmentSeries, EventSeries> calendarEventSeriesService,
    IEventSeriesMaterializationService eventSeriesMaterializationService,
    AssignmentSeriesMaterializationHandler assignmentSeriesMaterializationHandler,
    IShiftAssignmentService shiftAssignmentService,
    CalendarLifecycleService calendarLifecycleService,
    ITimeZoneService timeZoneService,
    TimeProvider timeProvider,
    ICalendarConflictService calendarConflictService
) : IAssignmentSeriesService
{
    private static readonly RecurrenceValidationOptions AssignmentRecurrenceValidationOptions = new()
    {
        MaximumDuration = TimeSpan.FromDays(365),
        MaximumOccurrences = 400,
        RequireBoundedRule = true,
    };

    public async Task<IReadOnlyCollection<AssignmentSeriesResponse>> GetAssignmentSeriesAsync(
        AssignmentSeriesQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    )
    {
        IQueryable<AssignmentSeries> query = db
            .AssignmentSeries.AsNoTracking()
            .Include(series => series.EventSeries)
            .Include(series => series.Category)
            .Include(series => series.SubCategory)
            .Include(series => series.ShiftAssignmentSeriesLinks)
                .ThenInclude(link => link.Users);

        if (queryParams?.EventSeriesId is int eventSeriesId)
            query = query.Where(series => series.EventSeriesId == eventSeriesId);
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
                    && (!rangeEnd.HasValue || entry.Event.StartAtUtc < rangeEnd.Value)
                    && (
                        !rangeStart.HasValue
                        || (entry.Event.EndAtUtc.HasValue && entry.Event.EndAtUtc.Value > rangeStart.Value)
                    )
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
            .Include(series => series.Category)
            .Include(series => series.SubCategory)
            .Include(series => series.ShiftAssignmentSeriesLinks)
                .ThenInclude(link => link.Users)
            .SingleOrDefaultAsync(series => series.Id == id, cancellationToken);
        return result is null ? null : await MapToAssignmentSeriesResponseAsync(result, cancellationToken);
    }

    public async Task<AssignmentSeriesResponse> CreateAssignmentSeriesAsync(
        AssignmentSeriesRequest request,
        CancellationToken cancellationToken = default
    )
    {
        await ValidateAssignmentSeriesRequest(request, cancellationToken);
        logger.LogInformation("Creating assignment series starting at {StartAtUtc}.", request.StartAtUtc);
        var assignmentSeries = await calendarEventSeriesService.BuildEventAsync(
            request,
            SchedulingConstants.SourceModule,
            SchedulingConstants.AssignmentEventTypeCode,
            cancellationToken: cancellationToken
        );

        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken
        );
        db.AssignmentSeries.Add(assignmentSeries);
        await db.SaveChangesAsync(cancellationToken);
        await eventSeriesMaterializationService.MaterializeAsync(
            assignmentSeries.EventSeries!,
            AssignmentRecurrenceValidationOptions,
            assignmentSeriesMaterializationHandler,
            new AssignmentSeriesMaterializationContext { AssignmentSeries = assignmentSeries },
            cancellationToken
        );
        await db.SaveChangesAsync(cancellationToken);
        await EnsureSeriesAssignmentsAreUniqueAsync(assignmentSeries.Id, cancellationToken);
        await shiftAssignmentService.ReplaceAssignmentSeriesLinksAsync(
            assignmentSeries.Id,
            request.ShiftSeriesLinks ?? [],
            cancellationToken
        );
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Created assignment series {AssignmentSeriesId}.", assignmentSeries.Id);
        return await MapToAssignmentSeriesResponseAsync(assignmentSeries, cancellationToken);
    }

    private async Task ValidateAssignmentSeriesRequest(AssignmentSeriesRequest input, CancellationToken ct)
    {
        var definition = await GetActiveDefinitionAsync(input.AssignmentDefinitionId, input.StartAtUtc, ct);
        await ValidateAssignmentValuesAsync(input.LocationId, input.CategoryId, input.SubCategoryId, definition, ct);
    }

    public async Task<AssignmentSeriesResponse?> UpdateAssignmentSeriesAsync(
        int id,
        AssignmentSeriesRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(request.RecurrenceRule))
            throw new ArgumentException("A recurring assignment series cannot be converted to a single assignment.");
        await ValidateAssignmentSeriesRequest(request, cancellationToken);
        logger.LogInformation("Updating assignment series {AssignmentSeriesId}.", id);
        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken
        );
        var entity = await db
            .AssignmentSeries.Include(series => series.EventSeries!)
                .ThenInclude(eventSeries => eventSeries.Events)
            .Include(series => series.AssignmentEntries)
                .ThenInclude(entry => entry.Event)
            .Include(series => series.ShiftAssignmentSeriesLinks)
                .ThenInclude(link => link.Users)
            .SingleOrDefaultAsync(series => series.Id == id, cancellationToken);
        if (entity is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }
        // Capture recurrence changes and inherited values before the update mutates the series.
        var updatePlan = AssignmentSeriesUpdatePlanner.CreatePlan(entity, request);

        var assignmentSeries =
            await calendarEventSeriesService.UpdateEventAsync(
                request,
                entity,
                SchedulingConstants.SourceModule,
                SchedulingConstants.AssignmentEventTypeCode,
                cancellationToken: cancellationToken
            ) ?? throw new InvalidOperationException("Failed to update assignment series.");

        if (updatePlan.RegenerateEntries)
        {
            await shiftAssignmentService.ReplaceAssignmentSeriesLinksAsync(assignmentSeries.Id, [], cancellationToken);
            await eventSeriesMaterializationService.RegenerateDraftSeriesAsync(
                assignmentSeries.EventSeries!,
                AssignmentRecurrenceValidationOptions,
                assignmentSeriesMaterializationHandler,
                new AssignmentSeriesMaterializationContext
                {
                    AssignmentSeries = assignmentSeries,
                    ExistingEntries = assignmentSeries.AssignmentEntries.ToList(),
                },
                cancellationToken
            );
        }
        else if (updatePlan.PropagateSeriesChanges)
            PropagateSeriesChangesToEntries(assignmentSeries, request, updatePlan);
        await db.SaveChangesAsync(cancellationToken);
        await EnsureSeriesAssignmentsAreUniqueAsync(assignmentSeries.Id, cancellationToken);
        if (request.ShiftSeriesLinks is not null)
            await shiftAssignmentService.ReplaceAssignmentSeriesLinksAsync(
                assignmentSeries.Id,
                request.ShiftSeriesLinks,
                cancellationToken
            );
        var updatedConflictState = await SchedulingConflictParticipantProvider.GetParticipantsForAssignmentSeriesAsync(
            db,
            id,
            cancellationToken
        );
        await calendarConflictService.EnsureNoUnresolvedConflictsAsync(updatedConflictState, cancellationToken);
        var affectedEventIds = await db
            .AssignmentEntries.Where(entry => entry.AssignmentSeriesId == id)
            .Select(entry => entry.EventId)
            .ToListAsync(cancellationToken);
        await calendarConflictService.InvalidateResolvedOverridesAsync(
            affectedEventIds
                .Select(eventId => new CalendarConflictEventIdentity(
                    SchedulingConstants.SourceModule,
                    eventId.ToString()
                ))
                .ToList(),
            cancellationToken: cancellationToken
        );
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Updated assignment series {AssignmentSeriesId}.", id);
        return await MapToAssignmentSeriesResponseAsync(assignmentSeries, cancellationToken);
    }

    public async Task<AssignmentSeriesResponse?> PublishAssignmentSeriesAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Publishing assignment series {AssignmentSeriesId}.", id);
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
            : null;
        var series = await db
            .AssignmentSeries.Include(item => item.EventSeries!)
                .ThenInclude(item => item.Events)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (series is null)
            return null;
        ValidateAssignmentEventSeriesType(series.EventSeries!);
        calendarLifecycleService.PublishSeries(series.EventSeries!, series.EventSeries!.Events.ToList());
        await db.SaveChangesAsync(cancellationToken);
        var conflicts = await SchedulingConflictParticipantProvider.GetParticipantsForAssignmentSeriesAsync(
            db,
            id,
            cancellationToken
        );
        await calendarConflictService.EnsureNoUnresolvedConflictsAsync(conflicts, cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Published assignment series {AssignmentSeriesId}.", id);
        return await MapToAssignmentSeriesResponseAsync(series, cancellationToken);
    }

    public async Task<AssignmentSeriesResponse?> ExpireAssignmentSeriesAsync(
        int id,
        ExpireShiftRequest request,
        Guid? cancelledByUserId = null,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Expiring assignment series {AssignmentSeriesId}.", id);
        var series = await db
            .AssignmentSeries.Include(item => item.EventSeries!)
                .ThenInclude(item => item.Events)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (series is null)
            return null;
        ValidateAssignmentEventSeriesType(series.EventSeries!);
        calendarLifecycleService.CancelSeries(
            series.EventSeries!,
            series.EventSeries!.Events.ToList(),
            timeProvider.GetUtcNow(),
            cancelledByUserId,
            request.CancellationReason
        );
        await db.SaveChangesAsync(cancellationToken);
        await calendarConflictService.InvalidateResolvedOverridesAsync(
            series
                .EventSeries.Events.Select(item => new CalendarConflictEventIdentity(
                    SchedulingConstants.SourceModule,
                    item.Id.ToString()
                ))
                .ToList(),
            updatedById: cancelledByUserId,
            cancellationToken: cancellationToken
        );
        logger.LogInformation("Expired assignment series {AssignmentSeriesId}.", id);
        return await MapToAssignmentSeriesResponseAsync(series, cancellationToken);
    }

    public async Task<bool> DeleteAssignmentSeriesAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Deleting assignment series {AssignmentSeriesId}.", id);
        var series = await db
            .AssignmentSeries.Include(item => item.EventSeries)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        var isDeletable = calendarEventSeriesService.IsDeletable(
            series,
            SchedulingConstants.SourceModule,
            SchedulingConstants.AssignmentEventTypeCode
        );
        if (!isDeletable)
            return false;
        var eventSeries = series.EventSeries!;
        var childEvents = await db
            .Events.Where(item =>
                item.EventSeriesId == eventSeries.Id
                && item.EventTypeCode == SchedulingConstants.AssignmentEventTypeCode
                && item.SourceModule == SchedulingConstants.SourceModule
            )
            .ToListAsync(cancellationToken);
        var entries = await db
            .AssignmentEntries.Include(entry => entry.Event)
            .Where(entry => entry.AssignmentSeriesId == id)
            .ToListAsync(cancellationToken);
        var draftEvents = childEvents.Where(item => item.StatusTypeCode == CalendarEventStatusTypeCodes.Draft).ToList();
        var draftEventIds = draftEvents.Select(item => item.Id).ToHashSet();
        var draftEntries = entries.Where(entry => draftEventIds.Contains(entry.EventId)).ToList();
        var draftEntryIds = draftEntries.Select(entry => entry.Id).ToHashSet();
        var draftEntryLinks = await db
            .ShiftAssignmentEntries.Include(link => link.Users)
            .Where(link => draftEntryIds.Contains(link.AssignmentEntryId))
            .ToListAsync(cancellationToken);
        var seriesLinks = await db
            .ShiftAssignmentSeriesLinks.Include(link => link.Users)
            .Include(link => link.EntryLinks)
                .ThenInclude(link => link.Users)
            .Where(link => link.AssignmentSeriesId == id)
            .ToListAsync(cancellationToken);
        foreach (var retainedEntry in entries.Except(draftEntries))
            retainedEntry.AssignmentSeriesId = null;
        foreach (var retainedEvent in childEvents.Except(draftEvents))
            retainedEvent.EventSeriesId = null;
        var retainedLinks = seriesLinks
            .SelectMany(link => link.EntryLinks)
            .Where(link => !draftEntryIds.Contains(link.AssignmentEntryId))
            .ToList();
        var suppressedLinks = retainedLinks.Where(link => link.Users.Count == 0).ToList();
        foreach (var retainedLink in retainedLinks.Except(suppressedLinks))
        {
            retainedLink.ShiftAssignmentSeriesLinkId = null;
            retainedLink.ShiftAssignmentSeriesLink = null;
            retainedLink.IsException = false;
        }
        var removedLinks = draftEntryLinks.Concat(suppressedLinks).ToList();
        db.ShiftAssignmentEntryUsers.RemoveRange(removedLinks.SelectMany(link => link.Users));
        db.ShiftAssignmentEntries.RemoveRange(removedLinks);
        db.ShiftAssignmentSeriesLinkUsers.RemoveRange(seriesLinks.SelectMany(link => link.Users));
        db.ShiftAssignmentSeriesLinks.RemoveRange(seriesLinks);
        db.AssignmentEntries.RemoveRange(draftEntries);
        db.Events.RemoveRange(draftEvents);
        db.AssignmentSeries.Remove(series);
        db.EventSeries.Remove(eventSeries);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted assignment series {AssignmentSeriesId}.", id);
        return true;
    }

    private async Task EnsureSeriesAssignmentsAreUniqueAsync(
        int assignmentSeriesId,
        CancellationToken cancellationToken
    )
    {
        var entries = await db
            .AssignmentEntries.AsNoTracking()
            .Include(entry => entry.Event)
            .Where(entry => entry.AssignmentSeriesId == assignmentSeriesId)
            .Where(entry => entry.Event != null && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled)
            .ToListAsync(cancellationToken);
        await AssignmentUniquenessValidator.EnsureAssignmentsAreUniqueAsync(
            db,
            timeZoneService,
            entries
                .Select(entry =>
                    AssignmentUniquenessValidator.CreateCandidate(entry.AssignmentDefinitionId, entry.Event!)
                )
                .ToList(),
            entries.Select(entry => entry.Id).ToList(),
            cancellationToken
        );
    }

    private async Task<AssignmentDefinition> GetActiveDefinitionAsync(
        int id,
        DateTimeOffset startAtUtc,
        CancellationToken cancellationToken
    )
    {
        var definition =
            await db
                .AssignmentDefinitions.Include(item => item.Location)
                .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Assignment definition {id} not found.");
        var timeZone = timeZoneService.ResolveRequired(definition.Location.Timezone);
        var assignmentDate = DateOnly.FromDateTime(timeZoneService.ToLocalUnspecified(startAtUtc, timeZone));
        var effectiveDate = DateOnly.FromDateTime(definition.EffectiveDateUtc.UtcDateTime);
        var expiryDate = definition.ExpiryDateUtc.HasValue
            ? DateOnly.FromDateTime(definition.ExpiryDateUtc.Value.UtcDateTime)
            : (DateOnly?)null;
        if (effectiveDate > assignmentDate || (expiryDate.HasValue && expiryDate.Value <= assignmentDate))
            throw new InvalidOperationException("Assignment definition is not active.");
        return definition;
    }

    private async Task ValidateAssignmentValuesAsync(
        int? locationId,
        int categoryId,
        int subCategoryId,
        AssignmentDefinition definition,
        CancellationToken cancellationToken
    )
    {
        if (locationId is not int requiredLocationId)
            throw new InvalidOperationException("An assignment location is required.");
        if (definition.LocationId != requiredLocationId)
            throw new InvalidOperationException("Assignment location must match the assignment definition location.");
        if (!await db.Locations.AnyAsync(item => item.Id == requiredLocationId, cancellationToken))
            throw new InvalidOperationException("Location does not exist.");
        if (!await db.StatCategories.AnyAsync(item => item.Id == categoryId && !item.IsArchived, cancellationToken))
            throw new InvalidOperationException("Category does not exist or is archived.");
        if (
            !await db.SubCategories.AnyAsync(
                item => item.Id == subCategoryId && item.CategoryId == categoryId,
                cancellationToken
            )
        )
            throw new InvalidOperationException("Subcategory does not exist or does not belong to the category.");
    }

    private async Task<IReadOnlyCollection<AssignmentSeriesResponse>> MapToAssignmentSeriesResponsesAsync(
        IReadOnlyCollection<AssignmentSeries> series,
        CancellationToken cancellationToken
    )
    {
        var entries = await LoadSeriesEntryIdsAsync(series, cancellationToken);
        return series
            .Select(item =>
                AssignmentResponseMapper.ToAssignmentSeriesResponse(item, entries.GetValueOrDefault(item.Id, []))
            )
            .ToList();
    }

    private async Task<AssignmentSeriesResponse> MapToAssignmentSeriesResponseAsync(
        AssignmentSeries series,
        CancellationToken cancellationToken
    )
    {
        var entries = await LoadSeriesEntryIdsAsync([series], cancellationToken);
        if (series.EventSeries is null)
            series.EventSeries = await db
                .EventSeries.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == series.EventSeriesId, cancellationToken);
        if (series.Category is null)
            series.Category = await db
                .StatCategories.AsNoTracking()
                .SingleAsync(item => item.Id == series.CategoryId, cancellationToken);
        if (series.SubCategory is null)
            series.SubCategory = await db
                .SubCategories.AsNoTracking()
                .SingleAsync(item => item.Id == series.SubCategoryId, cancellationToken);
        return AssignmentResponseMapper.ToAssignmentSeriesResponse(series, entries.GetValueOrDefault(series.Id, []));
    }

    private async Task<Dictionary<int, List<AssignmentSeriesEntryIds>>> LoadSeriesEntryIdsAsync(
        IReadOnlyCollection<AssignmentSeries> series,
        CancellationToken cancellationToken
    )
    {
        if (series.Count == 0)
            return [];
        var ids = series.Select(item => item.Id).ToList();
        var entries = await db
            .AssignmentEntries.AsNoTracking()
            .Where(entry => entry.AssignmentSeriesId.HasValue && ids.Contains(entry.AssignmentSeriesId.Value))
            .Where(entry => entry.Event != null && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled)
            .OrderBy(entry => entry.Id)
            .Select(entry => new AssignmentSeriesEntryIds(entry.AssignmentSeriesId!.Value, entry.Id, entry.EventId))
            .ToListAsync(cancellationToken);
        return entries
            .GroupBy(item => item.AssignmentSeriesId)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    private static void PropagateSeriesChangesToEntries(
        AssignmentSeries series,
        AssignmentSeriesRequest request,
        AssignmentSeriesUpdatePlan plan
    )
    {
        foreach (
            var entry in series.AssignmentEntries.Where(entry =>
                entry.Event?.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
            )
        )
        {
            var eventEntity = entry.Event!;
            if (eventEntity.Title == plan.PreviousValues.Title)
                eventEntity.Title = series.EventSeries!.Title;
            if (eventEntity.Description == plan.PreviousValues.Description)
                eventEntity.Description = series.EventSeries!.Description;
            if (eventEntity.Notes == plan.PreviousValues.Notes)
                eventEntity.Notes = series.EventSeries!.Notes;
            if (eventEntity.Color == plan.PreviousValues.Color)
                eventEntity.Color = series.EventSeries!.Color;
            if (eventEntity.LocationId == plan.PreviousValues.LocationId)
                eventEntity.LocationId = series.EventSeries!.LocationId;
            if (entry.AssignmentDefinitionId == plan.PreviousValues.AssignmentDefinitionId)
                entry.AssignmentDefinitionId = request.AssignmentDefinitionId;
            if (entry.Capacity == plan.PreviousValues.Capacity)
                entry.Capacity = series.Capacity;
            if (entry.CategoryId == plan.PreviousValues.CategoryId)
                entry.CategoryId = series.CategoryId;
            if (entry.SubCategoryId == plan.PreviousValues.SubCategoryId)
                entry.SubCategoryId = series.SubCategoryId;
        }
    }

    private static void ValidateAssignmentEventSeriesType(EventSeries eventSeries)
    {
        if (eventSeries.EventTypeCode != SchedulingConstants.AssignmentEventTypeCode)
            throw new InvalidOperationException($"Event series {eventSeries.Id} is not an assignment event series.");
    }
}
