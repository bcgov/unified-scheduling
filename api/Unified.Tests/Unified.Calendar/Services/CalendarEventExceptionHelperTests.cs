using Unified.Calendar;
using Unified.Calendar.Services;
using Unified.Db.Models.Calendar;

namespace Unified.Tests.Calendar.Services;

public sealed class CalendarEventExceptionHelperTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 0, true)]
    [InlineData(0, 1, true)]
    public void UpdateExceptionFlag_UsesOnlySeriesTimes(int startOffset, int endOffset, bool expected)
    {
        var occurrence = CreateOccurrence();
        occurrence.StartAtUtc = Start.AddHours(startOffset);
        occurrence.EndAtUtc = Start.AddHours(8 + endOffset);
        occurrence.Title = "An unrelated title override";
        occurrence.AllDay = true;

        CalendarEventExceptionHelper.UpdateExceptionFlag(occurrence);
        CalendarEventExceptionHelper.UpdateExceptionFlag(occurrence);

        Assert.Equal(expected, occurrence.IsException);
        Assert.Equal(expected ? new[] { "DateTimeChanged" } : [], occurrence.ExceptionReasonCodes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UpdateExceptionFlag_RestoringTimesOrDetaching_PreservesAway(bool detach)
    {
        var occurrence = CreateOccurrence();
        occurrence.StartAtUtc = Start.AddHours(1);
        CalendarEventExceptionHelper.SetReason(occurrence, CalendarEventExceptionReasonCode.IsAway, true);
        CalendarEventExceptionHelper.UpdateExceptionFlag(occurrence);
        Assert.Equal(new[] { "IsAway", "DateTimeChanged" }, occurrence.ExceptionReasonCodes);

        if (detach)
            occurrence.EventSeriesId = null;
        else
            occurrence.StartAtUtc = Start;
        CalendarEventExceptionHelper.UpdateExceptionFlag(occurrence);

        Assert.Equal(new[] { "IsAway" }, occurrence.ExceptionReasonCodes);
        Assert.True(occurrence.IsException);
        CalendarEventExceptionHelper.SetReason(occurrence, CalendarEventExceptionReasonCode.IsAway, false);
        Assert.False(occurrence.IsException);
    }

    [Fact]
    public void UpdateExceptionFlag_StandaloneWithNoBaseline_NeverAddsTimeReason()
    {
        var occurrence = new Event { StartAtUtc = Start, EndAtUtc = Start.AddHours(8) };
        CalendarEventExceptionHelper.UpdateExceptionFlag(occurrence);
        Assert.Empty(occurrence.ExceptionReasonCodes);
        Assert.False(occurrence.IsException);
    }

    [Fact]
    public void SetReason_RemovingAway_PreservesTimeReasonWithoutDuplicates()
    {
        var occurrence = CreateOccurrence();
        occurrence.StartAtUtc = Start.AddHours(1);
        CalendarEventExceptionHelper.UpdateExceptionFlag(occurrence);
        CalendarEventExceptionHelper.SetReason(occurrence, CalendarEventExceptionReasonCode.IsAway, true);
        CalendarEventExceptionHelper.SetReason(occurrence, CalendarEventExceptionReasonCode.IsAway, true);
        Assert.Equal(new[] { "DateTimeChanged", "IsAway" }, occurrence.ExceptionReasonCodes);

        CalendarEventExceptionHelper.SetReason(occurrence, CalendarEventExceptionReasonCode.IsAway, false);
        Assert.Equal(new[] { "DateTimeChanged" }, occurrence.ExceptionReasonCodes);
        Assert.True(occurrence.IsException);
    }

    [Fact]
    public void ExceptionReasonCodes_AssigningRepeatedCodes_KeepsOnlyDistinctNames()
    {
        var occurrence = new Event { ExceptionReasonCodes = ["DateTimeChanged", "IsAway", "IsAway"] };
        Assert.Equal(new[] { "DateTimeChanged", "IsAway" }, occurrence.ExceptionReasonCodes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void UpdateExceptionFlag_NullBaselineComparison_MatchesExistingSemantics(bool end, bool baselineEnd)
    {
        var occurrence = CreateOccurrence();
        occurrence.EndAtUtc = end ? Start.AddHours(8) : null;
        occurrence.SeriesEndAtUtc = baselineEnd ? Start.AddHours(8) : null;
        CalendarEventExceptionHelper.UpdateExceptionFlag(occurrence);
        Assert.Equal(end != baselineEnd, occurrence.IsException);
    }

    private static Event CreateOccurrence() =>
        new()
        {
            EventSeriesId = 42,
            StartAtUtc = Start,
            EndAtUtc = Start.AddHours(8),
            SeriesStartAtUtc = Start,
            SeriesEndAtUtc = Start.AddHours(8),
        };
}
