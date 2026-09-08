using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.UserManagement;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Services;

/// <summary>
/// Manages leave records, each backed by a one-to-one linked Calendar <see cref="Event"/>
/// (see <see cref="UserLeave"/>). Lifecycle rules:
/// <list type="bullet">
/// <item>Create adds a new <see cref="Event"/> (SourceModule = "user-management", EventTypeCode = "leave")
/// alongside the <see cref="UserLeave"/> link row.</item>
/// <item>Update mutates the fields of the same linked <see cref="Event"/> in place — the event is never
/// replaced or duplicated.</item>
/// <item>Expire cancels the linked <see cref="Event"/> (sets CancelledAt/CancellationReason/StatusTypeCode)
/// and retains both rows rather than deleting them, matching away-location expiry semantics.</item>
/// </list>
/// </summary>
public sealed class LeaveService(UnifiedDbContext db) : ILeaveService
{
    public async Task<IReadOnlyCollection<LeaveResponseDto>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        await GetUserOrThrowAsync(userId, cancellationToken);

        var leaves = await db
            .UserLeaves.AsNoTracking()
            .Include(x => x.Event)
            .Include(x => x.LeaveType)
            .Where(x =>
                x.UserId == userId && (x.Event.CancelledAt == null || x.Event.CancelledAt > DateTimeOffset.UtcNow)
            )
            .OrderByDescending(x => x.Event.StartAtUtc)
            .ThenBy(x => x.LeaveTypeId)
            .ToListAsync(cancellationToken);

        return leaves.Select(MapResponse).ToList();
    }

    public async Task<LeaveResponseDto> CreateAsync(
        Guid userId,
        LeaveRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        var leaveType = await GetLeaveTypeOrThrowAsync(request.LeaveTypeCode, cancellationToken);

        var timezoneId = request.Timezone ?? user.HomeLocation?.Timezone;

        var startAtUtc = DateTimeOffset.Parse(request.StartDateTime).ToUniversalTime();
        var endAtUtc = DateTimeOffset.Parse(request.EndDateTime).ToUniversalTime();

        var calendarEvent = new Event
        {
            Title = $"Leave - {leaveType.Description}",
            Notes = request.Comment?.Trim(),
            StartAtUtc = startAtUtc,
            EndAtUtc = endAtUtc,
            TimeZoneId = timezoneId,
            AllDay = request.AllDay,
            EventTypeCode = UserManagementConstants.LeaveEventTypeCode,
            StatusTypeCode = CalendarEventStatusTypeCodes.Active,
            SourceModule = UserManagementConstants.SourceModule,
        };

        // Set Event/LeaveType via navigation (not EventId/LeaveTypeId) so the insert happens in one
        // SaveChangesAsync call/transaction and both navigations are already populated for MapResponse.
        var leave = new UserLeave
        {
            UserId = userId,
            LeaveTypeId = leaveType.Id,
            LeaveType = leaveType,
            Event = calendarEvent,
        };

        db.UserLeaves.Add(leave);
        await db.SaveChangesAsync(cancellationToken);

        return MapResponse(leave);
    }

    public async Task<LeaveResponseDto> UpdateAsync(
        Guid userId,
        int leaveId,
        LeaveRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        await GetUserOrThrowAsync(userId, cancellationToken);

        var leave = await db
            .UserLeaves.Include(x => x.Event)
            .Include(x => x.LeaveType)
            .SingleOrDefaultAsync(x => x.Id == leaveId && x.UserId == userId, cancellationToken);

        if (leave is null)
        {
            throw new KeyNotFoundException($"Leave {leaveId} not found for user {userId}.");
        }

        if (leave.Event.CancelledAt is not null)
        {
            throw new InvalidOperationException($"Leave {leaveId} is already expired and cannot be edited.");
        }

        var leaveType = await GetLeaveTypeOrThrowAsync(request.LeaveTypeCode, cancellationToken);
        var timezoneId = request.Timezone ?? leave.Event.TimeZoneId;

        leave.LeaveTypeId = leaveType.Id;
        leave.Event.Title = $"Leave - {leaveType.Description}";
        leave.Event.StartAtUtc = DateTimeOffset.Parse(request.StartDateTime).ToUniversalTime();
        leave.Event.EndAtUtc = DateTimeOffset.Parse(request.EndDateTime).ToUniversalTime();
        leave.Event.TimeZoneId = timezoneId;
        leave.Event.AllDay = request.AllDay;
        leave.Event.Notes = request.Comment?.Trim();
        leave.LeaveType = leaveType;

        await db.SaveChangesAsync(cancellationToken);
        return MapResponse(leave);
    }

    public async Task<LeaveResponseDto> ExpireAsync(
        Guid userId,
        ExpireLeaveRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        await GetUserOrThrowAsync(userId, cancellationToken);

        var leave = await db
            .UserLeaves.Include(x => x.Event)
            .Include(x => x.LeaveType)
            .SingleOrDefaultAsync(x => x.Id == request.LeaveId && x.UserId == userId, cancellationToken);

        if (leave is null)
        {
            throw new KeyNotFoundException($"Leave {request.LeaveId} not found for user {userId}.");
        }

        if (leave.Event.CancelledAt is not null)
        {
            throw new InvalidOperationException($"Leave {request.LeaveId} is already expired.");
        }

        leave.Event.CancelledAt = DateTimeOffset.UtcNow;
        leave.Event.CancellationReason = request.ExpiryReason.Trim();
        leave.Event.StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled;

        await db.SaveChangesAsync(cancellationToken);
        return MapResponse(leave);
    }

    private async Task<User> GetUserOrThrowAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db
            .Users.AsNoTracking()
            .Include(x => x.HomeLocation)
            .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException($"User {userId} not found.");
        }

        return user;
    }

    private async Task<LeaveType> GetLeaveTypeOrThrowAsync(
        string leaveTypeCode,
        CancellationToken cancellationToken = default
    )
    {
        var leaveType = await db.LeaveTypes.SingleOrDefaultAsync(lt => lt.Code == leaveTypeCode, cancellationToken);

        if (leaveType is null)
        {
            throw new KeyNotFoundException($"LeaveType '{leaveTypeCode}' not found.");
        }

        return leaveType;
    }

    private static LeaveResponseDto MapResponse(UserLeave leave) =>
        new()
        {
            Id = leave.Id,
            EventId = leave.EventId,
            UserId = leave.UserId,
            LeaveTypeId = leave.LeaveTypeId,
            LeaveTypeCode = leave.LeaveType?.Code ?? string.Empty,
            LeaveTypeDescription = leave.LeaveType?.Description ?? string.Empty,
            IsPaid = leave.LeaveType?.IsPaid ?? false,
            StartAtUtc = leave.Event.StartAtUtc,
            EndAtUtc = leave.Event.EndAtUtc,
            AllDay = leave.Event.AllDay,
            ExpiryAtUtc = leave.Event.CancelledAt,
            ExpiryReason = leave.Event.CancellationReason,
            Comment = leave.Event.Notes,
            Timezone = leave.Event.TimeZoneId,
        };
}
