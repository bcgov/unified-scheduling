using Microsoft.EntityFrameworkCore;
using Unified.Calendar;
using Unified.Calendar.Services;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.LoanOut;
using Unified.Db.Models.Scheduling;

namespace Unified.Scheduling.Services;

public sealed class ShiftLoanOutExceptionMarkerService(
    UnifiedDbContext db,
    ShiftLoanOutAvailabilityService availabilityService,
    ICalendarTimeZoneResolver timeZoneResolver
)
{
    public async Task<IReadOnlyDictionary<ShiftEntry, IReadOnlyCollection<Guid>>> ApplyToSeriesAsync(
        IReadOnlyCollection<ShiftEntry> entries,
        CancellationToken cancellationToken
    )
    {
        var awayUsers = await availabilityService.GetAwayUserIdsAsync(entries, cancellationToken);
        foreach (var entry in entries.Where(entry => entry.Event?.StatusTypeCode == CalendarEventStatusTypeCodes.Draft))
            RefreshMarker(entry, awayUsers.GetValueOrDefault(entry) ?? []);
        return awayUsers;
    }

    public async Task RestoreHomeLocationUserShiftsAsync(
        IReadOnlyCollection<LoanOutEntry> removedEntries,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var removed = removedEntries.Where(entry => entry.Event is not null).ToList();
        var userIds = removed.SelectMany(entry => entry.Users).Select(user => user.UserId).Distinct().ToList();
        if (userIds.Count == 0)
            return;
        var removedIds = removed.Select(entry => entry.Id).ToList();
        var start = removed.Min(entry => entry.Event!.StartAtUtc).AddDays(-1);
        var end = removed.Max(entry => entry.Event!.EndAtUtc ?? entry.Event.StartAtUtc).AddDays(1);
        var shifts = await db
            .ShiftEntries.Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .Where(entry =>
                entry.Event != null
                && entry.Event.IsException
                && entry.Event.ExceptionReason == nameof(CalendarEventExceptionReasonCode.IsAway)
                && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                && entry.Event.StartAtUtc < end
                && (entry.Event.EndAtUtc ?? entry.Event.StartAtUtc) >= start
                && entry.Users.Any(user => userIds.Contains(user.UserId))
            )
            .ToListAsync(cancellationToken);
        if (shifts.Count == 0)
            return;
        var locationIds = shifts.Select(entry => entry.Event!.LocationId).OfType<int>().Distinct().ToList();
        var locations = await db
            .Locations.AsNoTracking()
            .Where(location => locationIds.Contains(location.Id))
            .ToDictionaryAsync(location => location.Id, location => location.Timezone, cancellationToken);
        var homeLocations = await db
            .Users.AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.HomeLocationId, cancellationToken);
        var candidates = shifts
            .Where(shift => shift.Event!.LocationId.HasValue)
            .Select(shift =>
            {
                var locationId = shift.Event!.LocationId!.Value;
                var zone = timeZoneResolver.Resolve(null, locations.GetValueOrDefault(locationId));
                return (
                    Shift: shift,
                    Range: availabilityService.GetAssignmentDayRange(
                        new ShiftAssignmentLoanOutContext(shift.Event.StartAtUtc, shift.Event.EndAtUtc),
                        zone
                    )
                );
            })
            .ToList();
        if (candidates.Count == 0)
            return;
        var rangeStart = candidates.Min(item => item.Range.StartAtUtc);
        var rangeEnd = candidates.Max(item => item.Range.EndAtUtc);
        var candidateUserIds = candidates
            .SelectMany(item => item.Shift.Users)
            .Select(user => user.UserId)
            .Distinct()
            .ToList();
        var remaining = await db
            .LoanOutEntries.AsNoTracking()
            .Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .Where(entry =>
                !removedIds.Contains(entry.Id)
                && entry.Event != null
                && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                && entry.Users.Any(user => candidateUserIds.Contains(user.UserId))
                && entry.Event.StartAtUtc < rangeEnd
                && (
                    entry.Event.EndAtUtc.HasValue
                        ? entry.Event.EndAtUtc > rangeStart
                        : entry.Event.StartAtUtc >= rangeStart
                )
            )
            .ToListAsync(cancellationToken);
        foreach (var (shift, range) in candidates)
        {
            var locationId = shift.Event!.LocationId!.Value;
            var awayUserIds = shift.Users.Select(user => user.UserId).ToHashSet();
            foreach (var user in shift.Users)
            {
                var affected = removed.Any(entry =>
                    ShiftLoanOutAvailabilityService.IsFromLocation(entry, user.UserId, locationId, homeLocations)
                    && entry.LoanOutLocationId != locationId
                    && entry.Users.Any(item => item.UserId == user.UserId)
                    && ShiftLoanOutAvailabilityService.Overlaps(entry.Event, range.StartAtUtc, range.EndAtUtc)
                );
                if (!affected)
                    continue;
                var stillAway = remaining.Any(entry =>
                    entry.LoanOutLocationId != locationId
                    && ShiftLoanOutAvailabilityService.IsFromLocation(entry, user.UserId, locationId, homeLocations)
                    && entry.Users.Any(item => item.UserId == user.UserId)
                    && ShiftLoanOutAvailabilityService.Overlaps(entry.Event!, range.StartAtUtc, range.EndAtUtc)
                );
                if (!stillAway)
                    awayUserIds.Remove(user.UserId);
            }
            RefreshMarker(shift, awayUserIds);
        }
    }

    public void RefreshMarker(ShiftEntry entry, IReadOnlyCollection<Guid> awayUserIds)
    {
        var calendarEvent = entry.Event!;
        if (entry.Users.Count > 0 && entry.Users.All(user => awayUserIds.Contains(user.UserId)))
        {
            if (calendarEvent.ExceptionReason is null or nameof(CalendarEventExceptionReasonCode.IsAway))
            {
                calendarEvent.IsException = true;
                calendarEvent.ExceptionReason = nameof(CalendarEventExceptionReasonCode.IsAway);
            }
        }
        else if (calendarEvent.ExceptionReason == nameof(CalendarEventExceptionReasonCode.IsAway))
        {
            calendarEvent.IsException = false;
            calendarEvent.ExceptionReason = null;
        }
    }
}
