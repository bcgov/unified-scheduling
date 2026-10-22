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

public sealed class ShiftEntryService(
    ILogger<ShiftEntryService> logger,
    UnifiedDbContext db,
    CalendarEventEntryService<ShiftEntryRequest, ShiftEntry> calendarEventEntryService,
    IShiftSeriesService shiftSeriesService,
    IShiftAssignmentService shiftAssignmentService,
    CalendarLifecycleService calendarLifecycleService,
    ITimeZoneService timeZoneService,
    TimeProvider timeProvider,
    ICalendarConflictService calendarConflictService,
    ShiftConflictService shiftConflictService
) : IShiftEntryService, IShiftPublicationService
{
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
        var result = await LoadShiftEntry(id, asNoTracking: true, cancellationToken);
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
        logger.LogInformation(
            "Creating shift entry for shift series {ShiftSeriesId} and users {UserIds} starting at {StartAtUtc}.",
            request.ShiftSeriesId,
            string.Join(",", userIds),
            request.StartAtUtc
        );

        var shiftSeries = request.ShiftSeriesId.HasValue
            ? await GetValidatedShiftSeriesAsync(request.ShiftSeriesId.Value, cancellationToken)
            : null;

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );
        var entity = await calendarEventEntryService.BuildEventAsync(
            request with
            {
                UserIds = userIds,
            },
            SchedulingConstants.SourceModule,
            SchedulingConstants.ShiftEventTypeCode,
            shiftSeries?.EventSeriesId,
            cancellationToken: cancellationToken
        );
        entity.ShiftSeries = shiftSeries;
        db.ShiftEntries.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await shiftAssignmentService.ReplaceShiftEntryLinksAsync(
            entity.Id,
            request.AssignmentEntryLinks ?? [],
            cancellationToken
        );
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Created shift entry {ShiftEntryId}.", entity.Id);
        return ShiftResponseMapper.ToShiftEntryResponse(entity);
    }

    public async Task<ShiftEntryResponse?> UpdateShiftEntryAsync(
        int id,
        ShiftEntryUpdateRequest request,
        CancellationToken cancellationToken = default,
        Guid? conflictOverrideActorId = null
    )
    {
        logger.LogInformation("Updating shift entry {ShiftEntryId}.", id);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );
        var entity = await LoadShiftEntry(id, asNoTracking: false, cancellationToken);
        if (entity is null)
            return null;

        var shiftSeries = request.ShiftSeriesId.HasValue
            ? await GetValidatedShiftSeriesAsync(request.ShiftSeriesId.Value, cancellationToken)
            : null;

        var updated = await calendarEventEntryService.UpdateEventAsync(
            request,
            entity,
            SchedulingConstants.SourceModule,
            SchedulingConstants.ShiftEventTypeCode,
            validators:
            [
                (input, _) =>
                {
                    ShiftAssignmentGuards.EnsureShiftEntryUpdatePreservesLinks(
                        entity,
                        input.StartAtUtc,
                        input.EndAtUtc,
                        input.TimeZoneId,
                        input.UserIds,
                        input.ShiftSeriesId,
                        timeZoneService,
                        input.AssignmentEntryLinks?.Select(link => link.AssignmentEntryId).ToList()
                    );
                    return Task.CompletedTask;
                },
            ],
            additionalGuards: input =>
            {
                entity.Event!.EventSeriesId = shiftSeries?.EventSeriesId;
                entity.ShiftSeriesId = input.ShiftSeriesId;
            },
            cancellationToken: cancellationToken
        );
        if (updated is null)
            return null;
        entity = updated;

        await db.SaveChangesAsync(cancellationToken);
        if (request.AssignmentEntryLinks is not null)
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
        await calendarConflictService.ValidateAndApplyConflictAcknowledgementsAsync(
            conflictCandidates,
            request.ConflictOverrides,
            conflictOverrideActorId,
            cancellationToken
        );
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Updated shift entry {ShiftEntryId}.", id);
        return ShiftResponseMapper.ToShiftEntryResponse(entity);
    }

    public async Task<ShiftEntryResponse?> PublishShiftEntryAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Publishing shift entry {ShiftEntryId}.", id);
        var result = await PublishEntriesAsync([id], cancellationToken);
        var entity = result.ShiftEntries.SingleOrDefault();
        if (entity is null)
        {
            logger.LogInformation("Shift entry {ShiftEntryId} was not found for publish.", id);
            return null;
        }
        logger.LogInformation("Published shift entry {ShiftEntryId}.", id);
        return ShiftResponseMapper.ToShiftEntryResponse(entity);
    }

    public async Task<ShiftPublicationResult> PublishEntriesAsync(
        IReadOnlyCollection<int> shiftEntryIds,
        CancellationToken cancellationToken = default
    )
    {
        if (shiftEntryIds.Count == 0)
            return new ShiftPublicationResult([], []);

        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        var entries = await LoadPublicationEntriesAsync(shiftEntryIds, cancellationToken);
        var blockers = await GetEntryPublicationBlockersAsync(entries, cancellationToken);
        if (blockers.Count > 0)
            throw new ConflictValidationException(
                new Dictionary<string, string[]> { ["UserIds"] = blockers.Select(blocker => blocker.Message).ToArray() }
            );

        foreach (var entry in entries)
            calendarLifecycleService.Publish(entry.Event!);

        var linkedAssignmentEventIds = await PublishLinkedDraftAssignmentsAsync(
            entries.Select(entry => entry.Id).ToList(),
            cancellationToken
        );
        await db.SaveChangesAsync(cancellationToken);
        var conflictCandidates = await SchedulingConflictParticipantProvider.GetParticipantsForShiftEntriesAsync(
            db,
            entries.Select(entry => entry.Id).ToList(),
            cancellationToken
        );
        await calendarConflictService.EnsureNoUnresolvedConflictsAsync(conflictCandidates, cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return new ShiftPublicationResult(entries, linkedAssignmentEventIds);
    }

    public async Task<IReadOnlyCollection<ShiftPublicationBlocker>> GetEntryPublicationBlockersAsync(
        IReadOnlyCollection<int> shiftEntryIds,
        CancellationToken cancellationToken = default
    )
    {
        var entries = await LoadPublicationEntriesAsync(shiftEntryIds, cancellationToken);
        return await GetEntryPublicationBlockersAsync(entries, cancellationToken);
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
        var linkedAssignmentEventIds = await LoadLinkedAssignmentEventIdsAsync([id], cancellationToken);
        ShiftGuards.EnsureShiftEventType(entity.Event!);
        calendarLifecycleService.Cancel(
            entity.Event!,
            timeProvider.GetUtcNow(),
            cancelledByUserId,
            request.CancellationReason
        );
        await db.SaveChangesAsync(cancellationToken);
        await InvalidateLinkedOverridesAsync(linkedAssignmentEventIds, cancelledByUserId, cancellationToken);
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
        if (
            !calendarEventEntryService.IsDeletable(
                entity,
                SchedulingConstants.SourceModule,
                SchedulingConstants.ShiftEventTypeCode
            )
        )
            return false;
        var eventEntity = entity.Event!;
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

    private async Task<ShiftEntry?> LoadShiftEntry(int id, bool asNoTracking, CancellationToken cancellationToken)
    {
        IQueryable<ShiftEntry> query = db.ShiftEntries;
        if (asNoTracking)
            query = query.AsNoTracking();
        return await query
            .Include(shiftEntry => shiftEntry.Users)
            .Include(shiftEntry => shiftEntry.Event)
            .Include(shiftEntry => shiftEntry.ShiftAssignmentEntries)
                .ThenInclude(link => link.Users)
            .Include(shiftEntry => shiftEntry.ShiftAssignmentEntries)
                .ThenInclude(link => link.AssignmentEntry)
                    .ThenInclude(entry => entry!.Event)
            .Include(shiftEntry => shiftEntry.ShiftAssignmentEntries)
                .ThenInclude(link => link.ShiftAssignmentSeriesLink)
            .SingleOrDefaultAsync(shiftEntry => shiftEntry.Id == id, cancellationToken);
    }

    private async Task<ShiftSeries> GetValidatedShiftSeriesAsync(int id, CancellationToken cancellationToken)
    {
        var shiftSeries = await shiftSeriesService.GetShiftSeriesEntityForEntryAsync(id, cancellationToken);
        if (shiftSeries is null)
            throw new KeyNotFoundException($"Shift series {id} not found.");
        ShiftGuards.EnsureShiftEventSeriesType(shiftSeries.EventSeries!);
        return shiftSeries;
    }

    private async Task<List<ShiftEntry>> LoadPublicationEntriesAsync(
        IReadOnlyCollection<int> ids,
        CancellationToken cancellationToken
    ) =>
        await db
            .ShiftEntries.Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .Where(entry => ids.Contains(entry.Id))
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyCollection<ShiftPublicationBlocker>> GetEntryPublicationBlockersAsync(
        IReadOnlyCollection<ShiftEntry> entries,
        CancellationToken cancellationToken
    )
    {
        foreach (var entry in entries)
            ShiftGuards.EnsureShiftEventType(entry.Event!);
        try
        {
            await shiftConflictService.EnsureShiftsDoNotConflictAsync(
                entries,
                entries.Select(entry => entry.Id).ToList(),
                cancellationToken
            );
            return [];
        }
        catch (ConflictValidationException exception)
        {
            return exception
                .Errors.Values.SelectMany(messages => messages)
                .Distinct()
                .Select(message => new ShiftPublicationBlocker(message))
                .ToList();
        }
    }

    private async Task<IReadOnlyCollection<int>> PublishLinkedDraftAssignmentsAsync(
        IReadOnlyCollection<int> ids,
        CancellationToken cancellationToken
    )
    {
        var events = await db
            .ShiftAssignmentEntries.Where(link => ids.Contains(link.ShiftEntryId))
            .Select(link => link.AssignmentEntry!.Event!)
            .Where(eventEntity => eventEntity.StatusTypeCode == CalendarEventStatusTypeCodes.Draft)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var eventEntity in events)
            calendarLifecycleService.Publish(eventEntity);
        return events.Select(eventEntity => eventEntity.Id).ToList();
    }

    private async Task<IReadOnlyCollection<int>> LoadLinkedAssignmentEventIdsAsync(
        IReadOnlyCollection<int> shiftEntryIds,
        CancellationToken cancellationToken
    )
    {
        if (shiftEntryIds.Count == 0)
            return [];
        return await db
            .ShiftAssignmentEntries.Where(link => shiftEntryIds.Contains(link.ShiftEntryId))
            .Select(link => link.AssignmentEntry!.EventId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

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

    private void RemoveShiftAssignmentLinks(IReadOnlyCollection<ShiftAssignmentEntry> links)
    {
        db.ShiftAssignmentEntryUsers.RemoveRange(links.SelectMany(link => link.Users));
        db.ShiftAssignmentEntries.RemoveRange(links);
    }
}
