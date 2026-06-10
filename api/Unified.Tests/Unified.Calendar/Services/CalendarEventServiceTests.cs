using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Unified.Calendar.Models;
using Unified.Calendar.Services;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;

namespace Unified.Tests.Calendar.Services;

using ApiCalendarConstants = Unified.Calendar.CalendarConstants;
using ApiCalendarEventStatusTypeCodes = Unified.Calendar.CalendarEventStatusTypeCodes;
using ApiCalendarEventTypeCodes = Unified.Calendar.CalendarEventTypeCodes;

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

        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new SqliteTestUnifiedDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await SeedLookupDataAsync();
        _service = new CalendarEventService(Microsoft.Extensions.Logging.Abstractions.NullLogger<CalendarEventService>.Instance, _dbContext);
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
        var request = new CalendarEventsRequest
        {
            StartDate = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 6, 5, 0, 0, 0, TimeSpan.Zero),
        };

        _dbContext.Events.AddRange(
            new Event
            {
                Id = 5,
                Title = "Ends at request start",
                StartAtUtc = request.StartDate.AddHours(-2),
                EndAtUtc = request.StartDate,
                SourceModule = ApiCalendarConstants.SourceModule,
            },
            new Event
            {
                Id = 4,
                Title = "Starts at request end",
                StartAtUtc = request.EndDate,
                EndAtUtc = request.EndDate.AddHours(1),
                SourceModule = ApiCalendarConstants.SourceModule,
            },
            new Event
            {
                Id = 3,
                Title = "Open ended on range start",
                StartAtUtc = request.StartDate,
                EndAtUtc = null,
                SourceModule = ApiCalendarConstants.SourceModule,
                LocationId = null,
            },
            new Event
            {
                Id = 2,
                Title = "Non-calendar",
                StartAtUtc = request.StartDate.AddHours(2),
                EndAtUtc = request.StartDate.AddHours(3),
                SourceModule = "other",
            },
            new Event
            {
                Id = 1,
                Title = "Overlapping event",
                Description = "Description",
                Notes = "Notes",
                Color = "calendar.deadline",
                StartAtUtc = request.StartDate.AddHours(1),
                EndAtUtc = request.StartDate.AddHours(2),
                SeriesStartAtUtc = request.StartDate,
                SeriesEndAtUtc = request.EndDate,
                TimeZoneId = "America/Vancouver",
                AllDay = false,
                IsException = true,
                EventTypeCode = ApiCalendarEventTypeCodes.Deadline,
                StatusTypeCode = ApiCalendarEventStatusTypeCodes.Active,
                CancelledAt = request.StartDate.AddHours(3),
                CancellationReason = "Reason",
                SourceModule = ApiCalendarConstants.SourceModule,
                Status = "published",
                LocationId = 8,
            }
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
                Assert.Equal(ApiCalendarEventTypeCodes.Deadline, second.EventTypeCode);
                Assert.Equal(ApiCalendarEventStatusTypeCodes.Active, second.StatusTypeCode);
                Assert.Equal("Reason", second.CancellationReason);
                Assert.Equal("published", second.Status);
                Assert.Equal(8, second.LocationId);
            }
        );
    }

    [Fact]
    public async Task GetEventsAsync_WhenLocationFilterProvided_ReturnsSharedAndMatchingLocationsOnly()
    {
        // Arrange
        var request = new CalendarEventsRequest
        {
            StartDate = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero),
            LocationId = 5,
        };

        _dbContext.Events.AddRange(
            new Event
            {
                Id = 1,
                Title = "Shared",
                StartAtUtc = request.StartDate,
                EndAtUtc = request.StartDate.AddHours(1),
                SourceModule = ApiCalendarConstants.SourceModule,
                LocationId = null,
            },
            new Event
            {
                Id = 2,
                Title = "Matching",
                StartAtUtc = request.StartDate.AddHours(1),
                EndAtUtc = request.StartDate.AddHours(2),
                SourceModule = ApiCalendarConstants.SourceModule,
                LocationId = 5,
            },
            new Event
            {
                Id = 3,
                Title = "Different",
                StartAtUtc = request.StartDate.AddHours(2),
                EndAtUtc = request.StartDate.AddHours(3),
                SourceModule = ApiCalendarConstants.SourceModule,
                LocationId = 9,
            }
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetEventsAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([1, 2], result.Select(x => x.Id).ToArray());
    }

    private async Task SeedLookupDataAsync()
    {
        _dbContext.EventTypes.AddRange(
            new EventType { Code = ApiCalendarEventTypeCodes.General, Description = "General", EffectiveDate = requestDate() },
            new EventType { Code = ApiCalendarEventTypeCodes.Holiday, Description = "Holiday", EffectiveDate = requestDate() },
            new EventType { Code = ApiCalendarEventTypeCodes.Deadline, Description = "Deadline", EffectiveDate = requestDate() }
        );

        _dbContext.EventStatusTypes.AddRange(
            new EventStatusType { Code = ApiCalendarEventStatusTypeCodes.Draft, Description = "Draft", EffectiveDate = requestDate() },
            new EventStatusType { Code = ApiCalendarEventStatusTypeCodes.Active, Description = "Active", EffectiveDate = requestDate() },
            new EventStatusType { Code = ApiCalendarEventStatusTypeCodes.Cancelled, Description = "Cancelled", EffectiveDate = requestDate() }
        );

        _dbContext.Locations.AddRange(
            new Location { Id = 5, AgencyId = "A5", Name = "Location 5", Timezone = "America/Vancouver" },
            new Location { Id = 8, AgencyId = "A8", Name = "Location 8", Timezone = "America/Vancouver" },
            new Location { Id = 9, AgencyId = "A9", Name = "Location 9", Timezone = "America/Vancouver" }
        );

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        static DateTimeOffset requestDate() => new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
