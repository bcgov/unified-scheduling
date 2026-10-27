using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Calendar.Services;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.LoanOut;
using Unified.LoanOut.Constants;
using Unified.LoanOut.Factories;
using Unified.LoanOut.Mappers;
using Unified.LoanOut.Models;
using Unified.Scheduling.Services;

namespace Unified.LoanOut.Services;

public sealed class LoanOutSeriesService(
    ILogger<LoanOutSeriesService> logger,
    UnifiedDbContext db,
    LoanOutEventMapper loanOutEventMapper,
    ICalendarEventSeriesService<LoanOutSeriesRequest, LoanOutSeries> calendarEventSeriesService,
    IEventSeriesMaterializationService materializer,
    LoanOutSeriesMaterializationHandler materializationHandler,
    ISchedulingLoanOutService? schedulingLoanOutService = null
) : ILoanOutSeriesService
{
    private static readonly RecurrenceValidationOptions RecurrenceOptions = new()
    {
        MaximumDuration = TimeSpan.FromDays(365),
        MaximumOccurrences = 400,
        RequireBoundedRule = true,
    };

    public async Task<LoanOutSeriesResponse> CreateLoanOutSeriesAsync(
        LoanOutSeriesRequest request,
        CancellationToken cancellationToken = default
    )
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );
        var entity = await calendarEventSeriesService.BuildEventAsync(
            request,
            LoanOutConstants.SourceModule,
            LoanOutConstants.LoanOutEventTypeCode,
            cancellationToken: cancellationToken
        );
        db.LoanOutSeries.Add(entity);
        await materializer.MaterializeAsync(
            entity.EventSeries!,
            RecurrenceOptions,
            materializationHandler,
            new LoanOutSeriesMaterializationContext
            {
                LoanOutSeries = entity,
                UserIds = request.UserIds.Distinct().ToList(),
            },
            cancellationToken
        );
        await db.SaveChangesAsync(cancellationToken);
        await TransferRelatedShiftsAsync(entity, cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Created loan out series {LoanOutSeriesId}.", entity.Id);
        return await MapLoanOutSeriesResponseAsync(entity, cancellationToken);
    }

    public async Task<LoanOutSeriesResponse?> GetLoanOutSeriesByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug("Retrieving loan out series {LoanOutSeriesId}.", id);
        var entity = await LoadLoanOutSeriesAsync(id, tracked: false, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Loan out series {LoanOutSeriesId} was not found.", id);
            return null;
        }
        return await MapLoanOutSeriesResponseAsync(entity, cancellationToken);
    }

    public async Task<IReadOnlyList<LoanOutSeriesResponse>> GetLoanOutSeriesByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        var entities = await db
            .LoanOutSeries.AsNoTracking()
            .Include(series => series.EventSeries!)
                .ThenInclude(eventSeries => eventSeries.Events)
            .Include(series => series.Users)
            .Include(series => series.LoanOutEntries)
            .Where(series => series.Users.Any(user => user.UserId == userId))
            .OrderByDescending(series => series.EventSeries!.StartAtUtc)
            .ToListAsync(cancellationToken);
        return await MapLoanOutSeriesResponsesAsync(entities, cancellationToken);
    }

    public async Task<LoanOutSeriesResponse?> UpdateLoanOutSeriesAsync(
        int id,
        LoanOutSeriesRequest request,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Updating loan out series {LoanOutSeriesId}.", id);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );
        var entity = await LoadLoanOutSeriesAsync(id, tracked: true, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Loan out series {LoanOutSeriesId} was not found for update.", id);
            return null;
        }
        var eventSeries = entity.EventSeries!;
        var existingEntries = entity.LoanOutEntries.ToList();
        var oldValues = CaptureCopiedValues(eventSeries);
        var oldLoanOutLocationId = entity.LoanOutLocationId;
        var oldUserIds = entity.Users.Select(user => user.UserId).Distinct().Order().ToList();
        var recurrenceChanged = calendarEventSeriesService.HasRecurrenceChanged(entity, request);
        var newUserIds = request.UserIds.Distinct().ToList();
        var removedSeriesUsers = entity.Users.Where(user => !newUserIds.Contains(user.UserId)).ToList();

        var updated = await calendarEventSeriesService.UpdateEventAsync(
            request,
            entity,
            LoanOutConstants.SourceModule,
            LoanOutConstants.LoanOutEventTypeCode,
            cancellationToken: cancellationToken
        );
        if (updated is null)
            throw new InvalidOperationException("Failed to update loan out series.");
        db.Set<LoanOutSeriesUser>().RemoveRange(removedSeriesUsers);
        var newValues = CaptureCopiedValues(eventSeries);
        if (recurrenceChanged)
        {
            await materializer.RegenerateDraftSeriesAsync(
                updated.EventSeries!,
                RecurrenceOptions,
                materializationHandler,
                new LoanOutSeriesMaterializationContext
                {
                    LoanOutSeries = updated,
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
                if (entry.LoanOutLocationId == oldLoanOutLocationId)
                    entry.LoanOutLocationId = request.LoanOutLocationId;
                if (entry.Users.Select(user => user.UserId).Order().SequenceEqual(oldUserIds))
                    SyncEntryUsers(entry, newUserIds);
            }
        }
        ValidatePropagatedEntries(entity, GetCurrentEntries(entity.LoanOutEntries));
        await db.SaveChangesAsync(cancellationToken);
        await TransferRelatedShiftsAsync(entity, cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Updated loan out series {LoanOutSeriesId}.", id);
        return await MapLoanOutSeriesResponseAsync(updated, cancellationToken);
    }

    public async Task<bool> DeleteLoanOutSeriesAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Deleting loan out series {LoanOutSeriesId}.", id);
        var entity = await LoadLoanOutSeriesAsync(id, tracked: true, cancellationToken);
        if (entity is null)
        {
            logger.LogInformation("Loan out series {LoanOutSeriesId} was not found for delete.", id);
            return false;
        }
        var eventSeries = entity.EventSeries!;
        if (
            !calendarEventSeriesService.IsDeletable(
                entity,
                LoanOutConstants.SourceModule,
                LoanOutConstants.LoanOutEventTypeCode
            )
        )
            throw new InvalidOperationException($"Loan out series {id} can only be deleted while in draft status.");
        // LoanOut has no publish/cancel lifecycle: deleting a series removes all its occurrences.
        var entries = entity.LoanOutEntries.ToList();
        var events = eventSeries.Events.Concat(entries.Select(entry => entry.Event!)).Distinct().ToList();
        db.Set<LoanOutEntryUser>().RemoveRange(entries.SelectMany(entry => entry.Users));
        db.LoanOutEntries.RemoveRange(entries);
        db.Events.RemoveRange(events);
        db.LoanOutSeriesUsers.RemoveRange(entity.Users);
        db.LoanOutSeries.Remove(entity);
        db.EventSeries.Remove(eventSeries);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted loan out series {LoanOutSeriesId}.", id);
        return true;
    }

    private IReadOnlyCollection<LoanOutEntry> GetCurrentEntries(IEnumerable<LoanOutEntry> entries) =>
        entries.Where(entry => db.Entry(entry).State != EntityState.Deleted && entry.Event is not null).ToList();

    private void SyncEntryUsers(LoanOutEntry entry, IReadOnlyCollection<Guid> userIds)
    {
        var requestedUserIds = userIds.Distinct().ToHashSet();
        db.Set<LoanOutEntryUser>().RemoveRange(entry.Users.Where(user => !requestedUserIds.Contains(user.UserId)));
        LoanOutEventFactory.SyncEntryUsers(entry, requestedUserIds.ToList());
    }

    private static void ValidatePropagatedEntries(LoanOutSeries series, IEnumerable<LoanOutEntry> entries)
    {
        foreach (var entry in entries)
        {
            if (entry.Users.Count == 0)
                throw new InvalidOperationException(
                    $"Loan out series {series.Id} update would loan out an entry without any users."
                );
        }
    }

    private Task<LoanOutSeries?> LoadLoanOutSeriesAsync(int id, bool tracked, CancellationToken cancellationToken)
    {
        var query = tracked ? db.LoanOutSeries : db.LoanOutSeries.AsNoTracking();
        return query
            .Include(series => series.EventSeries!)
                .ThenInclude(eventSeries => eventSeries.Events)
            .Include(series => series.Users)
            .Include(series => series.LoanOutEntries)
                .ThenInclude(entry => entry.Event)
            .Include(series => series.LoanOutEntries)
                .ThenInclude(entry => entry.Users)
            .SingleOrDefaultAsync(series => series.Id == id, cancellationToken);
    }

    private async Task<LoanOutSeriesResponse> MapLoanOutSeriesResponseAsync(
        LoanOutSeries series,
        CancellationToken cancellationToken
    ) => (await MapLoanOutSeriesResponsesAsync([series], cancellationToken)).Single();

    private async Task<IReadOnlyList<LoanOutSeriesResponse>> MapLoanOutSeriesResponsesAsync(
        IReadOnlyCollection<LoanOutSeries> series,
        CancellationToken cancellationToken
    )
    {
        if (series.Count == 0)
            return [];

        // Query persisted occurrences so regenerated/deleted tracked entities cannot leak into responses.
        var seriesIds = series.Select(item => item.Id).ToList();
        var occurrences = await db
            .LoanOutEntries.AsNoTracking()
            .Where(entry => entry.LoanOutSeriesId.HasValue && seriesIds.Contains(entry.LoanOutSeriesId.Value))
            .Where(entry => entry.Event != null)
            .OrderBy(entry => entry.Id)
            .Select(entry => new
            {
                SeriesId = entry.LoanOutSeriesId!.Value,
                EntryId = entry.Id,
                entry.EventId,
            })
            .ToListAsync(cancellationToken);
        var bySeries = occurrences.ToLookup(entry => entry.SeriesId);

        return series
            .Select(item =>
            {
                var entries = bySeries[item.Id].ToList();
                return loanOutEventMapper.ToLoanOutSeriesResponse(
                    item,
                    entries.Select(entry => entry.EventId).ToList(),
                    entries.Select(entry => entry.EntryId).ToList()
                );
            })
            .ToList();
    }

    private static LoanOutEventCopiedValues CaptureCopiedValues(EventSeries eventSeries)
    {
        ArgumentNullException.ThrowIfNull(eventSeries);

        return new LoanOutEventCopiedValues(
            eventSeries.Title,
            eventSeries.Description,
            eventSeries.Notes,
            eventSeries.Color,
            eventSeries.LocationId
        );
    }

    private static void ApplyCopiedFieldUpdatesPreservingOverrides(
        Event eventEntity,
        LoanOutEventCopiedValues oldValues,
        LoanOutEventCopiedValues newValues
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

    private Task TransferRelatedShiftsAsync(
        LoanOutSeries entity,
        bool deleteAssignments = false,
        CancellationToken cancellationToken = default
    )
    {
        if (schedulingLoanOutService is null || entity.EventSeries is null)
            return Task.CompletedTask;

        return schedulingLoanOutService.TransferLoanedOutUserShift(entity, deleteAssignments, cancellationToken);
    }

    private sealed record LoanOutEventCopiedValues(
        string Title,
        string? Description,
        string? Notes,
        string? Color,
        int? LocationId
    );
}
