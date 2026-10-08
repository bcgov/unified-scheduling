using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.UserManagement;
using Unified.Tests.TestHelpers;
using Unified.TimeOff.Constants;

namespace Unified.Tests.TimeOff;

/// <summary>
/// SQLite in-memory database seeded with the lookup data time-off tests rely on.
/// </summary>
internal sealed class TimeOffTestDatabase : IAsyncDisposable
{
    public static readonly Guid UserA = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid UserB = new("22222222-2222-2222-2222-222222222222");

    private static readonly DateTimeOffset LookupEffective = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;

    private TimeOffTestDatabase(SqliteConnection connection, UnifiedDbContext db)
    {
        _connection = connection;
        Db = db;
    }

    public UnifiedDbContext Db { get; }

    public static async Task<TimeOffTestDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.CreateFunction("now", () => DateTimeOffset.UtcNow.ToString("O"));
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var options = new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(connection).Options;
        var db = new SqliteTestUnifiedDbContext(options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        db.EventTypes.AddRange(
            new EventType
            {
                Code = TimeOffConstants.TimeOffEventTypeCode,
                Description = "Time Off",
                EffectiveDate = LookupEffective,
            },
            new EventType
            {
                Code = CalendarEventTypeCodes.General,
                Description = "General",
                EffectiveDate = LookupEffective,
            }
        );
        db.EventStatusTypes.AddRange(
            CreateStatus(CalendarEventStatusTypeCodes.Draft),
            CreateStatus(CalendarEventStatusTypeCodes.Active),
            CreateStatus(CalendarEventStatusTypeCodes.Cancelled)
        );
        db.Locations.AddRange(
            new Location
            {
                Id = 5,
                AgencyId = "A5",
                Name = "Location 5",
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
        db.Users.AddRange(CreateUser(UserA, "UserA"), CreateUser(UserB, "UserB"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new TimeOffTestDatabase(connection, db);
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static EventStatusType CreateStatus(string code) =>
        new()
        {
            Code = code,
            Description = code,
            EffectiveDate = LookupEffective,
        };

    private static User CreateUser(Guid id, string name) =>
        new()
        {
            Id = id,
            IdirName = name,
            IsEnabled = true,
            FirstName = name,
            LastName = "Test",
            Email = $"{name}@example.com",
            Gender = Gender.Other,
        };
}
