using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Validation;
using Unified.Db;
using Unified.Db.Models.Lookup;
using Unified.TimeOff.Mappings;
using Unified.TimeOff.Models;

namespace Unified.TimeOff.Services;

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
        leaveType.EffectiveDate = request.EffectiveDateUtc ?? DateTimeOffset.UtcNow;

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
            leaveType.EffectiveDate = request.EffectiveDateUtc.Value;

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
            await db.TimeOffEntries.AnyAsync(entry => entry.LeaveTypeId == id, cancellationToken)
            || await db.TimeOffSeries.AnyAsync(series => series.LeaveTypeId == id, cancellationToken);

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
