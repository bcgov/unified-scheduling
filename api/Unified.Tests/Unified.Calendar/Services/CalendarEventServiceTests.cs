using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Unified.Calendar;
using Unified.Calendar.Holidays;
using Unified.Calendar.Models;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.Calendar.Services;

public class CalendarEventServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private UnifiedDbContext _dbContext = null!;
    private CalendarEventService _service = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.CreateFunction("now", () => DateTimeOffset.UtcNow.ToString("O"));
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        var options = new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(_connection).Options;

        _dbContext = new SqliteTestUnifiedDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await SeedLookupDataAsync();
        var timeZoneService = new TimeZoneService();
        _service = new CalendarEventService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CalendarEventService>.Instance,
            _dbContext,
            new StatutoryHolidayCalendarDataProvider(new BcStatutoryHolidayCalculator(), timeZoneService),
            new CalendarTimeZoneResolver(
                Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "America/Vancouver" }),
                timeZoneService
            ),
            timeZoneService
        );
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task GetEventsAsync_WhenNoLocationFilter_UsesStartEndExclusiveOverlapBoundaries()
    {
        // Arrange
        var request = CreateRequest(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 4));
        var rangeStartAtUtc = new DateTimeOffset(2026, 6, 1, 7, 0, 0, TimeSpan.Zero);
        var rangeEndAtUtc = new DateTimeOffset(2026, 6, 5, 7, 0, 0, TimeSpan.Zero);

        _dbContext.Events.AddRange(
            CreateEvent(5, "Ends at request start", rangeStartAtUtc.AddHours(-2), rangeStartAtUtc),
            CreateEvent(4, "Starts at request end", rangeEndAtUtc, rangeEndAtUtc.AddHours(1)),
            CreateEvent(3, "Open ended on range start", rangeStartAtUtc, endAtUtc: null),
            CreateEvent(
                2,
                "Non-calendar",
                rangeStartAtUtc.AddHours(2),
                rangeStartAtUtc.AddHours(3),
                sourceModule: "other"
            ),
            CreateEvent(
                1,
                "Overlapping event",
                rangeStartAtUtc.AddHours(1),
                rangeStartAtUtc.AddHours(2),
                description: "Description",
                notes: "Notes",
                color: "calendar.deadline",
                seriesStartAtUtc: rangeStartAtUtc,
                seriesEndAtUtc: rangeEndAtUtc,
                timeZoneId: "America/Vancouver",
                isException: true,
                eventTypeCode: CalendarCodeMappings.ToDbCode(CalendarEventTypeCode.Deadline),
                statusTypeCode: CalendarCodeMappings.ToDbCode(CalendarEventStatusTypeCode.Active),
                cancelledAt: rangeStartAtUtc.AddHours(3),
                cancellationReason: "Reason",
                locationId: 8
            )
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetEventsAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Collection(
            result,
            first =>
            {
                Assert.Equal("3", first.Id);
                Assert.Equal("Open ended on range start", first.Title);
                Assert.Null(first.EndAtUtc);
            },
            second =>
            {
                Assert.Equal("1", second.Id);
                Assert.Equal("Overlapping event", second.Title);
                Assert.Equal("Description", second.Description);
                Assert.Equal("Notes", second.Notes);
                Assert.Equal("calendar.deadline", second.Color);
                Assert.Equal("America/Vancouver", second.TimeZoneId);
                Assert.True(second.IsException);
                Assert.Equal(CalendarEventType.CalendarEvent, second.Type);
                Assert.Equal(CalendarEventStatus.Active, second.Status);
                Assert.Equal(CalendarEventTypeCode.Deadline, second.EventTypeCode);
                Assert.Equal(CalendarEventStatusTypeCode.Active, second.StatusTypeCode);
                Assert.Equal("Reason", second.CancellationReason);
                Assert.Equal(8, second.LocationId);
            }
        );
    }

    [Fact]
    public async Task GetEventsAsync_WhenLocationFilterProvided_ReturnsSharedAndMatchingLocationsOnly()
    {
        // Arrange
        var request = CreateRequest(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 9), locationId: 5);
        var rangeStartAtUtc = new DateTimeOffset(2026, 6, 1, 7, 0, 0, TimeSpan.Zero);

        _dbContext.Events.AddRange(
            CreateEvent(1, "Shared", rangeStartAtUtc, rangeStartAtUtc.AddHours(1), locationId: null),
            CreateEvent(2, "Matching", rangeStartAtUtc.AddHours(1), rangeStartAtUtc.AddHours(2), locationId: 5),
            CreateEvent(3, "Different", rangeStartAtUtc.AddHours(2), rangeStartAtUtc.AddHours(3), locationId: 9)
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetEventsAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["1", "2"], result.Select(x => x.Id).ToArray());
    }

    [Fact]
    public async Task GetEventsAsync_WhenRangeContainsManuallyPersistedHoliday_ReturnsPersistedAndDynamicHolidays()
    {
        // Arrange
        var request = CreateRequest(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1));
        _dbContext.Events.AddRange(
            CreateEvent(
                1,
                "Persisted event",
                new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 7, 1, 13, 0, 0, TimeSpan.Zero)
            ),
            CreateEvent(
                2,
                "Manually persisted holiday",
                new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 7, 2, 0, 0, 0, TimeSpan.Zero),
                allDay: true,
                eventTypeCode: CalendarCodeMappings.ToDbCode(CalendarEventTypeCode.Holiday),
                statusTypeCode: CalendarCodeMappings.ToDbCode(CalendarEventStatusTypeCode.Active)
            )
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetEventsAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(result, calendarEvent => calendarEvent.Id == "1");
        var persistedHoliday = Assert.Single(result, calendarEvent => calendarEvent.Id == "2");
        Assert.Equal("Manually persisted holiday", persistedHoliday.Title);
        Assert.Equal(CalendarEventTypeCode.Holiday, persistedHoliday.EventTypeCode);
        Assert.Null(persistedHoliday.HolidayType);
        Assert.False(persistedHoliday.IsReadOnly);

        var holiday = Assert.Single(result, calendarEvent => calendarEvent.HolidayType.HasValue);
        Assert.Equal("stat-holiday:CanadaDay:2026-07-01", holiday.Id);
        Assert.Equal("Canada Day", holiday.Title);
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 7, 0, 0, TimeSpan.Zero), holiday.StartAtUtc);
        Assert.Equal(new DateTimeOffset(2026, 7, 2, 7, 0, 0, TimeSpan.Zero), holiday.EndAtUtc);
        Assert.Equal("America/Vancouver", holiday.TimeZoneId);
        Assert.Equal(StatutoryHolidayType.CanadaDay, holiday.HolidayType);
        Assert.Equal(CalendarEventTypeCode.Holiday, holiday.EventTypeCode);
        Assert.Equal(CalendarEventStatusTypeCode.Active, holiday.StatusTypeCode);
        Assert.Equal(global::Unified.Calendar.CalendarConstants.SourceModule, holiday.SourceModule);
        Assert.True(holiday.AllDay);
        Assert.True(holiday.IsReadOnly);
        Assert.Equal(2, await _dbContext.Events.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetEventsAsync_WhenRangeSpansYears_ReturnsOnlyHolidaysInsideInclusiveRange()
    {
        var request = CreateRequest(new DateOnly(2026, 12, 27), new DateOnly(2027, 1, 1));

        var result = await _service.GetEventsAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(
            ["stat-holiday:BoxingDay:2026-12-28", "stat-holiday:NewYearsDay:2027-01-01"],
            result.Select(calendarEvent => calendarEvent.Id)
        );
    }

    [Fact]
    public async Task GetEventsAsync_WhenLocationHasTimeZone_MapsHolidayAcrossLocalMidnights()
    {
        var request = CreateRequest(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1), locationId: 9);

        var result = await _service.GetEventsAsync(request, TestContext.Current.CancellationToken);

        var holiday = Assert.Single(result);
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 4, 0, 0, TimeSpan.Zero), holiday.StartAtUtc);
        Assert.Equal(new DateTimeOffset(2026, 7, 2, 4, 0, 0, TimeSpan.Zero), holiday.EndAtUtc);
        Assert.Equal("America/Toronto", holiday.TimeZoneId);
    }

    [Fact]
    public async Task GetEventsAsync_WhenLocationAndRequestTimeZonesDiffer_UsesRequestedTimeZone()
    {
        var request = CreateRequest(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 1),
            locationId: 9,
            timeZoneId: "America/Vancouver"
        );

        var result = await _service.GetEventsAsync(request, TestContext.Current.CancellationToken);

        var holiday = Assert.Single(result);
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 7, 0, 0, TimeSpan.Zero), holiday.StartAtUtc);
        Assert.Equal("America/Vancouver", holiday.TimeZoneId);
    }

    [Fact]
    public async Task GetEventsAsync_WhenConfiguredLocationTimeZoneIsInvalid_Throws()
    {
        var request = CreateRequest(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1), locationId: 10);

        await Assert.ThrowsAsync<TimeZoneNotFoundException>(() =>
            _service.GetEventsAsync(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task GetEventsAsync_WhenLocationHasNoConfiguredTimeZone_UsesRequestedTimeZone()
    {
        var request = CreateRequest(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 1),
            locationId: 11,
            timeZoneId: "America/Toronto"
        );

        var result = await _service.GetEventsAsync(request, TestContext.Current.CancellationToken);

        var holiday = Assert.Single(result);
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 4, 0, 0, TimeSpan.Zero), holiday.StartAtUtc);
        Assert.Equal("America/Toronto", holiday.TimeZoneId);
    }

    private static CalendarEventsRequest CreateRequest(
        DateOnly startDate,
        DateOnly endDate,
        int? locationId = null,
        string? timeZoneId = null
    ) =>
        new()
        {
            StartDate = startDate,
            EndDate = endDate,
            LocationId = locationId,
            TimeZoneId = timeZoneId,
        };

    private static Event CreateEvent(
        int id,
        string title,
        DateTimeOffset startAtUtc,
        DateTimeOffset? endAtUtc,
        string sourceModule = Db.Models.Calendar.CalendarConstants.SourceModule,
        string? description = null,
        string? notes = null,
        string? color = null,
        DateTimeOffset? seriesStartAtUtc = null,
        DateTimeOffset? seriesEndAtUtc = null,
        string? timeZoneId = null,
        bool allDay = false,
        bool isException = false,
        string? eventTypeCode = null,
        string? statusTypeCode = null,
        DateTimeOffset? cancelledAt = null,
        string? cancellationReason = null,
        int? locationId = null
    ) =>
        new()
        {
            Id = id,
            Title = title,
            Description = description,
            Notes = notes,
            Color = color,
            StartAtUtc = startAtUtc,
            EndAtUtc = endAtUtc,
            SeriesStartAtUtc = seriesStartAtUtc,
            SeriesEndAtUtc = seriesEndAtUtc,
            TimeZoneId = timeZoneId,
            AllDay = allDay,
            IsException = isException,
            EventTypeCode = eventTypeCode ?? CalendarCodeMappings.ToDbCode(CalendarEventTypeCode.General),
            StatusTypeCode = statusTypeCode ?? CalendarCodeMappings.ToDbCode(CalendarEventStatusTypeCode.Draft),
            CancelledAt = cancelledAt,
            CancellationReason = cancellationReason,
            SourceModule = sourceModule,
            LocationId = locationId,
        };

    private async Task SeedLookupDataAsync()
    {
        _dbContext.EventTypes.AddRange(
            new EventType
            {
                Code = CalendarCodeMappings.ToDbCode(CalendarEventTypeCode.General),
                Description = "General",
                EffectiveDate = requestDate(),
            },
            new EventType
            {
                Code = CalendarCodeMappings.ToDbCode(CalendarEventTypeCode.Holiday),
                Description = "Holiday",
                EffectiveDate = requestDate(),
            },
            new EventType
            {
                Code = CalendarCodeMappings.ToDbCode(CalendarEventTypeCode.Deadline),
                Description = "Deadline",
                EffectiveDate = requestDate(),
            }
        );

        _dbContext.EventStatusTypes.AddRange(
            new EventStatusType
            {
                Code = CalendarCodeMappings.ToDbCode(CalendarEventStatusTypeCode.Draft),
                Description = "Draft",
                EffectiveDate = requestDate(),
            },
            new EventStatusType
            {
                Code = CalendarCodeMappings.ToDbCode(CalendarEventStatusTypeCode.Active),
                Description = "Active",
                EffectiveDate = requestDate(),
            },
            new EventStatusType
            {
                Code = CalendarCodeMappings.ToDbCode(CalendarEventStatusTypeCode.Cancelled),
                Description = "Cancelled",
                EffectiveDate = requestDate(),
            }
        );

        _dbContext.Locations.AddRange(
            new Location
            {
                Id = 5,
                AgencyId = "A5",
                Name = "Location 5",
                Timezone = "America/Vancouver",
            },
            new Location
            {
                Id = 8,
                AgencyId = "A8",
                Name = "Location 8",
                Timezone = "America/Vancouver",
            },
            new Location
            {
                Id = 9,
                AgencyId = "A9",
                Name = "Location 9",
                Timezone = "America/Toronto",
            },
            new Location
            {
                Id = 10,
                AgencyId = "A10",
                Name = "Location 10",
                Timezone = "Not/AZone",
            },
            new Location
            {
                Id = 11,
                AgencyId = "A11",
                Name = "Location 11",
                Timezone = string.Empty,
            }
        );

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        static DateTimeOffset requestDate() => new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
