using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Calendar;
using Unified.Calendar.Seeders;
using Unified.Db;
using Unified.Tests.TestHelpers;

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

        var options = new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(_connection).Options;

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
                Code = CalendarCodeMappings.ToDbCode(CalendarEventTypeCode.General),
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
        var eventTypes = await _dbContext
            .EventTypes.OrderBy(x => x.Code)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            [
                CalendarCodeMappings.ToDbCode(CalendarEventTypeCode.AwayLocation),
                CalendarCodeMappings.ToDbCode(CalendarEventTypeCode.Deadline),
                CalendarCodeMappings.ToDbCode(CalendarEventTypeCode.General),
                CalendarCodeMappings.ToDbCode(CalendarEventTypeCode.Holiday),
            ],
            eventTypes.Select(x => x.Code)
        );

        var general = Assert.Single(
            eventTypes,
            x => x.Code == CalendarCodeMappings.ToDbCode(CalendarEventTypeCode.General)
        );
        Assert.Equal("General", general.Description);
        Assert.Equal(new DateTimeOffset(2020, 6, 10, 0, 0, 0, TimeSpan.Zero), general.EffectiveDate);
        Assert.Null(general.ExpiryDate);
    }

    [Fact]
    public async Task EventTypeSeeder_SeedAsync_IsIdempotent()
    {
        var seeder = new EventTypeSeeder(new NullLogger<EventTypeSeeder>());

        await seeder.SeedAsync(_dbContext, TestContext.Current.CancellationToken);
        await seeder.SeedAsync(_dbContext, TestContext.Current.CancellationToken);

        var eventTypes = await _dbContext
            .EventTypes.OrderBy(x => x.Code)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(4, eventTypes.Count);
        Assert.Equal(4, eventTypes.Select(x => x.Code).Distinct().Count());
    }

    [Fact]
    public async Task EventStatusTypeSeeder_SeedAsync_AddsMissingRowsAndUpdatesExistingRows()
    {
        // Arrange
        _dbContext.EventStatusTypes.Add(
            new()
            {
                Code = CalendarCodeMappings.ToDbCode(CalendarEventStatusTypeCode.Draft),
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
        var statuses = await _dbContext
            .EventStatusTypes.OrderBy(x => x.Code)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            [
                CalendarCodeMappings.ToDbCode(CalendarEventStatusTypeCode.Active),
                CalendarCodeMappings.ToDbCode(CalendarEventStatusTypeCode.Cancelled),
                CalendarCodeMappings.ToDbCode(CalendarEventStatusTypeCode.Draft),
            ],
            statuses.Select(x => x.Code)
        );

        var draft = Assert.Single(
            statuses,
            x => x.Code == CalendarCodeMappings.ToDbCode(CalendarEventStatusTypeCode.Draft)
        );
        Assert.Equal("Draft", draft.Description);
        Assert.Equal(new DateTimeOffset(2020, 6, 10, 0, 0, 0, TimeSpan.Zero), draft.EffectiveDate);
        Assert.Null(draft.ExpiryDate);
    }

    [Fact]
    public async Task EventStatusTypeSeeder_SeedAsync_IsIdempotent()
    {
        var seeder = new EventStatusTypeSeeder(new NullLogger<EventStatusTypeSeeder>());

        await seeder.SeedAsync(_dbContext, TestContext.Current.CancellationToken);
        await seeder.SeedAsync(_dbContext, TestContext.Current.CancellationToken);

        var statuses = await _dbContext
            .EventStatusTypes.OrderBy(x => x.Code)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, statuses.Count);
        Assert.Equal(3, statuses.Select(x => x.Code).Distinct().Count());
    }
}
