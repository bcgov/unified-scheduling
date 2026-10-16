using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Validation;
using Unified.Db;
using Unified.Db.Models.Lookup;
using Unified.Leave.Mappings;
using Unified.Leave.Models;

namespace Unified.Leave.Services;

public sealed class LeaveTypeService(ILogger<LeaveTypeService> logger, UnifiedDbContext db) : ILeaveTypeService
{
    public async Task<IReadOnlyCollection<LeaveTypeResponse>> GetLeaveTypesAsync(
        CancellationToken cancellationToken = default
    )
    {
        var leaveTypes = await db
            .LeaveTypes.AsNoTracking()
            .OrderBy(leaveType => leaveType.Code)
            .ToListAsync(cancellationToken);

        logger.LogDebug("Retrieved {LeaveTypeCount} leave types.", leaveTypes.Count);

        return leaveTypes.Select(LeaveTypeMapper.ToResponse).ToList();
    }

    public async Task<LeaveTypeResponse?> GetLeaveTypeByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var leaveType = await db
            .LeaveTypes.AsNoTracking()
            .SingleOrDefaultAsync(leaveType => leaveType.Id == id, cancellationToken);

        if (leaveType is null)
            logger.LogInformation("Leave type {LeaveTypeId} was not found.", id);

        return leaveType is null ? null : LeaveTypeMapper.ToResponse(leaveType);
    }

    public async Task<LeaveTypeResponse> CreateLeaveTypeAsync(
        LeaveTypeRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var code = request.Name.Trim();
        await EnsureCodeIsUniqueAsync(code, excludeId: null, cancellationToken);

        var leaveType = new LeaveType();
        LeaveTypeMapper.Apply(leaveType, request, code);
        leaveType.EffectiveDate = NormalizeUtcBusinessDate(request.EffectiveDateUtc ?? DateTimeOffset.UtcNow);
        leaveType.ExpiryDate = NormalizeOptionalUtcBusinessDate(request.ExpiryDateUtc);

        db.LeaveTypes.Add(leaveType);
        await db.SaveChangesAsync(cancellationToken);

        return LeaveTypeMapper.ToResponse(leaveType);
    }

    public async Task<LeaveTypeResponse?> UpdateLeaveTypeAsync(
        int id,
        LeaveTypeRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var leaveType = await db.LeaveTypes.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (leaveType is null)
        {
            logger.LogInformation("Leave type {LeaveTypeId} was not found for update.", id);
            return null;
        }

        var code = request.Name.Trim();
        await EnsureCodeIsUniqueAsync(code, excludeId: id, cancellationToken);

        LeaveTypeMapper.Apply(leaveType, request, code);
        if (request.EffectiveDateUtc.HasValue)
            leaveType.EffectiveDate = NormalizeUtcBusinessDate(request.EffectiveDateUtc.Value);
        leaveType.ExpiryDate = NormalizeOptionalUtcBusinessDate(request.ExpiryDateUtc);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated leave type {LeaveTypeId}.", id);
        return LeaveTypeMapper.ToResponse(leaveType);
    }

    public async Task<bool> DeleteLeaveTypeAsync(int id, CancellationToken cancellationToken = default)
    {
        var leaveType = await db.LeaveTypes.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (leaveType is null)
        {
            logger.LogInformation("Leave type {LeaveTypeId} was not found for deletion.", id);
            return false;
        }

        var isInUse =
            await db.LeaveEntries.AnyAsync(entry => entry.LeaveTypeId == id, cancellationToken)
            || await db.LeaveSeries.AnyAsync(series => series.LeaveTypeId == id, cancellationToken);

        if (isInUse)
        {
            logger.LogWarning("Leave type {LeaveTypeId} cannot be deleted because it is in use.", id);
            throw new ConflictValidationException(
                new Dictionary<string, string[]>
                {
                    [nameof(LeaveTypeRequest.Name)] = ["The leave type is in use and cannot be deleted."],
                }
            );
        }

        db.LeaveTypes.Remove(leaveType);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted leave type {LeaveTypeId}.", id);
        return true;
    }

    // Effective/expiry are date-only business dates stored as UTC midnight (same as assignment definitions).
    private static DateTimeOffset NormalizeUtcBusinessDate(DateTimeOffset value)
    {
        var date = DateOnly.FromDateTime(value.UtcDateTime);
        return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }

    private static DateTimeOffset? NormalizeOptionalUtcBusinessDate(DateTimeOffset? value) =>
        value.HasValue ? NormalizeUtcBusinessDate(value.Value) : null;

    private async Task EnsureCodeIsUniqueAsync(string code, int? excludeId, CancellationToken cancellationToken)
    {
        var exists = await db.LeaveTypes.AnyAsync(
            leaveType => leaveType.Code == code && leaveType.Id != excludeId,
            cancellationToken
        );

        if (!exists)
            return;

        throw new ConflictValidationException(
            new Dictionary<string, string[]>
            {
                [nameof(LeaveTypeRequest.Name)] = [$"A leave type named '{code}' already exists."],
            }
        );
    }
}
