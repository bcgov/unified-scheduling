using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Unified.Common.Scheduling.Models;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.LoanOut;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.UserManagement;
using Unified.LoanOut.Constants;
using Unified.LoanOut.Mappers;
using Unified.LoanOut.Models.Calendar;
using Unified.LoanOut.Services;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.LoanOut.Services;

public sealed class LoanOutCalendarDataProviderTests : IAsyncLifetime
{
    private static readonly DateTimeOffset RangeStart = new(2025, 3, 3, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LookupEffective = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = new("11111111-1111-1111-1111-111111111111");

    private SqliteConnection _connection = null!;
    private UnifiedDbContext _db = null!;
    private LoanOutCalendarDataProvider _provider = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.CreateFunction("now", () => DateTimeOffset.UtcNow.ToString("O"));
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        var options = new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(_connection).Options;
        _db = new SqliteTestUnifiedDbContext(options);
        await _db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        _db.EventTypes.Add(
            new EventType
            {
                Code = LoanOutConstants.LoanOutEventTypeCode,
                Description = LoanOutConstants.LoanOutEventTypeDescription,
                EffectiveDate = LookupEffective,
            }
        );
        _db.EventStatusTypes.AddRange(
            CreateStatus(CalendarEventStatusTypeCodes.Active),
            CreateStatus(CalendarEventStatusTypeCodes.Cancelled)
        );
        _db.Locations.Add(
            new Location
            {
                Id = 5,
                AgencyId = "A5",
                Name = "Location 5",
                Timezone = "America/Vancouver",
            }
        );
        _db.Users.Add(
            new User
            {
                Id = UserId,
                IdirName = "test.user",
                IsEnabled = true,
                FirstName = "Test",
                LastName = "User",
                Email = "test.user@example.com",
                Gender = Gender.Other,
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        _provider = new LoanOutCalendarDataProvider(_db, new LoanOutCalendarMapper(), new TimeZoneService());
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task GetDataAsync_IncludesLoanOutLocationName()
    {
        await AddEntryAsync(RangeStart);
        var result = await _provider.GetDataAsync(
            new SchedulingCalendarEventQueryContext { StartAtUtc = RangeStart, EndAtUtc = RangeStart.AddDays(1) },
            TestContext.Current.CancellationToken
        );
        Assert.Equal("Location 5", Assert.Single(result).LoanOutLocationName);
    }

    [Fact]
    public async Task GetUsersAsync_WhenUserHasMultipleMatchingEntries_ReturnsAllEntryMetadata()
    {
        var series = new EventSeries
        {
            Title = "Recurring LoanOut",
            RecurrenceRule = "FREQ=WEEKLY",
            StartAtUtc = RangeStart.AddDays(2),
            EventTypeCode = LoanOutConstants.LoanOutEventTypeCode,
            StatusTypeCode = CalendarEventStatusTypeCodes.Active,
        };
        _db.EventSeries.Add(series);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var earlierEntry = await AddEntryAsync(RangeStart.AddDays(1));
        var laterEntry = await AddEntryAsync(RangeStart.AddDays(2), series.Id);

        var result = await _provider.GetUsersAsync(
            new LoanOutUserSearchRequest
            {
                StartDate = new DateOnly(2025, 3, 3),
                EndDate = new DateOnly(2025, 3, 10),
                TimeZoneId = "America/Vancouver",
                LocationId = 5,
            },
            TestContext.Current.CancellationToken
        );

        var user = Assert.Single(result);
        Assert.Equal(UserId, user.Id);
        Assert.Collection(
            user.LoanOutMetaData.OrderBy(metadata => metadata.StartAtUtc),
            first =>
            {
                Assert.Equal(5, first.LoanOutLocationId);
                Assert.Equal(RangeStart.AddDays(1), first.StartAtUtc);
                Assert.Null(first.RecurrenceRule);
            },
            second =>
            {
                Assert.Equal(5, second.LoanOutLocationId);
                Assert.Equal(RangeStart.AddDays(2), second.StartAtUtc);
                Assert.Equal("FREQ=WEEKLY", second.RecurrenceRule);
            }
        );
        Assert.NotEqual(earlierEntry.Id, laterEntry.Id);
    }

    [Fact]
    public async Task GetUsersAsync_WhenEntryIsOnInclusiveEndDate_UsesLocationTimeZone()
    {
        await AddEntryAsync(new DateTimeOffset(2025, 3, 10, 6, 30, 0, TimeSpan.Zero));
        await AddEntryAsync(new DateTimeOffset(2025, 3, 10, 7, 0, 0, TimeSpan.Zero));

        var result = await _provider.GetUsersAsync(
            new LoanOutUserSearchRequest
            {
                StartDate = new DateOnly(2025, 3, 9),
                EndDate = new DateOnly(2025, 3, 9),
                TimeZoneId = "America/Vancouver",
                LocationId = 5,
            },
            TestContext.Current.CancellationToken
        );

        var user = Assert.Single(result);
        Assert.Equal(UserId, user.Id);
        Assert.Equal(
            new DateTimeOffset(2025, 3, 10, 6, 30, 0, TimeSpan.Zero),
            Assert.Single(user.LoanOutMetaData).StartAtUtc
        );
    }

    private async Task<LoanOutEntry> AddEntryAsync(DateTimeOffset startAtUtc, int? eventSeriesId = null)
    {
        var eventEntity = new Event
        {
            EventSeriesId = eventSeriesId,
            Title = "LoanOut",
            StartAtUtc = startAtUtc,
            EndAtUtc = startAtUtc.AddHours(2),
            SourceModule = LoanOutConstants.SourceModule,
            EventTypeCode = LoanOutConstants.LoanOutEventTypeCode,
            StatusTypeCode = CalendarEventStatusTypeCodes.Active,
        };
        _db.Events.Add(eventEntity);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var entry = new LoanOutEntry
        {
            EventId = eventEntity.Id,
            LoanOutLocationId = 5,
            Users = [new LoanOutEntryUser { UserId = UserId }],
        };
        _db.LoanOutEntries.Add(entry);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entry;
    }

    private static EventStatusType CreateStatus(string code) =>
        new()
        {
            Code = code,
            Description = code,
            EffectiveDate = LookupEffective,
        };
}
