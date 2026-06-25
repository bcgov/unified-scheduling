using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Unified.Calendar;
using Unified.Calendar.Models;
using Unified.Calendar.Services;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.Calendar.Services;

using ApiCalendarConstants = Unified.Calendar.CalendarConstants;

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
            _dbContext
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
        var request = CreateRequest(
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 5, 0, 0, 0, TimeSpan.Zero)
        );

        _dbContext.Events.AddRange(
            CreateEvent(5, "Ends at request start", request.StartDate.AddHours(-2), request.StartDate),
            CreateEvent(4, "Starts at request end", request.EndDate, request.EndDate.AddHours(1)),
            CreateEvent(3, "Open ended on range start", request.StartDate, endAtUtc: null),
            CreateEvent(
                2,
                "Non-calendar",
                request.StartDate.AddHours(2),
                request.StartDate.AddHours(3),
                sourceModule: "other"
            ),
            CreateEvent(
                1,
                "Overlapping event",
                request.StartDate.AddHours(1),
                request.StartDate.AddHours(2),
                description: "Description",
                notes: "Notes",
                color: "calendar.deadline",
                seriesStartAtUtc: request.StartDate,
                seriesEndAtUtc: request.EndDate,
                timeZoneId: "America/Vancouver",
                isException: true,
                eventTypeCode: CalendarCodeMappings.ToDbCode(CalendarEventTypeCode.Deadline),
                statusTypeCode: CalendarCodeMappings.ToDbCode(CalendarEventStatusTypeCode.Active),
                cancelledAt: request.StartDate.AddHours(3),
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
            }
        );
    }

    [Fact]
    public async Task GetEventsAsync_WhenLocationFilterProvided_ReturnsSharedAndMatchingLocationsOnly()
    {
        // Arrange
        var request = CreateRequest(
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero),
            locationId: 5
        );

        _dbContext.Events.AddRange(
            CreateEvent(1, "Shared", request.StartDate, request.StartDate.AddHours(1), locationId: null),
            CreateEvent(2, "Matching", request.StartDate.AddHours(1), request.StartDate.AddHours(2), locationId: 5),
            CreateEvent(3, "Different", request.StartDate.AddHours(2), request.StartDate.AddHours(3), locationId: 9)
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetEventsAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([1, 2], result.Select(x => x.Id).ToArray());
    }

    private static CalendarEventsRequest CreateRequest(
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        int? locationId = null
    ) =>
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
        string sourceModule = ApiCalendarConstants.SourceModule,
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
}
