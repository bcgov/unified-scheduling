using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.Common.Validation;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Mappings;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public sealed class AssignmentService(
    ILogger<AssignmentService> logger,
    UnifiedDbContext db,
    IEventSeriesMaterializationService eventSeriesMaterializationService,
    AssignmentSeriesMaterializationHandler assignmentSeriesMaterializationHandler,
    IShiftAssignmentService shiftAssignmentService,
    CalendarLifecycleService calendarLifecycleService,
    ITimeZoneService timeZoneService,
    TimeProvider timeProvider
) : IAssignmentService
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
        var definition = await GetActiveDefinitionAsync(
            request.AssignmentDefinitionId,
            request.StartAtUtc,
            cancellationToken
        );
        await ValidateAssignmentValuesAsync(
            request.LocationId,
            request.CategoryId,
            request.SubCategoryId,
            definition,
            cancellationToken
        );
        logger.LogInformation("Creating assignment series starting at {StartAtUtc}.", request.StartAtUtc);

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );

        var eventSeries = AssignmentEventMapper.ToEventSeries(request);
        db.EventSeries.Add(eventSeries);
        await db.SaveChangesAsync(cancellationToken);

        var assignmentSeries = new AssignmentSeries
        {
            EventSeriesId = eventSeries.Id,
            AssignmentDefinitionId = definition.Id,
            Capacity = request.Capacity,
            CategoryId = request.CategoryId,
            SubCategoryId = request.SubCategoryId,
        };

        db.AssignmentSeries.Add(assignmentSeries);
        await db.SaveChangesAsync(cancellationToken);

        await eventSeriesMaterializationService.MaterializeAsync(
            eventSeries,
            AssignmentRecurrenceValidationOptions,
            assignmentSeriesMaterializationHandler,
            new AssignmentSeriesMaterializationContext { AssignmentSeries = assignmentSeries },
            cancellationToken
        );
        await db.SaveChangesAsync(cancellationToken);

        await EnsureSeriesAssignmentsAreUniqueAsync(assignmentSeries.Id, cancellationToken);

        await shiftAssignmentService.ReplaceAssignmentSeriesLinksAsync(
            assignmentSeries.Id,
            request.ShiftSeriesLinks,
            cancellationToken
        );

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Created assignment series {AssignmentSeriesId}.", assignmentSeries.Id);
        return await MapToAssignmentSeriesResponseAsync(assignmentSeries, cancellationToken);
    }

    public async Task<AssignmentSeriesResponse?> UpdateAssignmentSeriesAsync(
        int id,
        AssignmentSeriesRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var definition = await GetActiveDefinitionAsync(
            request.AssignmentDefinitionId,
            request.StartAtUtc,
            cancellationToken
        );
        await ValidateAssignmentValuesAsync(
            request.LocationId,
            request.CategoryId,
            request.SubCategoryId,
            definition,
            cancellationToken
        );
        logger.LogInformation("Updating assignment series {AssignmentSeriesId}.", id);

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );

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
        EnsureDraft(assignmentSeries.EventSeries!.StatusTypeCode, "Assignment series");
        var updatePlan = AssignmentSeriesUpdatePlanner.CreatePlan(assignmentSeries, request);

        AssignmentEventMapper.ApplyToEventSeries(assignmentSeries.EventSeries!, request);
        assignmentSeries.AssignmentDefinitionId = request.AssignmentDefinitionId;
        assignmentSeries.Capacity = request.Capacity;
        assignmentSeries.CategoryId = request.CategoryId;
        assignmentSeries.SubCategoryId = request.SubCategoryId;

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
        {
            PropagateSeriesChangesToEntries(assignmentSeries, request, updatePlan);
        }

        await db.SaveChangesAsync(cancellationToken);

        await EnsureSeriesAssignmentsAreUniqueAsync(assignmentSeries.Id, cancellationToken);

        await shiftAssignmentService.ReplaceAssignmentSeriesLinksAsync(
            assignmentSeries.Id,
            request.ShiftSeriesLinks,
            cancellationToken
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
        var assignmentSeries = await db
            .AssignmentSeries.Include(series => series.EventSeries!)
                .ThenInclude(series => series.Events)
            .SingleOrDefaultAsync(series => series.Id == id, cancellationToken);
        if (assignmentSeries is null)
            return null;
        ValidateAssignmentEventSeriesType(assignmentSeries.EventSeries!);
        calendarLifecycleService.PublishSeries(
            assignmentSeries.EventSeries!,
            assignmentSeries.EventSeries!.Events.ToList()
        );
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Published assignment series {AssignmentSeriesId}.", id);
        return await MapToAssignmentSeriesResponseAsync(assignmentSeries, cancellationToken);
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
            timeProvider.GetUtcNow(),
            cancelledByUserId,
            request.CancellationReason
        );

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Expired assignment series {AssignmentSeriesId}.", id);
        return await MapToAssignmentSeriesResponseAsync(assignmentSeries, cancellationToken);
    }

    public async Task<bool> DeleteAssignmentSeriesAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Deleting assignment series {AssignmentSeriesId}.", id);
        var assignmentSeries = await db
            .AssignmentSeries.Include(series => series.EventSeries)
            .SingleOrDefaultAsync(series => series.Id == id, cancellationToken);
        if (assignmentSeries is null)
            return false;
        ValidateAssignmentEventSeriesType(assignmentSeries.EventSeries!);
        if (!calendarLifecycleService.CanDelete(assignmentSeries.EventSeries!))
            throw new InvalidOperationException("Assignment series can only be deleted while in draft status.");

        var eventSeries = assignmentSeries.EventSeries!;
        var childEvents = await db
            .Events.Where(eventEntity =>
                eventEntity.EventSeriesId == eventSeries.Id
                && eventEntity.EventTypeCode == SchedulingConstants.AssignmentEventTypeCode
                && eventEntity.SourceModule == SchedulingConstants.SourceModule
            )
            .ToListAsync(cancellationToken);
        var entries = await db
            .AssignmentEntries.Include(entry => entry.Event)
            .Where(entry => entry.AssignmentSeriesId == id)
            .ToListAsync(cancellationToken);
        var draftChildEvents = childEvents
            .Where(eventEntity => eventEntity.StatusTypeCode == CalendarEventStatusTypeCodes.Draft)
            .ToList();
        var draftChildEventIds = draftChildEvents.Select(eventEntity => eventEntity.Id).ToHashSet();
        var draftEntries = entries.Where(entry => draftChildEventIds.Contains(entry.EventId)).ToList();
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
        foreach (var retainedChildEvent in childEvents.Except(draftChildEvents))
            retainedChildEvent.EventSeriesId = null;
        var retainedShiftLinks = seriesLinks
            .SelectMany(link => link.EntryLinks)
            .Where(link => !draftEntryIds.Contains(link.AssignmentEntryId))
            .ToList();
        var suppressedShiftLinks = retainedShiftLinks.Where(link => link.Users.Count == 0).ToList();
        foreach (var retainedShiftLink in retainedShiftLinks.Except(suppressedShiftLinks))
        {
            retainedShiftLink.ShiftAssignmentSeriesLinkId = null;
            retainedShiftLink.ShiftAssignmentSeriesLink = null;
            retainedShiftLink.IsException = false;
        }

        var removedEntryLinks = draftEntryLinks.Concat(suppressedShiftLinks).ToList();
        db.ShiftAssignmentEntryUsers.RemoveRange(removedEntryLinks.SelectMany(link => link.Users));
        db.ShiftAssignmentEntries.RemoveRange(removedEntryLinks);
        db.ShiftAssignmentSeriesLinkUsers.RemoveRange(seriesLinks.SelectMany(link => link.Users));
        db.ShiftAssignmentSeriesLinks.RemoveRange(seriesLinks);
        db.AssignmentEntries.RemoveRange(draftEntries);
        db.Events.RemoveRange(draftChildEvents);
        db.AssignmentSeries.Remove(assignmentSeries);
        db.EventSeries.Remove(eventSeries);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted assignment series {AssignmentSeriesId}.", id);
        return true;
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
                && (
                    !rangeStart.HasValue
                    || (entry.Event.EndAtUtc.HasValue && entry.Event.EndAtUtc.Value > rangeStart.Value)
                )
            );
        }

        var results = await query
            .OrderBy(entry => entry.EventId)
            .ThenBy(entry => entry.Id)
            .ToListAsync(cancellationToken);
        return results.Select(AssignmentResponseMapper.ToAssignmentEntryResponse).ToList();
    }

    public async Task<AssignmentEntryResponse?> GetAssignmentEntryByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var result = await IncludeAssignmentEntryGraph(db.AssignmentEntries.AsNoTracking())
            .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);
        return result is null ? null : AssignmentResponseMapper.ToAssignmentEntryResponse(result);
    }

    public async Task<AssignmentEntryResponse> CreateAssignmentEntryAsync(
        AssignmentEntryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var definition = await GetActiveDefinitionAsync(
            request.AssignmentDefinitionId,
            request.StartAtUtc,
            cancellationToken
        );
        await ValidateAssignmentValuesAsync(
            request.LocationId,
            request.CategoryId,
            request.SubCategoryId,
            definition,
            cancellationToken
        );
        logger.LogInformation("Creating assignment entry starting at {StartAtUtc}.", request.StartAtUtc);

        var assignmentSeries = request.AssignmentSeriesId.HasValue
            ? await GetValidatedAssignmentSeriesAsync(request.AssignmentSeriesId.Value, cancellationToken)
            : null;

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );
        var eventEntity = AssignmentEventMapper.ToEvent(request, assignmentSeries?.EventSeriesId);
        CalendarEventExceptionHelper.UpdateExceptionFlag(eventEntity);
        await EnsureAssignmentsAreUniqueAsync(
            [CreateAssignmentUniquenessCandidate(definition.Id, eventEntity)],
            [],
            cancellationToken
        );
        db.Events.Add(eventEntity);
        await db.SaveChangesAsync(cancellationToken);

        var assignmentEntry = new AssignmentEntry
        {
            AssignmentSeriesId = request.AssignmentSeriesId,
            EventId = eventEntity.Id,
            AssignmentDefinitionId = definition.Id,
            Capacity = request.Capacity,
            CategoryId = request.CategoryId,
            SubCategoryId = request.SubCategoryId,
        };

        db.AssignmentEntries.Add(assignmentEntry);
        await db.SaveChangesAsync(cancellationToken);

        await shiftAssignmentService.ReplaceAssignmentEntryLinksAsync(
            assignmentEntry.Id,
            request.ShiftEntryLinks,
            cancellationToken
        );

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Created assignment entry {AssignmentEntryId}.", assignmentEntry.Id);
        return (await GetAssignmentEntryByIdAsync(assignmentEntry.Id, cancellationToken))!;
    }

    public async Task<AssignmentEntryResponse?> UpdateAssignmentEntryAsync(
        int id,
        AssignmentEntryUpdateRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var definition = await GetActiveDefinitionAsync(
            request.AssignmentDefinitionId,
            request.StartAtUtc,
            cancellationToken
        );
        await ValidateAssignmentValuesAsync(
            request.LocationId,
            request.CategoryId,
            request.SubCategoryId,
            definition,
            cancellationToken
        );
        logger.LogInformation("Updating assignment entry {AssignmentEntryId}.", id);

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );

        var assignmentEntry = await db
            .AssignmentEntries.Include(entry => entry.Event)
            .Include(entry => entry.ShiftAssignmentEntries)
                .ThenInclude(link => link.Users)
            .Include(entry => entry.ShiftAssignmentEntries)
                .ThenInclude(link => link.ShiftEntry)
                    .ThenInclude(shiftEntry => shiftEntry!.Event)
            .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);

        if (assignmentEntry is null)
            return null;

        ValidateAssignmentEventType(assignmentEntry.Event!);
        EnsureDraft(assignmentEntry.Event!.StatusTypeCode, "Assignment entry");
        ShiftAssignmentGuards.EnsureAssignmentEntryUpdatePreservesLinks(
            assignmentEntry,
            request.StartAtUtc,
            request.EndAtUtc,
            assignmentEntry.AssignmentSeriesId,
            request.ShiftEntryLinks.Select(link => link.ShiftEntryId).ToList()
        );
        AssignmentEventMapper.ApplyToEvent(assignmentEntry.Event!, request);
        CalendarEventExceptionHelper.UpdateExceptionFlag(assignmentEntry.Event!);
        assignmentEntry.AssignmentDefinitionId = request.AssignmentDefinitionId;
        assignmentEntry.Capacity = request.Capacity;
        assignmentEntry.CategoryId = request.CategoryId;
        assignmentEntry.SubCategoryId = request.SubCategoryId;

        await EnsureAssignmentsAreUniqueAsync(
            [CreateAssignmentUniquenessCandidate(assignmentEntry.AssignmentDefinitionId, assignmentEntry.Event!)],
            [assignmentEntry.Id],
            cancellationToken
        );

        await db.SaveChangesAsync(cancellationToken);

        await shiftAssignmentService.ReplaceAssignmentEntryLinksAsync(
            assignmentEntry.Id,
            request.ShiftEntryLinks,
            cancellationToken
        );

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Updated assignment entry {AssignmentEntryId}.", id);
        return await GetAssignmentEntryByIdAsync(assignmentEntry.Id, cancellationToken);
    }

    public async Task<AssignmentEntryResponse?> PublishAssignmentEntryAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Publishing assignment entry {AssignmentEntryId}.", id);
        var assignmentEntry = await IncludeAssignmentEntryGraph(db.AssignmentEntries)
            .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);
        if (assignmentEntry is null)
            return null;
        ValidateAssignmentEventType(assignmentEntry.Event!);
        calendarLifecycleService.Publish(assignmentEntry.Event!);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Published assignment entry {AssignmentEntryId}.", id);
        return AssignmentResponseMapper.ToAssignmentEntryResponse(assignmentEntry);
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
            timeProvider.GetUtcNow(),
            cancelledByUserId,
            request.CancellationReason
        );

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Expired assignment entry {AssignmentEntryId}.", id);
        return AssignmentResponseMapper.ToAssignmentEntryResponse(assignmentEntry);
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

        await EnsureAssignmentsAreUniqueAsync(
            entries
                .Select(entry => CreateAssignmentUniquenessCandidate(entry.AssignmentDefinitionId, entry.Event!))
                .ToList(),
            entries.Select(entry => entry.Id).ToList(),
            cancellationToken
        );
    }

    public async Task<bool> DeleteAssignmentEntryAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Deleting assignment entry {AssignmentEntryId}.", id);
        var assignmentEntry = await db
            .AssignmentEntries.Include(entry => entry.Event)
            .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);
        if (assignmentEntry is null)
            return false;
        ValidateAssignmentEventType(assignmentEntry.Event!);
        if (!calendarLifecycleService.CanDelete(assignmentEntry.Event!))
            throw new InvalidOperationException("Assignment entry can only be deleted while in draft status.");
        var links = await db
            .ShiftAssignmentEntries.Include(link => link.Users)
            .Where(link => link.AssignmentEntryId == id)
            .ToListAsync(cancellationToken);
        db.ShiftAssignmentEntryUsers.RemoveRange(links.SelectMany(link => link.Users));
        db.ShiftAssignmentEntries.RemoveRange(links);
        db.AssignmentEntries.Remove(assignmentEntry);
        db.Events.Remove(assignmentEntry.Event!);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted assignment entry {AssignmentEntryId}.", id);
        return true;
    }

    private async Task EnsureAssignmentsAreUniqueAsync(
        IReadOnlyCollection<AssignmentUniquenessCandidate> candidates,
        IReadOnlyCollection<int> excludedAssignmentEntryIds,
        CancellationToken cancellationToken
    )
    {
        if (candidates.Count == 0)
            return;

        var locationIds = candidates.Select(candidate => candidate.LocationId).Distinct().ToList();
        var locations = await db
            .Locations.AsNoTracking()
            .Where(location => locationIds.Contains(location.Id))
            .ToDictionaryAsync(
                location => location.Id,
                location => new AssignmentLocationDetails(location.Name, location.Timezone),
                cancellationToken
            );
        var locationTimeZones = locations.ToDictionary(
            pair => pair.Key,
            pair => timeZoneService.ResolveRequired(pair.Value.TimeZoneId)
        );
        var assignmentDefinitionIds = candidates
            .Select(candidate => candidate.AssignmentDefinitionId)
            .Distinct()
            .ToList();
        var assignmentDefinitionNames = await db
            .AssignmentDefinitions.AsNoTracking()
            .Where(definition => assignmentDefinitionIds.Contains(definition.Id))
            .ToDictionaryAsync(definition => definition.Id, definition => definition.Name, cancellationToken);

        var candidateKeys = candidates
            .Select(candidate =>
                CreateAssignmentUniquenessKey(candidate, locationTimeZones, locations, assignmentDefinitionNames)
            )
            .ToList();
        var duplicateCandidateKey = candidateKeys.GroupBy(key => key).FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateCandidateKey is AssignmentUniquenessKey duplicateKey)
            throw CreateDuplicateAssignmentException(duplicateKey);

        var dateRanges = candidateKeys
            .Select(key =>
                timeZoneService.ConvertInclusiveLocalDateRangeToUtcRange(
                    key.Date,
                    key.Date,
                    locationTimeZones[key.LocationId]
                )
            )
            .ToList();
        var earliestStartAtUtc = dateRanges.Min(range => range.StartAtUtc);
        var latestEndAtUtc = dateRanges.Max(range => range.EndAtUtc);

        var existingEntries = await db
            .AssignmentEntries.AsNoTracking()
            .Include(entry => entry.Event)
            .Where(entry => !excludedAssignmentEntryIds.Contains(entry.Id))
            .Where(entry => assignmentDefinitionIds.Contains(entry.AssignmentDefinitionId))
            .Where(entry =>
                entry.Event != null
                && entry.Event.LocationId.HasValue
                && locationIds.Contains(entry.Event.LocationId.Value)
                && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                && entry.Event.StartAtUtc >= earliestStartAtUtc
                && entry.Event.StartAtUtc < latestEndAtUtc
            )
            .ToListAsync(cancellationToken);

        var candidateKeySet = candidateKeys.ToHashSet();
        var conflictingKey = existingEntries
            .Select(entry =>
                CreateAssignmentUniquenessKey(
                    CreateAssignmentUniquenessCandidate(entry.AssignmentDefinitionId, entry.Event!),
                    locationTimeZones,
                    locations,
                    assignmentDefinitionNames
                )
            )
            .FirstOrDefault(candidateKeySet.Contains);

        if (conflictingKey != default)
            throw CreateDuplicateAssignmentException(conflictingKey);
    }

    private static AssignmentUniquenessCandidate CreateAssignmentUniquenessCandidate(
        int assignmentDefinitionId,
        Event eventEntity
    ) =>
        new(
            eventEntity.LocationId ?? throw new InvalidOperationException("An assignment location is required."),
            assignmentDefinitionId,
            eventEntity.StartAtUtc
        );

    private AssignmentUniquenessKey CreateAssignmentUniquenessKey(
        AssignmentUniquenessCandidate candidate,
        IReadOnlyDictionary<int, TimeZoneInfo> locationTimeZones,
        IReadOnlyDictionary<int, AssignmentLocationDetails> locations,
        IReadOnlyDictionary<int, string> assignmentDefinitionNames
    )
    {
        if (!locationTimeZones.TryGetValue(candidate.LocationId, out var timeZone))
            throw new InvalidOperationException($"Location {candidate.LocationId} does not exist.");

        var localStart = timeZoneService.ToLocalUnspecified(candidate.StartAtUtc, timeZone);
        return new AssignmentUniquenessKey(
            candidate.LocationId,
            locations[candidate.LocationId].Name,
            candidate.AssignmentDefinitionId,
            assignmentDefinitionNames[candidate.AssignmentDefinitionId],
            DateOnly.FromDateTime(localStart)
        );
    }

    private static ConflictValidationException CreateDuplicateAssignmentException(AssignmentUniquenessKey key) =>
        new(
            new Dictionary<string, string[]>
            {
                ["AssignmentDefinitionId"] =
                [
                    $"An assignment already exists for location {key.LocationName}, assignment definition {key.AssignmentDefinitionName}, and date {key.Date:yyyy-MM-dd}.",
                ],
            }
        );

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
        var entryIds = await LoadAssignmentSeriesEntryIdsAsync(assignmentSeries, cancellationToken);
        return assignmentSeries
            .Select(series =>
                AssignmentResponseMapper.ToAssignmentSeriesResponse(series, entryIds.GetValueOrDefault(series.Id, []))
            )
            .ToList();
    }

    private async Task<AssignmentSeriesResponse> MapToAssignmentSeriesResponseAsync(
        AssignmentSeries assignmentSeries,
        CancellationToken cancellationToken
    )
    {
        var entryIds = await LoadAssignmentSeriesEntryIdsAsync([assignmentSeries], cancellationToken);
        var seriesEntryIds = entryIds.GetValueOrDefault(assignmentSeries.Id, []);

        if (assignmentSeries.EventSeries is null)
            assignmentSeries.EventSeries = await db
                .EventSeries.AsNoTracking()
                .SingleOrDefaultAsync(series => series.Id == assignmentSeries.EventSeriesId, cancellationToken);
        if (assignmentSeries.Category is null)
            assignmentSeries.Category = await db
                .StatCategories.AsNoTracking()
                .SingleAsync(category => category.Id == assignmentSeries.CategoryId, cancellationToken);
        if (assignmentSeries.SubCategory is null)
            assignmentSeries.SubCategory = await db
                .SubCategories.AsNoTracking()
                .SingleAsync(subCategory => subCategory.Id == assignmentSeries.SubCategoryId, cancellationToken);

        return AssignmentResponseMapper.ToAssignmentSeriesResponse(assignmentSeries, seriesEntryIds);
    }

    private async Task<Dictionary<int, List<AssignmentSeriesEntryIds>>> LoadAssignmentSeriesEntryIdsAsync(
        IReadOnlyCollection<AssignmentSeries> assignmentSeries,
        CancellationToken cancellationToken
    )
    {
        if (assignmentSeries.Count == 0)
            return [];

        var assignmentSeriesIds = assignmentSeries.Select(series => series.Id).ToList();
        var entryIds = await db
            .AssignmentEntries.AsNoTracking()
            .Where(entry =>
                entry.AssignmentSeriesId.HasValue && assignmentSeriesIds.Contains(entry.AssignmentSeriesId.Value)
            )
            .Where(entry => entry.Event != null && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled)
            .OrderBy(entry => entry.Id)
            .Select(entry => new AssignmentSeriesEntryIds(entry.AssignmentSeriesId!.Value, entry.Id, entry.EventId))
            .ToListAsync(cancellationToken);

        return entryIds
            .GroupBy(entry => entry.AssignmentSeriesId)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    private static IQueryable<AssignmentEntry> IncludeAssignmentEntryGraph(IQueryable<AssignmentEntry> query) =>
        query
            .Include(entry => entry.Event)
            .Include(entry => entry.Category)
            .Include(entry => entry.SubCategory)
            .Include(entry => entry.ShiftAssignmentEntries)
                .ThenInclude(link => link.Users)
            .Include(entry => entry.ShiftAssignmentEntries)
                .ThenInclude(link => link.ShiftEntry)
                    .ThenInclude(shiftEntry => shiftEntry!.Event);

    private async Task<AssignmentDefinition> GetActiveDefinitionAsync(
        int id,
        DateTimeOffset assignmentStartAtUtc,
        CancellationToken cancellationToken
    )
    {
        var definition = await GetDefinitionAsync(id, cancellationToken);
        if (!IsAssignmentDefinitionActiveForAssignmentDate(definition, assignmentStartAtUtc))
            throw new InvalidOperationException("Assignment definition is not active.");
        return definition;
    }

    private bool IsAssignmentDefinitionActiveForAssignmentDate(
        AssignmentDefinition definition,
        DateTimeOffset assignmentStartAtUtc
    )
    {
        var locationTimeZone = timeZoneService.ResolveRequired(definition.Location.Timezone);
        var assignmentDate = DateOnly.FromDateTime(
            timeZoneService.ToLocalUnspecified(assignmentStartAtUtc, locationTimeZone)
        );
        var effectiveDate = DateOnly.FromDateTime(definition.EffectiveDateUtc.UtcDateTime);
        var expiryDate = definition.ExpiryDateUtc.HasValue
            ? DateOnly.FromDateTime(definition.ExpiryDateUtc.Value.UtcDateTime)
            : (DateOnly?)null;

        return effectiveDate <= assignmentDate && (!expiryDate.HasValue || expiryDate.Value > assignmentDate);
    }

    private async Task<AssignmentDefinition> GetDefinitionAsync(int id, CancellationToken cancellationToken) =>
        await db
            .AssignmentDefinitions.Include(definition => definition.Location)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new KeyNotFoundException($"Assignment definition {id} not found.");

    private static void PropagateSeriesChangesToEntries(
        AssignmentSeries assignmentSeries,
        AssignmentSeriesRequest request,
        AssignmentSeriesUpdatePlan updatePlan
    )
    {
        foreach (
            var entry in assignmentSeries.AssignmentEntries.Where(entry =>
                entry.Event?.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
            )
        )
        {
            ApplyEventSeriesFieldUpdatePreservingOverrides(
                entry.Event!,
                updatePlan.PreviousValues,
                assignmentSeries.EventSeries!
            );
            if (entry.AssignmentDefinitionId == updatePlan.PreviousValues.AssignmentDefinitionId)
                entry.AssignmentDefinitionId = request.AssignmentDefinitionId;
            if (entry.Capacity == updatePlan.PreviousValues.Capacity)
                entry.Capacity = assignmentSeries.Capacity;
            if (entry.CategoryId == updatePlan.PreviousValues.CategoryId)
                entry.CategoryId = assignmentSeries.CategoryId;
            if (entry.SubCategoryId == updatePlan.PreviousValues.SubCategoryId)
                entry.SubCategoryId = assignmentSeries.SubCategoryId;
        }
    }

    private static void ApplyEventSeriesFieldUpdatePreservingOverrides(
        Event eventEntity,
        AssignmentSeriesPreviousValues oldValues,
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

    private async Task ValidateAssignmentValuesAsync(
        int locationId,
        int categoryId,
        int subCategoryId,
        AssignmentDefinition definition,
        CancellationToken cancellationToken
    )
    {
        if (definition.LocationId != locationId)
            throw new InvalidOperationException("Assignment location must match the assignment definition location.");
        if (!await db.Locations.AnyAsync(location => location.Id == locationId, cancellationToken))
            throw new InvalidOperationException("Location does not exist.");
        if (
            !await db.StatCategories.AnyAsync(
                category => category.Id == categoryId && !category.IsArchived,
                cancellationToken
            )
        )
            throw new InvalidOperationException("Category does not exist or is archived.");
        if (
            !await db.SubCategories.AnyAsync(
                subCategory => subCategory.Id == subCategoryId && subCategory.CategoryId == categoryId,
                cancellationToken
            )
        )
            throw new InvalidOperationException("Subcategory does not exist or does not belong to the category.");
    }

    private static void EnsureDraft(string statusTypeCode, string entityName)
    {
        if (statusTypeCode != CalendarEventStatusTypeCodes.Draft)
            throw new InvalidOperationException($"{entityName} must be in draft status to edit.");
    }

    private readonly record struct AssignmentUniquenessCandidate(
        int LocationId,
        int AssignmentDefinitionId,
        DateTimeOffset StartAtUtc
    );

    private readonly record struct AssignmentLocationDetails(string Name, string TimeZoneId);

    private readonly record struct AssignmentUniquenessKey(
        int LocationId,
        string LocationName,
        int AssignmentDefinitionId,
        string AssignmentDefinitionName,
        DateOnly Date
    );
}
