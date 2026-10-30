using Unified.Calendar.Factories;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Leave;
using Unified.Leave.Factories;
using Unified.Leave.Models.Scheduling;

namespace Unified.Tests.Leave.Factories;

public sealed class LeaveEventFactoryTests
{
    private static readonly DateTimeOffset Start = new(2025, 3, 3, 9, 0, 0, TimeSpan.Zero);

    private readonly ICalendarEventSeriesFactory<NormalizedLeaveSeriesRequest, LeaveSeries> _factory =
        new LeaveEventFactory();

    [Fact]
    public void HasRecurrenceChanged_IgnoresWhitespaceAndDetectsChanges()
    {
        var series = CreateSeries();
        var request = CreateRequest(" FREQ=DAILY;COUNT=2 ");

        Assert.False(_factory.HasRecurrenceChanged(series, request));
        Assert.True(_factory.HasRecurrenceChanged(series, request with { RecurrenceRule = "FREQ=DAILY;COUNT=3" }));
    }

    [Fact]
    public void HasRecurrenceChanged_DetectsStartEndTimeZoneAndAllDayChanges()
    {
        var series = CreateSeries();
        var request = CreateRequest("FREQ=DAILY;COUNT=2");

        Assert.True(_factory.HasRecurrenceChanged(series, request with { StartAtUtc = Start.AddHours(1) }));
        Assert.True(_factory.HasRecurrenceChanged(series, request with { EndAtUtc = Start.AddHours(9) }));
        Assert.True(_factory.HasRecurrenceChanged(series, request with { TimeZoneId = "America/Vancouver" }));
        Assert.True(_factory.HasRecurrenceChanged(series, request with { AllDay = true }));
    }

    private static EventSeries CreateSeries() =>
        new()
        {
            Title = "T",
            RecurrenceRule = "FREQ=DAILY;COUNT=2",
            StartAtUtc = Start,
            EndAtUtc = Start.AddHours(8),
            TimeZoneId = "UTC",
        };

    private static NormalizedLeaveSeriesRequest CreateRequest(string recurrenceRule) =>
        new()
        {
            Title = "T",
            RecurrenceRule = recurrenceRule,
            StartAtUtc = Start,
            EndAtUtc = Start.AddHours(8),
            TimeZoneId = "UTC",
        };
}
