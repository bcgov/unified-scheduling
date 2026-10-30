using Microsoft.Extensions.Options;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Mappings;

namespace Unified.Tests.Scheduling.Services;

public sealed class SchedulingLoanOutEventMapperTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 1, 16, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = new("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void ToShiftEntryRequest_MapsExistingShiftOccurrenceAndOverridesLocationAndUsers()
    {
        var sourceSeries = new ShiftSeries { EventSeries = new EventSeries { Title = "Source series" } };
        var entry = new ShiftEntry
        {
            Id = 42,
            ShiftSeriesId = 77,
            ShiftSeries = sourceSeries,
            Event = new Event
            {
                Id = 101,
                EventSeriesId = 88,
                EventSeries = sourceSeries.EventSeries,
                Title = "Source shift",
                Description = "Description",
                Notes = "Notes",
                Color = "#123456",
                StartAtUtc = Start,
                EndAtUtc = Start.AddHours(4),
                SeriesStartAtUtc = Start,
                SeriesEndAtUtc = Start.AddHours(4),
                TimeZoneId = "America/Vancouver",
                AllDay = true,
                LocationId = 5,
            },
            Users = [new ShiftEntryUser { UserId = UserId }],
        };

        var request = CreateMapper().ToShiftEntryRequest(entry, 10, [UserId]);

        Assert.Equal("Source shift", request.Title);
        Assert.Equal("Description", request.Description);
        Assert.Equal("Notes", request.Notes);
        Assert.Equal("#123456", request.Color);
        Assert.Equal(Start, request.StartAtUtc);
        Assert.Equal(Start.AddHours(4), request.EndAtUtc);
        Assert.Null(request.SeriesStartAtUtc);
        Assert.Null(request.SeriesEndAtUtc);
        Assert.Equal("America/Vancouver", request.TimeZoneId);
        Assert.True(request.AllDay);
        Assert.Equal(10, request.LocationId);
        Assert.Equal(UserId, Assert.Single(request.UserIds));
        Assert.Null(request.ShiftSeriesId);
        Assert.Null(request.AssignmentEntryLinks);
        Assert.Equal(77, entry.ShiftSeriesId);
        Assert.Equal(88, entry.Event.EventSeriesId);
        Assert.Equal(Start, entry.Event.SeriesStartAtUtc);
        Assert.Equal(Start.AddHours(4), entry.Event.SeriesEndAtUtc);
    }

    [Fact]
    public void ToShiftSeriesRequest_ClonesOnlyOccurrencesInLoanedPeriodAtNewLocation()
    {
        var sourceSeries = new ShiftSeries
        {
            EventSeries = new EventSeries
            {
                Title = "Source series",
                Description = "Description",
                Notes = "Notes",
                Color = "#123456",
                StartAtUtc = Start,
                EndAtUtc = Start.AddHours(4),
                TimeZoneId = "America/Vancouver",
                AllDay = true,
                LocationId = 5,
                RecurrenceRule = "FREQ=DAILY;COUNT=10;UNTIL=20260620T160000Z",
            },
        };
        var occurrences = new[]
        {
            new ShiftEntry { Event = new Event { StartAtUtc = Start.AddDays(2) } },
            new ShiftEntry { Event = new Event { StartAtUtc = Start.AddDays(3) } },
        };

        var request = CreateMapper().ToShiftSeriesRequest(sourceSeries, occurrences, 10, [UserId]);

        Assert.Equal("Source series", request.Title);
        Assert.Equal("Description", request.Description);
        Assert.Equal("Notes", request.Notes);
        Assert.Equal("#123456", request.Color);
        Assert.Equal(Start.AddDays(2), request.StartAtUtc);
        Assert.Equal(Start.AddDays(2).AddHours(4), request.EndAtUtc);
        Assert.Equal("America/Vancouver", request.TimeZoneId);
        Assert.True(request.AllDay);
        Assert.Equal(10, request.LocationId);
        Assert.Equal(UserId, Assert.Single(request.UserIds));
        Assert.Equal("FREQ=DAILY;COUNT=2", request.RecurrenceRule);
    }

    [Theory]
    [InlineData("2026-11-01T07:00:00Z", "2026-11-01T16:00:00Z", "2026-11-03T08:00:00Z", "2026-11-03T16:00:00Z")]
    [InlineData("2026-03-08T08:00:00Z", "2026-03-08T15:00:00Z", "2026-03-10T07:00:00Z", "2026-03-10T15:00:00Z")]
    [InlineData("2026-03-07T08:00:00Z", "2026-03-07T16:00:00Z", "2026-03-08T08:00:00Z", "2026-03-08T15:00:00Z")]
    public void ToShiftSeriesRequest_PreservesLocalDurationAcrossDst(
        string sourceStart,
        string sourceEnd,
        string occurrenceStart,
        string expectedEnd
    )
    {
        var source = new ShiftSeries
        {
            EventSeries = new EventSeries
            {
                StartAtUtc = DateTimeOffset.Parse(sourceStart),
                EndAtUtc = DateTimeOffset.Parse(sourceEnd),
                // Exercise the configured resolver fallback when no explicit time zone is set.
                TimeZoneId = null,
                RecurrenceRule = "FREQ=DAILY;COUNT=10",
            },
        };
        var occurrence = new ShiftEntry { Event = new Event { StartAtUtc = DateTimeOffset.Parse(occurrenceStart) } };

        var request = CreateMapper().ToShiftSeriesRequest(source, [occurrence], 10, [UserId]);

        Assert.Equal(DateTimeOffset.Parse(expectedEnd), request.EndAtUtc);
    }

    [Theory]
    [InlineData("2026-11-02T17:00:00Z")]
    [InlineData("2026-11-03T04:00:00Z")]
    [InlineData("2026-11-02T17:00:00.123Z")]
    public void ToShiftSeriesRequest_IncludesFinalThursday(string startAtUtc)
    {
        var timeZoneService = new TimeZoneService();
        var resolver = new CalendarTimeZoneResolver(
            Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "America/Vancouver" }),
            timeZoneService
        );
        var expander = new IcalNetRecurrenceExpander(timeZoneService, resolver);
        var start = DateTimeOffset.Parse(startAtUtc);
        var source = new ShiftSeries
        {
            EventSeries = new EventSeries
            {
                StartAtUtc = start,
                EndAtUtc = start.AddHours(7),
                TimeZoneId = "America/Vancouver",
                RecurrenceRule = "FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR;COUNT=19",
            },
        };
        var originals = expander.ExpandAllBounded(source.EventSeries, 400).ToList();
        var entries = originals
            .Select(occurrence => new ShiftEntry
            {
                Event = new Event { StartAtUtc = occurrence.StartAtUtc, EndAtUtc = occurrence.EndAtUtc },
            })
            .ToList();

        var request = new SchedulingLoanOutEventMapper(resolver, timeZoneService).ToShiftSeriesRequest(
            source,
            entries,
            10,
            [UserId]
        );
        var copied = expander.ExpandAllBounded(
            new EventSeries
            {
                StartAtUtc = request.StartAtUtc,
                EndAtUtc = request.EndAtUtc,
                TimeZoneId = request.TimeZoneId,
                RecurrenceRule = request.RecurrenceRule,
            },
            400
        );

        Assert.Equal(19, copied.Count);
        Assert.Equal(
            originals.Select(item => (item.StartAtUtc, item.EndAtUtc)),
            copied.Select(item => (item.StartAtUtc, item.EndAtUtc))
        );
    }

    [Theory]
    [InlineData(10)]
    [InlineData(1)]
    public void ToShiftSeriesRequest_PermanentVancouverTime_IncludesFinalCopiedShift(int count)
    {
        var time = new TimeZoneService();
        var resolver = new PermanentVancouverResolver();
        var expander = new IcalNetRecurrenceExpander(time, resolver);
        var source = new ShiftSeries
        {
            EventSeries = new EventSeries
            {
                StartAtUtc = DateTimeOffset.Parse("2026-11-02T16:00:00Z"),
                EndAtUtc = DateTimeOffset.Parse("2026-11-03T00:00:00Z"),
                TimeZoneId = "America/Vancouver",
                RecurrenceRule = "FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR;COUNT=20",
            },
        };
        var entries = expander
            .ExpandAllBounded(source.EventSeries, 400)
            .Skip(count == 1 ? 9 : 0)
            .Take(count)
            .Select(occurrence => new ShiftEntry
            {
                Event = new Event { StartAtUtc = occurrence.StartAtUtc, EndAtUtc = occurrence.EndAtUtc },
            })
            .ToList();

        var request = new SchedulingLoanOutEventMapper(resolver, time).ToShiftSeriesRequest(
            source,
            entries,
            10,
            [UserId]
        );
        var validation = new IcalNetRecurrenceRuleValidator(expander, time, resolver).Validate(
            request.RecurrenceRule!,
            request.StartAtUtc,
            request.EndAtUtc,
            request.TimeZoneId,
            new RecurrenceValidationOptions
            {
                MaximumDuration = TimeSpan.FromDays(365),
                MaximumOccurrences = 400,
                RequireBoundedRule = true,
            }
        );
        var copied = expander.ExpandAllBounded(
            new EventSeries
            {
                StartAtUtc = request.StartAtUtc,
                EndAtUtc = request.EndAtUtc,
                TimeZoneId = request.TimeZoneId,
                RecurrenceRule = request.RecurrenceRule,
            },
            400
        );

        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        Assert.Equal(count, copied.Count);
        Assert.Equal(
            entries.Select(entry => (entry.Event!.StartAtUtc, entry.Event.EndAtUtc)),
            copied.Select(occurrence => (occurrence.StartAtUtc, occurrence.EndAtUtc))
        );
    }

    private sealed class PermanentVancouverResolver : ICalendarTimeZoneResolver
    {
        private readonly TimeZoneInfo zone = TimeZoneInfo.CreateCustomTimeZone(
            "America/Vancouver",
            TimeSpan.FromHours(-7),
            "Pacific time",
            "Pacific time"
        );

        public TimeZoneInfo Resolve(string? requestedTimeZoneId, string? fallbackTimeZoneId = null) => zone;
    }

    private static SchedulingLoanOutEventMapper CreateMapper()
    {
        var timeZoneService = new TimeZoneService();
        return new SchedulingLoanOutEventMapper(
            new CalendarTimeZoneResolver(
                Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "America/Vancouver" }),
                timeZoneService
            ),
            timeZoneService
        );
    }
}
