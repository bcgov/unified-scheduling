using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Services;

namespace Unified.Tests.Scheduling.Services;

public sealed class ShiftAssignmentUserRulesTests
{
    [Fact]
    public void NormalizeRequiredUserIds_WhenUsersRepeat_Throws()
    {
        var userId = Guid.NewGuid();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ShiftAssignmentGuards.NormalizeRequiredUserIds([userId, userId])
        );

        Assert.Equal("Selected users must be unique.", exception.Message);
    }

    [Fact]
    public void EnsureCanLink_WhenSelectedUserDoesNotBelongToShift_Throws()
    {
        var shiftUserId = Guid.NewGuid();
        var requestedUserId = Guid.NewGuid();
        var shiftEntry = new ShiftEntry
        {
            Event = CreateEvent(8, 0, 16, 0),
            Users = [new ShiftEntryUser { UserId = shiftUserId }],
        };
        var assignmentEntry = new AssignmentEntry { Event = CreateEvent(9, 0, 10, 0) };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ShiftAssignmentGuards.EnsureCanLink(shiftEntry, assignmentEntry, [requestedUserId])
        );

        Assert.Equal("Selected users must belong to the linked shift entry.", exception.Message);
    }

    [Fact]
    public void ReplaceEntryUsers_WhenUsersChange_ReplacesRelationshipAndPreservesExceptionDecision()
    {
        var originalUserId = Guid.NewGuid();
        var replacementUserId = Guid.NewGuid();
        var seriesLink = new ShiftAssignmentSeriesLink
        {
            Users = [new ShiftAssignmentSeriesLinkUser { UserId = originalUserId }],
        };
        var entryLink = new ShiftAssignmentEntry
        {
            Id = 42,
            ShiftAssignmentSeriesLink = seriesLink,
            Users = [new ShiftAssignmentEntryUser { UserId = originalUserId }],
        };

        ShiftAssignmentUserSync.ReplaceEntryUsers(entryLink, [replacementUserId]);
        ShiftAssignmentUserSync.UpdateExceptionState(entryLink, [replacementUserId]);

        var user = Assert.Single(entryLink.Users);
        Assert.Equal(42, user.ShiftAssignmentEntryId);
        Assert.Equal(replacementUserId, user.UserId);
        Assert.True(entryLink.IsException);
    }

    private static Event CreateEvent(int startHour, int startMinute, int endHour, int endMinute) =>
        new()
        {
            StartAtUtc = new DateTimeOffset(2026, 8, 21, startHour, startMinute, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 8, 21, endHour, endMinute, 0, TimeSpan.Zero),
            StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
        };
}
