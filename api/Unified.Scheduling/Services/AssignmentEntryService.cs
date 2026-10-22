using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Calendar.Conflicts;
using Unified.Calendar.Services;
using Unified.Common.Calendar.Conflicts;
using Unified.Common.Time;
using Unified.Common.Validation;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Mappings;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public sealed class AssignmentEntryService(
    ILogger<AssignmentEntryService> logger,
    UnifiedDbContext db,
    CalendarEventEntryService<AssignmentEntryRequest, AssignmentEntry> calendarEventEntryService,
    IShiftAssignmentService shiftAssignmentService,
    CalendarLifecycleService calendarLifecycleService,
    ITimeZoneService timeZoneService,
    TimeProvider timeProvider,
    ICalendarConflictService calendarConflictService
) : IAssignmentEntryService, IAssignmentPublicationService
{
    public async Task<IReadOnlyCollection<AssignmentEntryResponse>> GetAssignmentEntriesAsync(
        AssignmentEntryQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    )
    {
        IQueryable<AssignmentEntry> query = IncludeGraph(db.AssignmentEntries.AsNoTracking());
        if (queryParams?.AssignmentSeriesId is int seriesId)
            query = query.Where(entry => entry.AssignmentSeriesId == seriesId);
        if (queryParams?.EventId is int eventId)
            query = query.Where(entry => entry.EventId == eventId);
        if (queryParams?.LocationId is int locationId)
            query = query.Where(entry => entry.Event != null && entry.Event.LocationId == locationId);
        if (!string.IsNullOrWhiteSpace(queryParams?.StatusTypeCode))
        {
            var status = queryParams.StatusTypeCode.Trim();
            query = query.Where(entry => entry.Event != null && entry.Event.StatusTypeCode == status);
        }
        if (queryParams?.StartAtUtc.HasValue == true || queryParams?.EndAtUtc.HasValue == true)
        {
            var start = queryParams?.StartAtUtc;
            var end = queryParams?.EndAtUtc;
            query = query.Where(entry =>
                entry.Event != null
                && (!end.HasValue || entry.Event.StartAtUtc < end.Value)
                && (!start.HasValue || (entry.Event.EndAtUtc.HasValue && entry.Event.EndAtUtc.Value > start.Value))
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
        var result = await IncludeGraph(db.AssignmentEntries.AsNoTracking())
            .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);
        return result is null ? null : AssignmentResponseMapper.ToAssignmentEntryResponse(result);
    }

    public async Task<AssignmentEntryResponse> CreateAssignmentEntryAsync(
        AssignmentEntryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Creating assignment entry starting at {StartAtUtc}.", request.StartAtUtc);

        var assignmentSeries = request.AssignmentSeriesId.HasValue
            ? await GetValidatedSeriesAsync(request.AssignmentSeriesId.Value, cancellationToken)
            : null;

        var entry = await calendarEventEntryService.BuildEventAsync(
            request,
            SchedulingConstants.SourceModule,
            SchedulingConstants.AssignmentEventTypeCode,
            assignmentSeries?.EventSeriesId,
            validators:
            [
                async (input, ct) =>
                {
                    var definition = await GetActiveDefinitionAsync(input.AssignmentDefinitionId, input.StartAtUtc, ct);
                    await ValidateAssignmentValuesAsync(
                        input.LocationId,
                        input.CategoryId,
                        input.SubCategoryId,
                        definition,
                        ct
                    );
                },
            ],
            overrides: eventEntity =>
            {
                eventEntity.EventSeriesId = assignmentSeries?.EventSeriesId;
            },
            cancellationToken: cancellationToken
        );
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );

        await AssignmentUniquenessValidator.EnsureAssignmentsAreUniqueAsync(
            db,
            timeZoneService,
            [AssignmentUniquenessValidator.CreateCandidate(entry.AssignmentDefinitionId, entry.Event)],
            [],
            cancellationToken
        );
        db.AssignmentEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        await shiftAssignmentService.ReplaceAssignmentEntryLinksAsync(
            entry.Id,
            request.ShiftEntryLinks,
            cancellationToken
        );
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Created assignment entry {AssignmentEntryId}.", entry.Id);
        return (await GetAssignmentEntryByIdAsync(entry.Id, cancellationToken))!;
    }

    public async Task<AssignmentEntryResponse?> UpdateAssignmentEntryAsync(
        int id,
        AssignmentEntryUpdateRequest request,
        CancellationToken cancellationToken = default,
        Guid? conflictOverrideActorId = null
    )
    {
        logger.LogInformation("Updating assignment entry {AssignmentEntryId}.", id);

        var definition = await GetActiveDefinitionAsync(
            request.AssignmentDefinitionId,
            request.StartAtUtc,
            cancellationToken
        );

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

        List<ShiftAssignmentEntry> invalidatedShiftEntryLinks = [];
        List<ShiftEntryLinkRequest>? requestedShiftEntryLinks = null;
        IReadOnlyCollection<int> retainedShiftEntryIds = [];
        var updatedEntry = await calendarEventEntryService.UpdateEventAsync(
            request,
            assignmentEntry,
            SchedulingConstants.SourceModule,
            SchedulingConstants.AssignmentEventTypeCode,
            validators:
            [
                (input, ct) =>
                {
                    return ValidateAssignmentValuesAsync(
                        input.LocationId,
                        input.CategoryId,
                        input.SubCategoryId,
                        definition,
                        ct
                    );
                },
                (input, ct) =>
                {
                    invalidatedShiftEntryLinks = assignmentEntry
                        .ShiftAssignmentEntries.Where(link =>
                            link.ShiftEntry?.Event is Event shiftEvent
                            && !ShiftAssignmentGuards.AssignmentStartsOnShiftDate(
                                shiftEvent.StartAtUtc,
                                shiftEvent.EndAtUtc,
                                input.StartAtUtc,
                                shiftEvent.TimeZoneId,
                                timeZoneService
                            )
                        )
                        .ToList();
                    var invalidatedShiftEntryIds = invalidatedShiftEntryLinks
                        .Select(link => link.ShiftEntryId)
                        .ToHashSet();
                    requestedShiftEntryLinks = input
                        .ShiftEntryLinks?.Where(link => !invalidatedShiftEntryIds.Contains(link.ShiftEntryId))
                        .ToList();
                    retainedShiftEntryIds =
                        requestedShiftEntryLinks?.Select(link => link.ShiftEntryId).ToList()
                        ?? assignmentEntry
                            .ShiftAssignmentEntries.Where(link => !invalidatedShiftEntryIds.Contains(link.ShiftEntryId))
                            .Select(link => link.ShiftEntryId)
                            .ToList();
                    ShiftAssignmentGuards.EnsureAssignmentEntryUpdatePreservesLinks(
                        assignmentEntry,
                        input.StartAtUtc,
                        assignmentEntry.AssignmentSeriesId,
                        timeZoneService,
                        retainedShiftEntryIds
                    );
                    ct.ThrowIfCancellationRequested();
                    return Task.CompletedTask;
                },
            ],
            cancellationToken: cancellationToken
        );
        if (updatedEntry is null)
            return null;
        assignmentEntry = updatedEntry;

        await AssignmentUniquenessValidator.EnsureAssignmentsAreUniqueAsync(
            db,
            timeZoneService,
            [AssignmentUniquenessValidator.CreateCandidate(definition.Id, assignmentEntry.Event!)],
            [assignmentEntry.Id],
            cancellationToken
        );

        await db.SaveChangesAsync(cancellationToken);

        if (request.ShiftEntryLinks is null)
        {
            foreach (var invalidatedLink in invalidatedShiftEntryLinks)
                await shiftAssignmentService.DeleteShiftEntryLinkAsync(invalidatedLink.Id, cancellationToken);
        }

        await shiftAssignmentService.ReplaceAssignmentEntryLinksAsync(
            assignmentEntry.Id,
            requestedShiftEntryLinks,
            cancellationToken
        );
        var updatedConflictState = await SchedulingConflictParticipantProvider.GetParticipantsForAssignmentEntriesAsync(
            db,
            [assignmentEntry.Id],
            cancellationToken
        );
        await calendarConflictService.ValidateAndApplyConflictAcknowledgementsAsync(
            updatedConflictState,
            request.ConflictOverrides,
            conflictOverrideActorId,
            cancellationToken
        );
        await calendarConflictService.InvalidateResolvedOverridesAsync(
            [new CalendarConflictEventIdentity(SchedulingConstants.SourceModule, assignmentEntry.EventId.ToString())],
            cancellationToken: cancellationToken
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
        var entry = (await PublishEntriesAsync([id], cancellationToken)).SingleOrDefault();
        if (entry is null)
            return null;
        logger.LogInformation("Published assignment entry {AssignmentEntryId}.", id);
        return AssignmentResponseMapper.ToAssignmentEntryResponse(entry);
    }

    public async Task<IReadOnlyCollection<AssignmentEntry>> PublishEntriesAsync(
        IReadOnlyCollection<int> ids,
        CancellationToken cancellationToken = default
    )
    {
        if (ids.Count == 0)
            return [];
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        var entries = await IncludeGraph(db.AssignmentEntries)
            .Where(entry => ids.Contains(entry.Id))
            .ToListAsync(cancellationToken);
        foreach (var entry in entries)
        {
            ValidateEventType(entry.Event!);
            calendarLifecycleService.Publish(entry.Event!);
        }
        await db.SaveChangesAsync(cancellationToken);
        var conflicts = await SchedulingConflictParticipantProvider.GetParticipantsForAssignmentEntriesAsync(
            db,
            entries.Select(entry => entry.Id).ToList(),
            cancellationToken
        );
        await calendarConflictService.EnsureNoUnresolvedConflictsAsync(conflicts, cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return entries;
    }

    public async Task<AssignmentEntryResponse?> ExpireAssignmentEntryAsync(
        int id,
        ExpireShiftRequest request,
        Guid? cancelledByUserId = null,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Expiring assignment entry {AssignmentEntryId}.", id);
        var entry = await IncludeGraph(db.AssignmentEntries)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entry is null)
            return null;
        ValidateEventType(entry.Event!);
        calendarLifecycleService.Cancel(
            entry.Event!,
            timeProvider.GetUtcNow(),
            cancelledByUserId,
            request.CancellationReason
        );
        await db.SaveChangesAsync(cancellationToken);
        await calendarConflictService.InvalidateResolvedOverridesAsync(
            [new CalendarConflictEventIdentity(SchedulingConstants.SourceModule, entry.EventId.ToString())],
            updatedById: cancelledByUserId,
            cancellationToken: cancellationToken
        );
        logger.LogInformation("Expired assignment entry {AssignmentEntryId}.", id);
        return AssignmentResponseMapper.ToAssignmentEntryResponse(entry);
    }

    public async Task<bool> DeleteAssignmentEntryAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Deleting assignment entry {AssignmentEntryId}.", id);
        var entry = await db
            .AssignmentEntries.Include(item => item.Event)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        var isDeletable = calendarEventEntryService.IsDeletable(
            entry,
            SchedulingConstants.SourceModule,
            SchedulingConstants.AssignmentEventTypeCode
        );
        if (!isDeletable)
            return false;
        var links = await db
            .ShiftAssignmentEntries.Include(link => link.Users)
            .Where(link => link.AssignmentEntryId == id)
            .ToListAsync(cancellationToken);
        db.ShiftAssignmentEntryUsers.RemoveRange(links.SelectMany(link => link.Users));
        db.ShiftAssignmentEntries.RemoveRange(links);
        db.AssignmentEntries.Remove(entry);
        db.Events.Remove(entry.Event!);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted assignment entry {AssignmentEntryId}.", id);
        return true;
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
        var zone = timeZoneService.ResolveRequired(definition.Location.Timezone);
        var date = DateOnly.FromDateTime(timeZoneService.ToLocalUnspecified(startAtUtc, zone));
        var effective = DateOnly.FromDateTime(definition.EffectiveDateUtc.UtcDateTime);
        var expiry = definition.ExpiryDateUtc.HasValue
            ? DateOnly.FromDateTime(definition.ExpiryDateUtc.Value.UtcDateTime)
            : (DateOnly?)null;
        if (effective > date || (expiry.HasValue && expiry.Value <= date))
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

    private async Task<AssignmentSeries> GetValidatedSeriesAsync(int id, CancellationToken cancellationToken)
    {
        var series =
            await db
                .AssignmentSeries.Include(item => item.EventSeries)
                .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Assignment series {id} not found.");
        if (series.EventSeries!.EventTypeCode != SchedulingConstants.AssignmentEventTypeCode)
            throw new InvalidOperationException(
                $"Event series {series.EventSeriesId} is not an assignment event series."
            );
        return series;
    }

    private static IQueryable<AssignmentEntry> IncludeGraph(IQueryable<AssignmentEntry> query) =>
        query
            .Include(entry => entry.Event)
            .Include(entry => entry.Category)
            .Include(entry => entry.SubCategory)
            .Include(entry => entry.ShiftAssignmentEntries)
                .ThenInclude(link => link.Users)
            .Include(entry => entry.ShiftAssignmentEntries)
                .ThenInclude(link => link.ShiftEntry)
                    .ThenInclude(shift => shift!.Event);

    private static void ValidateEventType(Event eventEntity)
    {
        if (eventEntity.EventTypeCode != SchedulingConstants.AssignmentEventTypeCode)
            throw new InvalidOperationException($"Event {eventEntity.Id} is not an assignment event.");
        if (eventEntity.SourceModule != SchedulingConstants.SourceModule)
            throw new InvalidOperationException($"Event {eventEntity.Id} is not owned by Scheduling.");
    }
}
