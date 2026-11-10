using Unified.Calendar.Conflicts;
using Unified.Calendar.Models;
using Unified.Common.Calendar.Conflicts;

namespace Unified.Tests.Calendar.Models;

public sealed class CalendarConflictResponseTests
{
    [Theory]
    [InlineData("leave", "shift", "Leave ↔ Shift")]
    [InlineData("leave", "assignment", "Leave ↔ Assignment")]
    [InlineData("leave", "training", "Leave ↔ Training")]
    [InlineData("leave", "holiday", "Leave ↔ Holiday")]
    [InlineData("assignment", "shift", "Assignment ↔ Shift")]
    [InlineData("assignment", "training", "Assignment ↔ Training")]
    [InlineData("assignment", "holiday", "Assignment ↔ Holiday")]
    [InlineData("shift", "training", "Shift ↔ Training")]
    [InlineData("shift", "holiday", "Holiday ↔ Shift")]
    [InlineData("training", "holiday", "Holiday ↔ Training")]
    [InlineData("leave", "leave", "Leave ↔ Leave")]
    [InlineData("shift", "shift", "Shift ↔ Shift")]
    [InlineData("assignment", "assignment", "Assignment ↔ Assignment")]
    [InlineData("training", "training", "Training ↔ Training")]
    [InlineData("holiday", "holiday", "Holiday ↔ Holiday")]
    public void FromConflict_EventTypes_MapsStableTitleWithoutChangingEventDetails(
        string entryType,
        string overlapsType,
        string expectedTitle
    )
    {
        var resourceId = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 9, 21, 16, 0, 0, TimeSpan.Zero);
        var entry = new CalendarConflictParticipant(
            "1",
            "scheduling",
            resourceId,
            start,
            start.AddHours(1),
            "First event",
            EventTypeCode: entryType
        );
        var overlaps = new CalendarConflictParticipant(
            "2",
            "scheduling",
            resourceId,
            start,
            start.AddHours(1),
            "Second event",
            EventTypeCode: overlapsType
        );
        var conflict = new CalendarConflict(
            entry,
            overlaps,
            resourceId,
            start,
            start.AddHours(1),
            ReportOnEntry: true,
            ReportOnOverlaps: false
        );

        var response = CalendarConflictResponse.FromConflict(conflict);
        var reversed = CalendarConflictResponse.FromConflict(conflict with { Entry = overlaps, Overlaps = entry });

        Assert.Equal(expectedTitle, response.Title);
        Assert.Equal(response.Title, reversed.Title);
        Assert.Equal(conflict.Id, response.Id);
        Assert.Equal(response.Id, reversed.Id);
        Assert.Equal("First event", response.Entry.Title);
        Assert.Equal("Second event", response.Overlaps.Title);
        Assert.Equal("Second event", reversed.Entry.Title);
        Assert.Equal("First event", reversed.Overlaps.Title);
        Assert.True(response.ReportOnEntry);
        Assert.False(response.ReportOnOverlaps);
    }
}
