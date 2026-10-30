using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Calendar.Conflicts;
using Unified.Calendar.Factories;
using Unified.Calendar.Services;
using Unified.Db;
using Unified.Db.Models.Leave;
using Unified.Leave.Conflicts;
using Unified.Leave.Constants;
using Unified.Leave.Mappings;
using Unified.Leave.Models.Scheduling;

namespace Unified.Leave.Services;

public sealed class SchedulingLeaveEntryService(
    ILogger<SchedulingLeaveEntryService> logger,
    UnifiedDbContext db,
    SchedulingLeaveEventMapper schedulingLeaveEventMapper,
    CalendarEventEntryService<NormalizedLeaveEntryRequest, LeaveEntry> calendarEventEntryService,
    LeaveRequestNormalizer requestNormalizer,
    ICalendarConflictService conflictService
) : ISchedulingLeaveEntryService
{
    public async Task<SchedulingLeaveEntryResponse> CreateLeaveEntryAsync(
        SchedulingLeaveEntryRequest input,
        CancellationToken cancellationToken = default
    )
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );
        var request = await requestNormalizer.NormalizeAsync(input, cancellationToken);
        var leaveSeries = request.LeaveSeriesId.HasValue
            ? await GetValidatedLeaveSeriesAsync(request.LeaveSeriesId.Value, cancellationToken)
            : null;
        var entity = await calendarEventEntryService.BuildEventAsync(
            request,
            LeaveConstants.SourceModule,
            LeaveConstants.LeaveEventTypeCode,
            leaveSeries?.EventSeriesId,
            cancellationToken: cancellationToken
        );
        entity.LeaveSeries = leaveSeries;
        db.LeaveEntries.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Created leave entry {LeaveEntryId}.", entity.Id);
        return schedulingLeaveEventMapper.ToSchedulingLeaveEntryResponse(entity);
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
            .Where(entry => entry.Users.Any(user => user.UserId == userId))
            .OrderByDescending(entry => entry.Event!.StartAtUtc)
            .ToListAsync(cancellationToken);
        return entities.Select(schedulingLeaveEventMapper.ToSchedulingLeaveEntryResponse).ToList();
    }

    public async Task<SchedulingLeaveEntryResponse?> UpdateLeaveEntryAsync(
        int id,
        SchedulingLeaveEntryRequest input,
        CancellationToken cancellationToken = default
    )
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );
        var entity = await LoadLeaveEntryAsync(id, tracked: true, cancellationToken);
        if (entity is null)
            return null;

        var request = await requestNormalizer.NormalizeAsync(input, cancellationToken);
        var leaveSeries = request.LeaveSeriesId.HasValue
            ? await GetValidatedLeaveSeriesAsync(request.LeaveSeriesId.Value, cancellationToken)
            : null;

        var updated = await calendarEventEntryService.UpdateEventAsync(
            request,
            entity,
            LeaveConstants.SourceModule,
            LeaveConstants.LeaveEventTypeCode,
            additionalGuards: input =>
            {
                entity.Event!.EventSeriesId = leaveSeries?.EventSeriesId;
                entity.LeaveSeries = leaveSeries;
                entity.LeaveSeriesId = input.LeaveSeriesId;
            },
            cancellationToken: cancellationToken
        );
        if (updated is null)
            return null;
        await db.SaveChangesAsync(cancellationToken);
        await conflictService.InvalidateResolvedOverridesAsync(
            [LeaveConflictParticipantProvider.IdentityFor(entity.EventId)],
            cancellationToken: cancellationToken
        );
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Updated leave entry {LeaveEntryId}.", id);
        return schedulingLeaveEventMapper.ToSchedulingLeaveEntryResponse(updated);
    }

    public async Task<bool> DeleteLeaveEntryAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Deleting leave entry {LeaveEntryId}.", id);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );
        var entity = await LoadLeaveEntryAsync(id, tracked: true, cancellationToken);
        if (entity is null)
            return false;
        if (
            !calendarEventEntryService.IsDeletable(
                entity,
                LeaveConstants.SourceModule,
                LeaveConstants.LeaveEventTypeCode
            )
        )
            return false;
        db.Set<LeaveEntryUser>().RemoveRange(entity.Users);
        db.LeaveEntries.Remove(entity);
        db.Events.Remove(entity.Event!);
        await db.SaveChangesAsync(cancellationToken);
        await conflictService.InvalidateResolvedOverridesAsync(
            [LeaveConflictParticipantProvider.IdentityFor(entity.EventId)],
            cancellationToken: cancellationToken
        );
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Deleted leave entry {LeaveEntryId}.", id);
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

    private async Task<LeaveSeries> GetValidatedLeaveSeriesAsync(int id, CancellationToken cancellationToken)
    {
        var leaveSeries = await db
            .LeaveSeries.Include(series => series.EventSeries)
            .SingleOrDefaultAsync(series => series.Id == id, cancellationToken);
        if (leaveSeries is null)
            throw new KeyNotFoundException($"Leave series {id} not found.");

        CalendarEventGuards.EnsureEventSeriesType(leaveSeries.EventSeries!, LeaveConstants.LeaveEventTypeCode);
        return leaveSeries;
    }
}
