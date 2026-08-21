using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;

namespace Unified.Scheduling.Services;

internal static class ShiftAssignmentGuards
{
    public static IReadOnlyCollection<Guid> NormalizeRequiredUserIds(IReadOnlyCollection<Guid> userIds)
    {
        if (userIds.Count == 0)
            throw new InvalidOperationException("At least one selected user is required.");

        var distinctUserIds = userIds.Distinct().ToList();
        if (distinctUserIds.Count != userIds.Count)
            throw new InvalidOperationException("Selected users must be unique.");

        return distinctUserIds;
    }

    public static void EnsureUsersBelongToShiftEntry(
        ShiftEntry shiftEntry,
        IReadOnlyCollection<Guid> selectedUserIds,
        string errorMessage = "Selected users must belong to the linked shift entry."
    )
    {
        var shiftUserIds = shiftEntry.Users.Select(user => user.UserId).ToHashSet();
        if (!selectedUserIds.All(shiftUserIds.Contains))
            throw new InvalidOperationException(errorMessage);
    }

    public static void EnsureCanLink(
        ShiftEntry shiftEntry,
        AssignmentEntry assignmentEntry,
        IReadOnlyCollection<Guid> selectedUserIds
    )
    {
        if (shiftEntry.Event?.StatusTypeCode == CalendarEventStatusTypeCodes.Cancelled)
            throw new InvalidOperationException("Cancelled shift entries cannot be linked.");

        if (assignmentEntry.Event?.StatusTypeCode == CalendarEventStatusTypeCodes.Cancelled)
            throw new InvalidOperationException("Cancelled assignment entries cannot be linked.");

        if (
            shiftEntry.Event is not Event shiftEvent
            || assignmentEntry.Event is not Event assignmentEvent
            || !UtcIntervalsOverlap(
                shiftEvent.StartAtUtc,
                shiftEvent.EndAtUtc,
                assignmentEvent.StartAtUtc,
                assignmentEvent.EndAtUtc
            )
        )
            throw new InvalidOperationException("Shift and assignment entries must overlap.");

        EnsureUsersBelongToShiftEntry(shiftEntry, selectedUserIds);
    }

    public static void EnsureShiftEntryUpdatePreservesLinks(
        ShiftEntry shiftEntry,
        DateTimeOffset proposedStartAtUtc,
        DateTimeOffset? proposedEndAtUtc,
        IReadOnlyCollection<Guid> proposedUserIds,
        int? proposedShiftSeriesId
    )
    {
        var activeLinks = shiftEntry
            .ShiftAssignmentEntries.Where(link =>
                link.Users.Count > 0
                && link.AssignmentEntry?.Event?.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
            )
            .ToList();
        var proposedUserIdSet = proposedUserIds.ToHashSet();

        if (
            shiftEntry.ShiftAssignmentEntries.Any(link =>
                link.ShiftAssignmentSeriesLinkId.HasValue
                && link.ShiftAssignmentSeriesLink?.ShiftSeriesId != proposedShiftSeriesId
            )
        )
            throw new InvalidOperationException(
                "A series-linked shift entry cannot be moved outside its linked shift series."
            );

        if (
            activeLinks
                .SelectMany(link => link.Users)
                .Select(user => user.UserId)
                .Any(userId => !proposedUserIdSet.Contains(userId))
        )
            throw new InvalidOperationException(
                "Shift users cannot be removed while they are assigned to linked assignments."
            );

        if (
            activeLinks.Any(link =>
                link.AssignmentEntry?.Event is not Event assignmentEvent
                || !UtcIntervalsOverlap(
                    proposedStartAtUtc,
                    proposedEndAtUtc,
                    assignmentEvent.StartAtUtc,
                    assignmentEvent.EndAtUtc
                )
            )
        )
            throw new InvalidOperationException(
                "Shift time cannot be changed because it would no longer overlap a linked assignment."
            );
    }

    public static void EnsureAssignmentEntryUpdatePreservesLinks(
        AssignmentEntry assignmentEntry,
        DateTimeOffset proposedStartAtUtc,
        DateTimeOffset proposedEndAtUtc,
        int? proposedAssignmentSeriesId
    )
    {
        if (
            assignmentEntry.ShiftAssignmentEntries.Any(link =>
                link.ShiftAssignmentSeriesLinkId.HasValue
                && link.ShiftAssignmentSeriesLink?.AssignmentSeriesId != proposedAssignmentSeriesId
            )
        )
            throw new InvalidOperationException(
                "A series-linked assignment entry cannot be moved outside its linked assignment series."
            );

        var invalidatesLink = assignmentEntry
            .ShiftAssignmentEntries.Where(link => link.Users.Count > 0)
            .Where(link => link.ShiftEntry?.Event?.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled)
            .Any(link =>
                link.ShiftEntry?.Event is not Event shiftEvent
                || !UtcIntervalsOverlap(
                    shiftEvent.StartAtUtc,
                    shiftEvent.EndAtUtc,
                    proposedStartAtUtc,
                    proposedEndAtUtc
                )
            );

        if (invalidatesLink)
            throw new InvalidOperationException(
                "Assignment time cannot be changed because it would no longer overlap a linked shift."
            );
    }

    public static bool UtcIntervalsOverlap(
        DateTimeOffset firstStartAtUtc,
        DateTimeOffset? firstEndAtUtc,
        DateTimeOffset secondStartAtUtc,
        DateTimeOffset? secondEndAtUtc
    ) =>
        firstEndAtUtc.HasValue
        && secondEndAtUtc.HasValue
        && firstStartAtUtc < secondEndAtUtc.Value
        && secondStartAtUtc < firstEndAtUtc.Value;
}
