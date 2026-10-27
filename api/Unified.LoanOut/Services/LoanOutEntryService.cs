using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Calendar.Services;
using Unified.Db;
using Unified.Db.Models.LoanOut;
using Unified.LoanOut.Constants;
using Unified.LoanOut.Mappers;
using Unified.LoanOut.Models;
using Unified.Scheduling.Services;

namespace Unified.LoanOut.Services;

public sealed class LoanOutEntryService(
    ILogger<LoanOutEntryService> logger,
    UnifiedDbContext db,
    LoanOutEventMapper loanOutEventMapper,
    CalendarEventEntryService<LoanOutEntryRequest, LoanOutEntry> calendarEventEntryService,
    ISchedulingLoanOutService? schedulingLoanOutService = null
) : ILoanOutEntryService
{
    public async Task<LoanOutEntryResponse> CreateLoanOutEntryAsync(
        LoanOutEntryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );
        var entity = await calendarEventEntryService.BuildEventAsync(
            request,
            LoanOutConstants.SourceModule,
            LoanOutConstants.LoanOutEventTypeCode,
            cancellationToken: cancellationToken
        );
        db.LoanOutEntries.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await TransferRelatedShiftsAsync(entity, cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Created loan out entry {LoanOutEntryId}.", entity.Id);
        return loanOutEventMapper.ToLoanOutEntryResponse(entity);
    }

    public async Task<LoanOutEntryResponse?> GetLoanOutEntryByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await LoadLoanOutEntryAsync(id, tracked: false, cancellationToken);
        return entity is null ? null : loanOutEventMapper.ToLoanOutEntryResponse(entity);
    }

    public async Task<IReadOnlyList<LoanOutEntryResponse>> GetLoanOutEntriesByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        var entities = await db
            .LoanOutEntries.AsNoTracking()
            .Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .Where(entry => entry.Users.Any(user => user.UserId == userId))
            .OrderByDescending(entry => entry.Event!.StartAtUtc)
            .ToListAsync(cancellationToken);
        return entities.Select(loanOutEventMapper.ToLoanOutEntryResponse).ToList();
    }

    public async Task<LoanOutEntryResponse?> UpdateLoanOutEntryAsync(
        int id,
        LoanOutEntryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );
        var entity = await LoadLoanOutEntryAsync(id, tracked: true, cancellationToken);
        if (entity is null)
            return null;

        var updated = await calendarEventEntryService.UpdateEventAsync(
            request,
            entity,
            LoanOutConstants.SourceModule,
            LoanOutConstants.LoanOutEventTypeCode,
            cancellationToken: cancellationToken
        );
        if (updated is null)
            return null;
        await db.SaveChangesAsync(cancellationToken);
        await TransferRelatedShiftsAsync(entity, deleteAssignments: false, cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Updated loan out entry {LoanOutEntryId}.", id);
        return loanOutEventMapper.ToLoanOutEntryResponse(updated);
    }

    public async Task<bool> DeleteLoanOutEntryAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Deleting loan out entry {LoanOutEntryId}.", id);
        var entity = await LoadLoanOutEntryAsync(id, tracked: true, cancellationToken);
        if (entity is null)
            return false;
        if (
            !calendarEventEntryService.IsDeletable(
                entity,
                LoanOutConstants.SourceModule,
                LoanOutConstants.LoanOutEventTypeCode
            )
        )
            return false;
        db.Set<LoanOutEntryUser>().RemoveRange(entity.Users);
        db.LoanOutEntries.Remove(entity);
        db.Events.Remove(entity.Event!);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted loan out entry {LoanOutEntryId}.", id);
        return true;
    }

    private Task<LoanOutEntry?> LoadLoanOutEntryAsync(int id, bool tracked, CancellationToken cancellationToken)
    {
        var query = tracked ? db.LoanOutEntries : db.LoanOutEntries.AsNoTracking();
        return query
            .Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);
    }

    private Task TransferRelatedShiftsAsync(
        LoanOutEntry entity,
        bool deleteAssignments = false,
        CancellationToken cancellationToken = default
    )
    {
        if (schedulingLoanOutService is null || entity.Event is null)
            return Task.CompletedTask;

        return schedulingLoanOutService.TransferLoanedOutUserShift(entity, deleteAssignments, cancellationToken);
    }
}
