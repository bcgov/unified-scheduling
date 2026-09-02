using Unified.Db.Models.Scheduling;

namespace Unified.Scheduling.Services;

internal static class ShiftAssignmentUserSync
{
    public static ShiftAssignmentEntry CreateEntryLink(
        int shiftEntryId,
        int assignmentEntryId,
        IReadOnlyCollection<Guid> selectedUserIds,
        ShiftAssignmentSeriesLink? seriesLink = null,
        bool isException = false
    ) =>
        new()
        {
            ShiftEntryId = shiftEntryId,
            AssignmentEntryId = assignmentEntryId,
            ShiftAssignmentSeriesLink = seriesLink,
            IsException = isException,
            Users = selectedUserIds.Select(userId => new ShiftAssignmentEntryUser { UserId = userId }).ToList(),
        };

    public static void ReplaceSeriesUsers(ShiftAssignmentSeriesLink link, IReadOnlyCollection<Guid> selectedUserIds)
    {
        link.Users.Clear();
        foreach (var userId in selectedUserIds)
            link.Users.Add(new ShiftAssignmentSeriesLinkUser { UserId = userId });
    }

    public static void ReplaceEntryUsers(ShiftAssignmentEntry link, IReadOnlyCollection<Guid> selectedUserIds)
    {
        link.Users.Clear();
        foreach (var userId in selectedUserIds)
            link.Users.Add(new ShiftAssignmentEntryUser { ShiftAssignmentEntryId = link.Id, UserId = userId });
    }

    public static void UpdateExceptionState(ShiftAssignmentEntry link, IReadOnlyCollection<Guid> selectedUserIds)
    {
        if (link.ShiftAssignmentSeriesLink is null)
        {
            link.IsException = false;
            return;
        }

        var parentUserIds = link.ShiftAssignmentSeriesLink.Users.Select(user => user.UserId).ToHashSet();
        link.IsException = !selectedUserIds.ToHashSet().SetEquals(parentUserIds);
    }
}
