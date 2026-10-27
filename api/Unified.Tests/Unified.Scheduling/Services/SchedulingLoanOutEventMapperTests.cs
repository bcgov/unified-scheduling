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
            ShiftSeries = sourceSeries,
            Event = new Event
            {
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

        var request = new SchedulingLoanOutEventMapper().ToShiftEntryRequest(entry, 10, [UserId]);

        Assert.Equal("Source shift", request.Title);
        Assert.Equal("Description", request.Description);
        Assert.Equal("Notes", request.Notes);
        Assert.Equal("#123456", request.Color);
        Assert.Equal(Start, request.StartAtUtc);
        Assert.Equal(Start.AddHours(4), request.EndAtUtc);
        Assert.Equal(Start, request.SeriesStartAtUtc);
        Assert.Equal(Start.AddHours(4), request.SeriesEndAtUtc);
        Assert.Equal("America/Vancouver", request.TimeZoneId);
        Assert.True(request.AllDay);
        Assert.Equal(10, request.LocationId);
        Assert.Equal(UserId, Assert.Single(request.UserIds));
        Assert.Null(request.ShiftSeriesId);
        Assert.Null(request.AssignmentEntryLinks);
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

        var request = new SchedulingLoanOutEventMapper().ToShiftSeriesRequest(sourceSeries, occurrences, 10, [UserId]);

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
        Assert.Equal("FREQ=DAILY;UNTIL=20260604T160000Z", request.RecurrenceRule);
    }
}
