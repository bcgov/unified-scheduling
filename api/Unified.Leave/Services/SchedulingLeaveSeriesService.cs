using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

public sealed class SchedulingLeaveSeriesService(
    ILogger<SchedulingLeaveSeriesService> logger,
    UnifiedDbContext db,
    SchedulingLeaveEventMapper schedulingLeaveEventMapper,
    ICalendarEventSeriesService<SchedulingLeaveSeriesRequest, LeaveSeries> calendarEventSeriesService,
    IEventSeriesMaterializationService materializer,
    LeaveSeriesMaterializationHandler materializationHandler
) : ISchedulingLeaveSeriesService
{
    private static readonly RecurrenceValidationOptions RecurrenceOptions = new()
    {
        MaximumDuration = TimeSpan.FromDays(365),
        MaximumOccurrences = 400,
        RequireBoundedRule = true,
    };

    public async Task<SchedulingLeaveSeriesResponse> CreateLeaveSeriesAsync(
        SchedulingLeaveSeriesRequest request,
        CancellationToken cancellationToken = default
    )
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );
        var entity = await calendarEventSeriesService.BuildEventAsync(
            request,
            LeaveConstants.SourceModule,
            LeaveConstants.LeaveEventTypeCode,
            cancellationToken: cancellationToken
        );
        db.LeaveSeries.Add(entity);
        await materializer.MaterializeAsync(
            entity.EventSeries!,
            RecurrenceOptions,
            materializationHandler,
            new LeaveSeriesMaterializationContext
            {
                LeaveSeries = entity,
                UserIds = request.UserIds.Distinct().ToList(),
            },
            cancellationToken
        );
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Created leave series {LeaveSeriesId}.", entity.Id);
        return await MapLeaveSeriesResponseAsync(entity, cancellationToken);
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
        return await MapLeaveSeriesResponseAsync(entity, cancellationToken);
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
        return await MapLeaveSeriesResponsesAsync(entities, cancellationToken);
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
        var oldValues = CaptureCopiedValues(eventSeries);
        var oldLeaveTypeId = entity.LeaveTypeId;
        var oldUserIds = entity.Users.Select(user => user.UserId).Distinct().Order().ToList();
        var recurrenceChanged = calendarEventSeriesService.HasRecurrenceChanged(entity, request);
        var newUserIds = request.UserIds.Distinct().ToList();
        var removedSeriesUsers = entity.Users.Where(user => !newUserIds.Contains(user.UserId)).ToList();

        var updated = await calendarEventSeriesService.UpdateEventAsync(
            request,
            entity,
            LeaveConstants.SourceModule,
            LeaveConstants.LeaveEventTypeCode,
            cancellationToken: cancellationToken
        );
        if (updated is null)
            throw new InvalidOperationException("Failed to update leave series.");
        db.Set<LeaveSeriesUser>().RemoveRange(removedSeriesUsers);
        var newValues = CaptureCopiedValues(eventSeries);
        if (recurrenceChanged)
        {
            await materializer.RegenerateDraftSeriesAsync(
                updated.EventSeries!,
                RecurrenceOptions,
                materializationHandler,
                new LeaveSeriesMaterializationContext
                {
                    LeaveSeries = updated,
                    UserIds = newUserIds,
                    ExistingEntries = existingEntries,
                },
                cancellationToken
            );
        }
        else
        {
            foreach (var entry in GetCurrentEntries(existingEntries))
            {
                ApplyCopiedFieldUpdatesPreservingOverrides(entry.Event!, oldValues, newValues);
                if (entry.LeaveTypeId == oldLeaveTypeId)
                    entry.LeaveTypeId = request.LeaveTypeId;
                if (entry.Users.Select(user => user.UserId).Order().SequenceEqual(oldUserIds))
                    SyncEntryUsers(entry, newUserIds);
            }
        }
        ValidatePropagatedEntries(entity, GetCurrentEntries(entity.LeaveEntries));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Updated leave series {LeaveSeriesId}.", id);
        return await MapLeaveSeriesResponseAsync(updated, cancellationToken);
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
        if (
            !calendarEventSeriesService.IsDeletable(
                entity,
                LeaveConstants.SourceModule,
                LeaveConstants.LeaveEventTypeCode
            )
        )
            throw new InvalidOperationException($"Leave series {id} can only be deleted while in draft status.");
        // Leave has no publish/cancel lifecycle: deleting a series removes all its occurrences.
        var entries = entity.LeaveEntries.ToList();
        var events = eventSeries.Events.Concat(entries.Select(entry => entry.Event!)).Distinct().ToList();
        db.Set<LeaveEntryUser>().RemoveRange(entries.SelectMany(entry => entry.Users));
        db.LeaveEntries.RemoveRange(entries);
        db.Events.RemoveRange(events);
        db.LeaveSeriesUsers.RemoveRange(entity.Users);
        db.LeaveSeries.Remove(entity);
        db.EventSeries.Remove(eventSeries);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted leave series {LeaveSeriesId}.", id);
        return true;
    }

    private IReadOnlyCollection<LeaveEntry> GetCurrentEntries(IEnumerable<LeaveEntry> entries) =>
        entries.Where(entry => db.Entry(entry).State != EntityState.Deleted && entry.Event is not null).ToList();

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

    private async Task<SchedulingLeaveSeriesResponse> MapLeaveSeriesResponseAsync(
        LeaveSeries series,
        CancellationToken cancellationToken
    ) => (await MapLeaveSeriesResponsesAsync([series], cancellationToken)).Single();

    private async Task<IReadOnlyList<SchedulingLeaveSeriesResponse>> MapLeaveSeriesResponsesAsync(
        IReadOnlyCollection<LeaveSeries> series,
        CancellationToken cancellationToken
    )
    {
        if (series.Count == 0)
            return [];

        // Query persisted occurrences so regenerated/deleted tracked entities cannot leak into responses.
        var seriesIds = series.Select(item => item.Id).ToList();
        var occurrences = await db
            .LeaveEntries.AsNoTracking()
            .Where(entry => entry.LeaveSeriesId.HasValue && seriesIds.Contains(entry.LeaveSeriesId.Value))
            .Where(entry => entry.Event != null)
            .OrderBy(entry => entry.Id)
            .Select(entry => new
            {
                SeriesId = entry.LeaveSeriesId!.Value,
                EntryId = entry.Id,
                entry.EventId,
            })
            .ToListAsync(cancellationToken);
        var bySeries = occurrences.ToLookup(entry => entry.SeriesId);

        return series
            .Select(item =>
            {
                var entries = bySeries[item.Id].ToList();
                return schedulingLeaveEventMapper.ToSchedulingLeaveSeriesResponse(
                    item,
                    entries.Select(entry => entry.EventId).ToList(),
                    entries.Select(entry => entry.EntryId).ToList()
                );
            })
            .ToList();
    }

    private static LeaveEventCopiedValues CaptureCopiedValues(EventSeries eventSeries)
    {
        ArgumentNullException.ThrowIfNull(eventSeries);

        return new LeaveEventCopiedValues(
            eventSeries.Title,
            eventSeries.Description,
            eventSeries.Notes,
            eventSeries.Color,
            eventSeries.LocationId
        );
    }

    private static void ApplyCopiedFieldUpdatesPreservingOverrides(
        Event eventEntity,
        LeaveEventCopiedValues oldValues,
        LeaveEventCopiedValues newValues
    )
    {
        ArgumentNullException.ThrowIfNull(eventEntity);
        ArgumentNullException.ThrowIfNull(oldValues);
        ArgumentNullException.ThrowIfNull(newValues);

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

    private sealed record LeaveEventCopiedValues(
        string Title,
        string? Description,
        string? Notes,
        string? Color,
        int? LocationId
    );
}
