using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ApiCalendarConstants = Unified.Calendar.CalendarConstants;
using ApiCalendarEventStatusTypeCodes = Unified.Calendar.CalendarEventStatusTypeCodes;
using ApiCalendarEventTypeCodes = Unified.Calendar.CalendarEventTypeCodes;
using Unified.Calendar;
using Unified.Calendar.Options;
using Unified.Calendar.Seeders;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;

namespace Unified.Tests.Calendar.Seeders;

public sealed class CalendarSeedersTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private UnifiedDbContext _dbContext = null!;

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
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task EventTypeSeeder_SeedAsync_AddsMissingRowsAndUpdatesExistingRows()
    {
        // Arrange
        _dbContext.EventTypes.Add(
            new()
            {
                Code = ApiCalendarEventTypeCodes.General,
                Description = "Old",
                EffectiveDate = new DateTimeOffset(2019, 1, 1, 0, 0, 0, TimeSpan.Zero),
                ExpiryDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            }
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var seeder = new EventTypeSeeder(new NullLogger<EventTypeSeeder>());

        // Act
        await seeder.SeedAsync(_dbContext, TestContext.Current.CancellationToken);

        // Assert
        var eventTypes = await _dbContext.EventTypes.OrderBy(x => x.Code).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            [ApiCalendarEventTypeCodes.Deadline, ApiCalendarEventTypeCodes.General, ApiCalendarEventTypeCodes.Holiday],
            eventTypes.Select(x => x.Code)
        );

        var general = Assert.Single(eventTypes, x => x.Code == ApiCalendarEventTypeCodes.General);
        Assert.Equal("General", general.Description);
        Assert.Equal(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), general.EffectiveDate);
        Assert.Null(general.ExpiryDate);
    }

    [Fact]
    public async Task EventTypeSeeder_SeedAsync_IsIdempotent()
    {
        var seeder = new EventTypeSeeder(new NullLogger<EventTypeSeeder>());

        await seeder.SeedAsync(_dbContext, TestContext.Current.CancellationToken);
        await seeder.SeedAsync(_dbContext, TestContext.Current.CancellationToken);

        var eventTypes = await _dbContext.EventTypes.OrderBy(x => x.Code).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, eventTypes.Count);
        Assert.Equal(3, eventTypes.Select(x => x.Code).Distinct().Count());
    }

    [Fact]
    public async Task EventStatusTypeSeeder_SeedAsync_AddsMissingRowsAndUpdatesExistingRows()
    {
        // Arrange
        _dbContext.EventStatusTypes.Add(
            new()
            {
                Code = ApiCalendarEventStatusTypeCodes.Draft,
                Description = "Old",
                EffectiveDate = new DateTimeOffset(2019, 1, 1, 0, 0, 0, TimeSpan.Zero),
                ExpiryDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            }
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var seeder = new EventStatusTypeSeeder(new NullLogger<EventStatusTypeSeeder>());

        // Act
        await seeder.SeedAsync(_dbContext, TestContext.Current.CancellationToken);

        // Assert
        var statuses = await _dbContext.EventStatusTypes.OrderBy(x => x.Code).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            [ApiCalendarEventStatusTypeCodes.Active, ApiCalendarEventStatusTypeCodes.Cancelled, ApiCalendarEventStatusTypeCodes.Draft],
            statuses.Select(x => x.Code)
        );

        var draft = Assert.Single(statuses, x => x.Code == ApiCalendarEventStatusTypeCodes.Draft);
        Assert.Equal("Draft", draft.Description);
        Assert.Equal(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), draft.EffectiveDate);
        Assert.Null(draft.ExpiryDate);
    }

    [Fact]
    public async Task EventStatusTypeSeeder_SeedAsync_IsIdempotent()
    {
        var seeder = new EventStatusTypeSeeder(new NullLogger<EventStatusTypeSeeder>());

        await seeder.SeedAsync(_dbContext, TestContext.Current.CancellationToken);
        await seeder.SeedAsync(_dbContext, TestContext.Current.CancellationToken);

        var statuses = await _dbContext.EventStatusTypes.OrderBy(x => x.Code).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, statuses.Count);
        Assert.Equal(3, statuses.Select(x => x.Code).Distinct().Count());
    }

    [Fact]
    public async Task HolidayEventSeeder_SeedAsync_WhenAbsolutePathDoesNotExist_DoesNothing()
    {
        // Arrange
        await SeedHolidayLookupDataAsync();
        var seeder = CreateHolidaySeeder(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json"));

        // Act
        await seeder.SeedAsync(_dbContext, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(_dbContext.Events);
    }

    [Fact]
    public async Task HolidayEventSeeder_SeedAsync_WhenRelativeFileHasNoHolidays_DoesNothing()
    {
        // Arrange
        await SeedHolidayLookupDataAsync();
        var relativePath = Path.Combine("seed-tests", $"{Guid.NewGuid():N}.json");
        var absolutePath = Path.Combine(AppContext.BaseDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        await File.WriteAllTextAsync(
            absolutePath,
            """
            {"Source":"test","SourceUrl":"https://example.com","Holidays":[]}
            """,
            TestContext.Current.CancellationToken
        );

        var seeder = CreateHolidaySeeder(relativePath);

        try
        {
            // Act
            await seeder.SeedAsync(_dbContext, TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(_dbContext.Events);
        }
        finally
        {
            File.Delete(absolutePath);
        }
    }

    [Fact]
    public async Task HolidayEventSeeder_SeedAsync_CreatesNewEventsUpdatesExistingEventsAndSkipsInvalidRows()
    {
        // Arrange
        await SeedHolidayLookupDataAsync();
        var startAtUtc = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        _dbContext.Events.Add(
            new Event
            {
                Title = "Existing Holiday",
                StartAtUtc = startAtUtc,
                EndAtUtc = startAtUtc.AddHours(2),
                AllDay = false,
                EventTypeCode = ApiCalendarEventTypeCodes.Holiday,
                StatusTypeCode = ApiCalendarEventStatusTypeCodes.Cancelled,
                SourceModule = ApiCalendarConstants.SourceModule,
                TimeZoneId = "America/Toronto",
            }
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var seedFilePath = Path.Combine(Path.GetTempPath(), $"holidays-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(
            seedFilePath,
            """
            {
                            "Source": "test",
                            "SourceUrl": "https://example.com",
                            "Holidays": [
                                { "Name": "Existing Holiday", "ActualDate": "2026-07-01" },
                                { "Name": "New Holiday", "ActualDate": "2026-08-04" },
                                { "Name": "", "ActualDate": "2026-09-01" },
                                { "Name": "Missing Date", "ActualDate": "" }
              ]
            }
            """,
            TestContext.Current.CancellationToken
        );

        var seeder = CreateHolidaySeeder(seedFilePath);

        try
        {
            // Act
            await seeder.SeedAsync(_dbContext, TestContext.Current.CancellationToken);

            // Assert
            var events = (await _dbContext.Events.ToListAsync(TestContext.Current.CancellationToken))
                .OrderBy(x => x.StartAtUtc)
                .ToList();
            Assert.Equal(2, events.Count);

            var existing = Assert.Single(events, x => x.Title == "Existing Holiday");
            Assert.Equal(startAtUtc.AddDays(1), existing.EndAtUtc);
            Assert.True(existing.AllDay);
            Assert.Null(existing.TimeZoneId);
            Assert.Equal(ApiCalendarEventTypeCodes.Holiday, existing.EventTypeCode);
            Assert.Equal(ApiCalendarEventStatusTypeCodes.Active, existing.StatusTypeCode);
            Assert.Equal(ApiCalendarConstants.SourceModule, existing.SourceModule);

            var created = Assert.Single(events, x => x.Title == "New Holiday");
            Assert.Equal(new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero), created.StartAtUtc);
            Assert.Equal(new DateTimeOffset(2026, 8, 5, 0, 0, 0, TimeSpan.Zero), created.EndAtUtc);
            Assert.True(created.AllDay);
        }
        finally
        {
            File.Delete(seedFilePath);
        }
    }

    [Fact]
    public async Task HolidayEventSeeder_SeedAsync_IsIdempotent()
    {
        await SeedHolidayLookupDataAsync();

        var seedFilePath = Path.Combine(Path.GetTempPath(), $"holidays-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(
            seedFilePath,
            """
            {
              "Source": "test",
              "SourceUrl": "https://example.com",
              "Holidays": [
                { "Name": "Family Day", "ActualDate": "2026-02-16" }
              ]
            }
            """,
            TestContext.Current.CancellationToken
        );

        var seeder = CreateHolidaySeeder(seedFilePath);

        try
        {
            await seeder.SeedAsync(_dbContext, TestContext.Current.CancellationToken);
            await seeder.SeedAsync(_dbContext, TestContext.Current.CancellationToken);

            var events = await _dbContext.Events.ToListAsync(TestContext.Current.CancellationToken);
            Assert.Single(events);
            Assert.Equal("Family Day", events[0].Title);
        }
        finally
        {
            File.Delete(seedFilePath);
        }
    }

    private static HolidayEventSeeder CreateHolidaySeeder(string holidaysFilePath)
    {
        return new HolidayEventSeeder(
            new NullLogger<HolidayEventSeeder>(),
            Options.Create(new CalendarSeedDataOptions { HolidaysFilePath = holidaysFilePath })
        );
    }

    private async Task SeedHolidayLookupDataAsync()
    {
        if (await _dbContext.EventTypes.AnyAsync(TestContext.Current.CancellationToken))
        {
            return;
        }

        _dbContext.EventTypes.AddRange(
            new EventType { Code = ApiCalendarEventTypeCodes.General, Description = "General", EffectiveDate = SeedEffectiveDate },
            new EventType { Code = ApiCalendarEventTypeCodes.Holiday, Description = "Holiday", EffectiveDate = SeedEffectiveDate },
            new EventType { Code = ApiCalendarEventTypeCodes.Deadline, Description = "Deadline", EffectiveDate = SeedEffectiveDate }
        );

        _dbContext.EventStatusTypes.AddRange(
            new EventStatusType { Code = ApiCalendarEventStatusTypeCodes.Draft, Description = "Draft", EffectiveDate = SeedEffectiveDate },
            new EventStatusType { Code = ApiCalendarEventStatusTypeCodes.Active, Description = "Active", EffectiveDate = SeedEffectiveDate },
            new EventStatusType { Code = ApiCalendarEventStatusTypeCodes.Cancelled, Description = "Cancelled", EffectiveDate = SeedEffectiveDate }
        );

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static readonly DateTimeOffset SeedEffectiveDate = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
}