using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Unified.Calendar;
using Unified.Calendar.Models;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
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
        _service = new CalendarEventService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CalendarEventService>.Instance,
            _dbContext,
            CreateCalendarDateTimeService()
        );
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task GetCalendarDataAsync_WhenNoLocationFilter_UsesStartEndExclusiveOverlapBoundaries()
    {
        // Arrange
        var request = CreateRequest(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5));
        var rangeStartAtUtc = new DateTimeOffset(2026, 6, 1, 7, 0, 0, TimeSpan.Zero);
        var rangeEndAtUtc = new DateTimeOffset(2026, 6, 6, 7, 0, 0, TimeSpan.Zero);
        var endDateStartAtUtc = new DateTimeOffset(2026, 6, 5, 7, 0, 0, TimeSpan.Zero);

        _dbContext.Events.AddRange(
            CreateEvent(5, "Ends at request start", rangeStartAtUtc.AddHours(-2), rangeStartAtUtc),
            CreateEvent(4, "Starts on request end date", endDateStartAtUtc, endDateStartAtUtc.AddHours(1)),
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
        var result = await _service.GetCalendarDataAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("calendar", result.ModuleId);
        Assert.Equal("calendar.events", result.ContributionId);
        Assert.Collection(
            result.Events,
            first =>
            {
                Assert.Equal(3, first.Id);
                Assert.Equal("Open ended on range start", first.Title);
                Assert.Null(first.EndAtUtc);
            },
            second =>
            {
                Assert.Equal(1, second.Id);
                Assert.Equal("Overlapping event", second.Title);
                Assert.Equal("Description", second.Description);
                Assert.Equal("Notes", second.Notes);
                Assert.Equal("calendar.deadline", second.Color);
                Assert.Equal("America/Vancouver", second.TimeZoneId);
                Assert.True(second.IsException);
                Assert.Equal(CalendarEventTypeCode.Deadline, second.EventTypeCode);
                Assert.Equal(CalendarEventStatusTypeCode.Active, second.StatusTypeCode);
                Assert.Equal("Reason", second.CancellationReason);
                Assert.Equal(8, second.LocationId);
            },
            third =>
            {
                Assert.Equal(4, third.Id);
                Assert.Equal("Starts on request end date", third.Title);
            }
        );
    }

    [Fact]
    public async Task GetCalendarDataAsync_WhenLocationFilterProvided_ReturnsSharedAndMatchingLocationsOnly()
    {
        // Arrange
        var request = CreateRequest(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 10), locationId: 5);
        var rangeStartAtUtc = new DateTimeOffset(2026, 6, 1, 7, 0, 0, TimeSpan.Zero);

        _dbContext.Events.AddRange(
            CreateEvent(1, "Shared", rangeStartAtUtc, rangeStartAtUtc.AddHours(1), locationId: null),
            CreateEvent(2, "Matching", rangeStartAtUtc.AddHours(1), rangeStartAtUtc.AddHours(2), locationId: 5),
            CreateEvent(3, "Different", rangeStartAtUtc.AddHours(2), rangeStartAtUtc.AddHours(3), locationId: 9)
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetCalendarDataAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([1, 2], result.Events.Select(x => x.Id).ToArray());
    }

    [Fact]
    public async Task GetCalendarDataAsync_WhenEventStartsOnEndDate_IncludesWholeEndDate()
    {
        // Arrange
        var request = CreateRequest(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 1));
        var endDateMorningUtc = new DateTimeOffset(2026, 6, 1, 15, 0, 0, TimeSpan.Zero);
        _dbContext.Events.AddRange(
            CreateEvent(1, "On end date", endDateMorningUtc, endDateMorningUtc.AddHours(1)),
            CreateEvent(
                2,
                "After end date",
                new DateTimeOffset(2026, 6, 2, 7, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 2, 8, 0, 0, TimeSpan.Zero)
            )
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetCalendarDataAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var item = Assert.Single(result.Events);
        Assert.Equal(1, item.Id);
        Assert.Equal("On end date", item.Title);
    }

    private static CalendarDataRequest CreateRequest(DateOnly startDate, DateOnly endDate, int? locationId = null) =>
        new()
        {
            StartDate = startDate,
            EndDate = endDate,
            LocationId = locationId,
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
                Timezone = "America/Vancouver",
            }
        );

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        static DateTimeOffset requestDate() => new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }

    private static CalendarDateTimeService CreateCalendarDateTimeService() =>
        new(Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "America/Vancouver" }));
}
