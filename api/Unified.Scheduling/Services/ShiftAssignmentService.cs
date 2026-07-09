using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Calendar.Services;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public sealed class ShiftAssignmentService(
    ILogger<ShiftAssignmentService> logger,
    UnifiedDbContext db,
    ICalendarDateTimeService calendarDateTimeService
) : IShiftAssignmentService
{
    public async Task<ShiftAssignmentEntryResponse> LinkShiftEntryAsync(
        ShiftAssignmentEntryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var selectedUserIds = ValidateSelectedUserIds(request.UserIds);
        var shiftEntry = await LoadShiftEntryAsync(request.ShiftEntryId, cancellationToken);
        var assignmentEntry = await LoadAssignmentEntryAsync(request.AssignmentEntryId, cancellationToken);

        ValidateCanLink(shiftEntry, assignmentEntry, selectedUserIds);

        if (
            await db.ShiftAssignmentEntries.AnyAsync(
                link => link.ShiftEntryId == shiftEntry.Id && link.AssignmentEntryId == assignmentEntry.Id,
                cancellationToken
            )
        )
            throw new InvalidOperationException("Shift entry is already linked to this assignment entry.");

        var link = CreateLink(shiftEntry.Id, assignmentEntry.Id, selectedUserIds);
        db.ShiftAssignmentEntries.Add(link);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Linked shift entry {ShiftEntryId} to assignment entry {AssignmentEntryId}.",
            shiftEntry.Id,
            assignmentEntry.Id
        );

        return MapToResponse(link, assignmentEntry.Capacity);
    }

    public async Task<IReadOnlyCollection<ShiftAssignmentEntryResponse>> LinkShiftSeriesAsync(
        ShiftAssignmentSeriesRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var selectedUserIds = ValidateSelectedUserIds(request.UserIds);
        var shiftSeriesExists = await db.ShiftSeries.AnyAsync(series => series.Id == request.ShiftSeriesId, cancellationToken);
        var assignmentSeriesExists = await db
            .AssignmentSeries.AnyAsync(series => series.Id == request.AssignmentSeriesId, cancellationToken);

        if (!shiftSeriesExists)
            throw new KeyNotFoundException($"Shift series {request.ShiftSeriesId} not found.");
        if (!assignmentSeriesExists)
            throw new KeyNotFoundException($"Assignment series {request.AssignmentSeriesId} not found.");

        var shiftEntries = await db
            .ShiftEntries.Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .Where(entry => entry.ShiftSeriesId == request.ShiftSeriesId)
            .Where(entry => entry.Event != null && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled)
            .ToListAsync(cancellationToken);
        var assignmentEntries = await db
            .AssignmentEntries.Include(entry => entry.Event)
            .Where(entry => entry.AssignmentSeriesId == request.AssignmentSeriesId)
            .Where(entry => entry.Event != null && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled)
            .ToListAsync(cancellationToken);

        var intersections = (
            from shiftEntry in shiftEntries
            from assignmentEntry in assignmentEntries
            where LocalDateRangesOverlap(shiftEntry.Event!, assignmentEntry.Event!)
            select (shiftEntry, assignmentEntry)
        ).ToList();

        foreach (var (shiftEntry, _) in intersections)
        {
            var shiftUserIds = shiftEntry.Users.Select(user => user.UserId).ToHashSet();
            if (!selectedUserIds.All(shiftUserIds.Contains))
            {
                logger.LogInformation(
                    "Invalid selected users for shift entry {ShiftEntryId} during series assignment link.",
                    shiftEntry.Id
                );
                throw new InvalidOperationException("Selected users must belong to every intersecting shift entry.");
            }
        }

        var createdLinks = new List<ShiftAssignmentEntry>();
        foreach (var (shiftEntry, assignmentEntry) in intersections)
        {
            var exists = await db.ShiftAssignmentEntries.AnyAsync(
                link => link.ShiftEntryId == shiftEntry.Id && link.AssignmentEntryId == assignmentEntry.Id,
                cancellationToken
            );

            if (exists)
            {
                logger.LogInformation(
                    "Ignoring existing shift assignment link for shift entry {ShiftEntryId} and assignment entry {AssignmentEntryId}.",
                    shiftEntry.Id,
                    assignmentEntry.Id
                );
                continue;
            }

            var link = CreateLink(shiftEntry.Id, assignmentEntry.Id, selectedUserIds);
            db.ShiftAssignmentEntries.Add(link);
            createdLinks.Add(link);
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Linked shift series {ShiftSeriesId} to assignment series {AssignmentSeriesId}; created {LinkCount} links.",
            request.ShiftSeriesId,
            request.AssignmentSeriesId,
            createdLinks.Count
        );

        return createdLinks
            .Select(link =>
            {
                var assignmentEntry = assignmentEntries.Single(entry => entry.Id == link.AssignmentEntryId);
                return MapToResponse(link, assignmentEntry.Capacity);
            })
            .ToList();
    }

    private async Task<ShiftEntry> LoadShiftEntryAsync(int id, CancellationToken cancellationToken) =>
        await db
            .ShiftEntries.Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken)
        ?? throw new KeyNotFoundException($"Shift entry {id} not found.");

    private async Task<AssignmentEntry> LoadAssignmentEntryAsync(int id, CancellationToken cancellationToken) =>
        await db
            .AssignmentEntries.Include(entry => entry.Event)
            .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken)
        ?? throw new KeyNotFoundException($"Assignment entry {id} not found.");

    private void ValidateCanLink(
        ShiftEntry shiftEntry,
        AssignmentEntry assignmentEntry,
        IReadOnlyCollection<Guid> selectedUserIds
    )
    {
        if (shiftEntry.Event?.StatusTypeCode == CalendarEventStatusTypeCodes.Cancelled)
        {
            logger.LogInformation("Blocked link to cancelled shift entry {ShiftEntryId}.", shiftEntry.Id);
            throw new InvalidOperationException("Cancelled shift entries cannot be linked.");
        }

        if (assignmentEntry.Event?.StatusTypeCode == CalendarEventStatusTypeCodes.Cancelled)
        {
            logger.LogInformation("Blocked link to cancelled assignment entry {AssignmentEntryId}.", assignmentEntry.Id);
            throw new InvalidOperationException("Cancelled assignment entries cannot be linked.");
        }

        var shiftUserIds = shiftEntry.Users.Select(user => user.UserId).ToHashSet();
        if (!selectedUserIds.All(shiftUserIds.Contains))
        {
            logger.LogInformation("Invalid selected users for shift entry {ShiftEntryId}.", shiftEntry.Id);
            throw new InvalidOperationException("Selected users must belong to the linked shift entry.");
        }
    }

    private bool LocalDateRangesOverlap(Event shiftEvent, Event assignmentEvent)
    {
        var shiftRange = GetLocalDateRange(shiftEvent);
        var assignmentRange = GetLocalDateRange(assignmentEvent);
        return shiftRange.StartDate <= assignmentRange.EndDate && assignmentRange.StartDate <= shiftRange.EndDate;
    }

    private LocalDateRange GetLocalDateRange(Event eventEntity)
    {
        var timeZone = calendarDateTimeService.ResolveTimeZone(eventEntity.TimeZoneId);
        var localStart = calendarDateTimeService.ToLocalTime(eventEntity.StartAtUtc, timeZone);
        var startDate = DateOnly.FromDateTime(localStart);
        if (!eventEntity.EndAtUtc.HasValue)
            return new LocalDateRange(startDate, startDate);

        var localEnd = calendarDateTimeService.ToLocalTime(eventEntity.EndAtUtc.Value, timeZone);
        var endDate = DateOnly.FromDateTime(localEnd);
        if (eventEntity.AllDay && localEnd.TimeOfDay == TimeSpan.Zero && endDate > startDate)
            endDate = endDate.AddDays(-1);

        if (endDate < startDate)
            endDate = startDate;

        return new LocalDateRange(startDate, endDate);
    }

    private static IReadOnlyCollection<Guid> ValidateSelectedUserIds(IReadOnlyCollection<Guid> userIds)
    {
        if (userIds.Count == 0)
            throw new InvalidOperationException("At least one selected user is required.");

        var distinctUserIds = userIds.Distinct().ToList();
        if (distinctUserIds.Count != userIds.Count)
            throw new InvalidOperationException("Selected users must be unique.");

        return distinctUserIds;
    }

    private static ShiftAssignmentEntry CreateLink(
        int shiftEntryId,
        int assignmentEntryId,
        IReadOnlyCollection<Guid> selectedUserIds
    ) =>
        new()
        {
            ShiftEntryId = shiftEntryId,
            AssignmentEntryId = assignmentEntryId,
            Users = selectedUserIds.Select(userId => new ShiftAssignmentEntryUser { UserId = userId }).ToList(),
        };

    private static ShiftAssignmentEntryResponse MapToResponse(ShiftAssignmentEntry link, int capacity)
    {
        var userIds = link.Users.Select(user => user.UserId).Distinct().ToList();
        return new ShiftAssignmentEntryResponse
        {
            Id = link.Id,
            ShiftEntryId = link.ShiftEntryId,
            AssignmentEntryId = link.AssignmentEntryId,
            Capacity = capacity,
            AssignedUserCount = userIds.Count,
            UserIds = userIds,
        };
    }

    private readonly record struct LocalDateRange(DateOnly StartDate, DateOnly EndDate);
}
