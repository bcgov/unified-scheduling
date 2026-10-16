using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Calendar.Factories;
using Unified.Calendar.Services;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Leave;
using Unified.Leave.Constants;
using Unified.Leave.Factories;
using Unified.Leave.Mappings;
using Unified.Leave.Models;
using Unified.Leave.Models.Scheduling;

namespace Unified.Leave.Services;

/// <summary>
/// Scheduling leave query, create, update and delete operations that leverage the common <see cref="CalendarEventEntryService{TRequest,TEntity}"/>
/// and <see cref="CalendarEventSeriesService{TRequest,TEntity}"/> pipelines (both driven by
/// <see cref="Unified.Leave.Factories.LeaveEventFactory"/>, exposed together via
/// <see cref="LeaveServiceProvider"/>) to build the shared calendar Event/EventSeries models directly
/// from the common <c>CalendarEventEntryRequest</c>/<c>CalendarEventSeriesRequest</c> shaped requests,
/// while this service owns the leave specific details and persistence. Series updates are delegated to
/// <see cref="SchedulingLeaveSeriesFacade"/> and explicitly remove dropped series users (as Shift does).
/// </summary>
public sealed class SchedulingLeaveService(
    ILogger<SchedulingLeaveService> logger,
    UnifiedDbContext db,
    SchedulingLeaveEventMapper schedulingLeaveEventMapper,
    LeaveEventFactory eventFactory,
    LeaveServiceProvider serviceProvider,
    SchedulingLeaveSeriesFacade seriesFacade,
    CalendarLifecycleService calendarLifecycleService
) : ISchedulingLeaveService
{
    public async Task<SchedulingLeaveEntryResponse> CreateLeaveEntryAsync(
        SchedulingLeaveEntryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var entity = GetCalendarEventEntryService()
            .BuildEvent(request, LeaveConstants.SourceModule, LeaveConstants.LeaveEventTypeCode);

        db.LeaveEntries.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created leave entry {LeaveEntryId}.", entity.Id);
        return schedulingLeaveEventMapper.ToSchedulingLeaveEntryResponse(entity);
    }

    public async Task<SchedulingLeaveSeriesResponse> CreateLeaveSeriesAsync(
        SchedulingLeaveSeriesRequest request,
        CancellationToken cancellationToken = default
    )
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );

        var entity = GetCalendarEventSeriesService()
            .BuildEvent(request, LeaveConstants.SourceModule, LeaveConstants.LeaveEventTypeCode);
        db.LeaveSeries.Add(entity);
        await seriesFacade.MaterializeSeriesAsync(entity, request.UserIds.Distinct().ToList(), cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Created leave series {LeaveSeriesId}.", entity.Id);
        return MapLeaveSeriesResponse(entity);
    }

    public async Task<SchedulingLeaveEntryResponse?> GetLeaveEntryByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await LoadLeaveEntryAsync(id, tracked: false, cancellationToken);
        return entity is null ? null : schedulingLeaveEventMapper.ToSchedulingLeaveEntryResponse(entity);
    }

    public async Task<IReadOnlyList<SchedulingLeaveEntryResponse>> GetLeaveEntriesByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        var entities = await db
            .LeaveEntries.AsNoTracking()
            .Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .Where(entry =>
                entry.Users.Any(user => user.UserId == userId)
                || (
                    entry.LeaveSeriesId.HasValue
                    && db.LeaveSeriesUsers.Any(seriesUser =>
                        seriesUser.LeaveSeriesId == entry.LeaveSeriesId.Value && seriesUser.UserId == userId
                    )
                )
            )
            .OrderByDescending(entry => entry.Event!.StartAtUtc)
            .ToListAsync(cancellationToken);

        return entities.Select(schedulingLeaveEventMapper.ToSchedulingLeaveEntryResponse).ToList();
    }

    public async Task<SchedulingLeaveSeriesResponse?> GetLeaveSeriesByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug("Retrieving leave series {LeaveSeriesId}.", id);
        var entity = await LoadLeaveSeriesAsync(id, tracked: false, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Leave series {LeaveSeriesId} was not found.", id);
            return null;
        }
        return MapLeaveSeriesResponse(entity);
    }

    public async Task<IReadOnlyList<SchedulingLeaveSeriesResponse>> GetLeaveSeriesByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        var entities = await db
            .LeaveSeries.AsNoTracking()
            .Include(series => series.EventSeries!)
                .ThenInclude(eventSeries => eventSeries.Events)
            .Include(series => series.Users)
            .Include(series => series.LeaveEntries)
            .Where(series => series.Users.Any(user => user.UserId == userId))
            .OrderByDescending(series => series.EventSeries!.StartAtUtc)
            .ToListAsync(cancellationToken);

        return entities.Select(MapLeaveSeriesResponse).ToList();
    }

    public async Task<SchedulingLeaveEntryResponse?> UpdateLeaveEntryAsync(
        int id,
        SchedulingLeaveEntryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await LoadLeaveEntryAsync(id, tracked: true, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Leave entry {LeaveEntryId} was not found for update.", id);
            return null;
        }

        var updated = GetCalendarEventEntryService()
            .UpdateEvent(request, entity, LeaveConstants.SourceModule, LeaveConstants.LeaveEventTypeCode);
        if (updated is null)
            return null;

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated leave entry {LeaveEntryId}.", id);
        return schedulingLeaveEventMapper.ToSchedulingLeaveEntryResponse(updated);
    }

    public async Task<SchedulingLeaveSeriesResponse?> UpdateLeaveSeriesAsync(
        int id,
        SchedulingLeaveSeriesRequest request,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Updating leave series {LeaveSeriesId}.", id);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );

        var entity = await LoadLeaveSeriesAsync(id, tracked: true, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Leave series {LeaveSeriesId} was not found for update.", id);
            return null;
        }

        var eventSeries = entity.EventSeries!;
        var existingEntries = entity.LeaveEntries.ToList();
        var oldValues = seriesFacade.CaptureCopiedValues(eventSeries);
        var oldLeaveTypeId = entity.LeaveTypeId;
        var oldUserIds = entity.Users.Select(user => user.UserId).Distinct().Order().ToList();
        var recurrenceChanged = (
            (ICalendarEventSeriesFactory<SchedulingLeaveSeriesRequest, LeaveSeries>)eventFactory
        ).HasRecurrenceChanged(eventSeries, request);

        var newUserIds = request.UserIds.Distinct().ToList();

        // Must run before UpdateEvent mutates the tracked series so a rejected update leaves it untouched.
        if (recurrenceChanged)
            seriesFacade.EnsureCanRegenerate(existingEntries);

        // Captured before UpdateEvent, whose factory sync drops them from the tracked collection; they are
        // deleted explicitly (mirrors ShiftUserSync.SyncSeriesUsers) rather than relying on orphan deletion.
        var removedSeriesUsers = entity.Users.Where(user => !newUserIds.Contains(user.UserId)).ToList();

        var updated = GetCalendarEventSeriesService()
            .UpdateEvent(request, entity, LeaveConstants.SourceModule, LeaveConstants.LeaveEventTypeCode);
        if (updated is null)
            throw new InvalidOperationException("Failed to update leave series.");
        db.Set<LeaveSeriesUser>().RemoveRange(removedSeriesUsers);

        var newValues = seriesFacade.CaptureCopiedValues(eventSeries);

        if (recurrenceChanged)
        {
            await seriesFacade.RegenerateSeriesAsync(updated, newUserIds, existingEntries, cancellationToken);
        }
        else
        {
            foreach (var entry in GetActiveEntries(existingEntries))
            {
                seriesFacade.ApplyCopiedFieldUpdatesPreservingOverrides(entry.Event!, oldValues, newValues);
                if (entry.LeaveTypeId == oldLeaveTypeId)
                    entry.LeaveTypeId = request.LeaveTypeId;
                if (entry.Users.Select(user => user.UserId).Order().SequenceEqual(oldUserIds))
                    SyncEntryUsers(entry, newUserIds);
            }
        }

        ValidatePropagatedEntries(entity, GetActiveEntries(entity.LeaveEntries));

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Updated leave series {LeaveSeriesId}.", id);
        return MapLeaveSeriesResponse(updated);
    }

    private IReadOnlyCollection<LeaveEntry> GetActiveEntries(IEnumerable<LeaveEntry> entries) =>
        entries
            .Where(entry =>
                db.Entry(entry).State != EntityState.Deleted
                && entry.Event is not null
                && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
            )
            .ToList();

    // Removes dropped users explicitly (mirrors ShiftUserSync) instead of relying on orphan deletion.
    private void SyncEntryUsers(LeaveEntry entry, IReadOnlyCollection<Guid> userIds)
    {
        var requestedUserIds = userIds.Distinct().ToHashSet();
        db.Set<LeaveEntryUser>().RemoveRange(entry.Users.Where(user => !requestedUserIds.Contains(user.UserId)));
        LeaveEventFactory.SyncEntryUsers(entry, requestedUserIds.ToList());
    }

    private static void ValidatePropagatedEntries(LeaveSeries series, IEnumerable<LeaveEntry> entries)
    {
        foreach (var entry in entries)
        {
            if (entry.Users.Count == 0)
                throw new InvalidOperationException(
                    $"Leave series {series.Id} update would leave an entry without any users."
                );
        }
    }

    public async Task<bool> DeleteLeaveEntryAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Deleting leave entry {LeaveEntryId}.", id);
        var entity = await LoadLeaveEntryAsync(id, tracked: true, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Leave entry {LeaveEntryId} was not found for delete.", id);
            return false;
        }

        CalendarEventGuards.EnsureEventType(
            entity.Event!,
            LeaveConstants.LeaveEventTypeCode,
            LeaveConstants.SourceModule
        );
        // A single occurrence of a series may be deleted (as with shifts) as long as it is still a draft.
        if (!calendarLifecycleService.CanDelete(entity.Event!))
            throw new InvalidOperationException("Leave entry can only be deleted while in draft status.");

        db.Set<LeaveEntryUser>().RemoveRange(entity.Users);
        db.LeaveEntries.Remove(entity);
        db.Events.Remove(entity.Event!);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted leave entry {LeaveEntryId}.", id);
        return true;
    }

    public async Task<bool> DeleteLeaveSeriesAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Deleting leave series {LeaveSeriesId}.", id);
        var entity = await LoadLeaveSeriesAsync(id, tracked: true, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Leave series {LeaveSeriesId} was not found for delete.", id);
            return false;
        }

        var eventSeries = entity.EventSeries!;
        CalendarEventGuards.EnsureEventSeriesType(eventSeries, LeaveConstants.LeaveEventTypeCode);
        if (!GetCalendarEventSeriesService().IsDeletable(entity))
            throw new InvalidOperationException($"Leave series {id} can only be deleted while in draft status.");

        // Mirrors shift: draft occurrences are deleted; any non-draft occurrence is kept as a standalone entry.
        var draftEvents = eventSeries.Events.Where(e => calendarLifecycleService.CanDelete(e)).ToList();
        var draftEventIds = draftEvents.Select(e => e.Id).ToHashSet();
        var draftEntries = entity.LeaveEntries.Where(entry => draftEventIds.Contains(entry.EventId)).ToList();
        foreach (var retainedEntry in entity.LeaveEntries.Except(draftEntries))
            retainedEntry.LeaveSeriesId = null;
        foreach (var retainedEvent in eventSeries.Events.Except(draftEvents))
            retainedEvent.EventSeriesId = null;

        db.Set<LeaveEntryUser>().RemoveRange(draftEntries.SelectMany(entry => entry.Users));
        db.LeaveEntries.RemoveRange(draftEntries);
        db.Events.RemoveRange(draftEvents);
        db.LeaveSeriesUsers.RemoveRange(entity.Users);
        db.LeaveSeries.Remove(entity);
        db.EventSeries.Remove(eventSeries);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted leave series {LeaveSeriesId}.", id);
        return true;
    }

    private Task<LeaveEntry?> LoadLeaveEntryAsync(int id, bool tracked, CancellationToken cancellationToken)
    {
        var query = tracked ? db.LeaveEntries : db.LeaveEntries.AsNoTracking();
        return query
            .Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);
    }

    private Task<LeaveSeries?> LoadLeaveSeriesAsync(int id, bool tracked, CancellationToken cancellationToken)
    {
        var query = tracked ? db.LeaveSeries : db.LeaveSeries.AsNoTracking();
        return query
            .Include(series => series.EventSeries!)
                .ThenInclude(eventSeries => eventSeries.Events)
            .Include(series => series.Users)
            .Include(series => series.LeaveEntries)
                .ThenInclude(entry => entry.Event)
            .Include(series => series.LeaveEntries)
                .ThenInclude(entry => entry.Users)
            .SingleOrDefaultAsync(series => series.Id == id, cancellationToken);
    }

    private SchedulingLeaveSeriesResponse MapLeaveSeriesResponse(LeaveSeries series)
    {
        var eventIds = series.EventSeries?.Events.Select(eventEntity => eventEntity.Id).ToList() ?? [];
        var leaveEntryIds = series.LeaveEntries.Select(entry => entry.Id).ToList();
        return schedulingLeaveEventMapper.ToSchedulingLeaveSeriesResponse(series, eventIds, leaveEntryIds);
    }

    private ICalendarEventService<SchedulingLeaveEntryRequest, LeaveEntry> GetCalendarEventEntryService()
    {
        return serviceProvider.EntryService;
    }

    private ICalendarEventService<SchedulingLeaveSeriesRequest, LeaveSeries> GetCalendarEventSeriesService()
    {
        return serviceProvider.SeriesService;
    }
}
