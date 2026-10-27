using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Calendar.Mappings;
using Unified.Calendar.Services;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.LoanOut;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.UserManagement;
using Unified.LoanOut.Constants;
using Unified.LoanOut.Factories;
using Unified.LoanOut.Mappers;
using Unified.LoanOut.Models;
using Unified.LoanOut.Services;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.LoanOut.Services;

public sealed class LoanOutEntryServiceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset EffectiveDate = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Start = new(2026, 6, 1, 16, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserA = new("11111111-1111-1111-1111-111111111111");
    private SqliteConnection _connection = null!;
    private UnifiedDbContext _db = null!;
    private LoanOutEntryService _service = null!;

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
                EffectiveDate = EffectiveDate,
            }
        );
        _db.EventStatusTypes.Add(
            new EventStatusType
            {
                Code = CalendarEventStatusTypeCodes.Draft,
                Description = CalendarEventStatusTypeCodes.Draft,
                EffectiveDate = EffectiveDate,
            }
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
        _db.Locations.Add(
            new Location
            {
                Id = 10,
                AgencyId = "A10",
                Name = "Loan out location",
                Timezone = "America/Vancouver",
            }
        );
        _db.Users.Add(
            new User
            {
                Id = UserA,
                IdirName = "test.user",
                IsEnabled = true,
                FirstName = "Test",
                LastName = "User",
                Email = "test@example.com",
                Gender = Gender.Other,
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var eventMapper = new CalendarEventMapper();
        var calendarEntryService = new CalendarEventEntryService<LoanOutEntryRequest, LoanOutEntry>(
            NullLogger<CalendarEventEntryService<LoanOutEntryRequest, LoanOutEntry>>.Instance,
            new LoanOutEventFactory(),
            new CalendarLifecycleService(),
            eventMapper
        );
        _service = new LoanOutEntryService(
            NullLogger<LoanOutEntryService>.Instance,
            _db,
            new LoanOutEventMapper(eventMapper),
            calendarEntryService
        );
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task CreateLoanOutEntryAsync_PersistsEventEntryAndUser()
    {
        var response = await _service.CreateLoanOutEntryAsync(CreateRequest(), TestContext.Current.CancellationToken);

        var entry = await _db.LoanOutEntries.Include(item => item.Event).Include(item => item.Users).SingleAsync();
        Assert.Equal(entry.Id, response.Id);
        Assert.Equal(10, response.LoanOutLocationId);
        Assert.Equal(Start, entry.Event!.StartAtUtc);
        Assert.Equal(LoanOutConstants.SourceModule, entry.Event.SourceModule);
        Assert.Equal(LoanOutConstants.LoanOutEventTypeCode, entry.Event.EventTypeCode);
        Assert.Equal(UserA, Assert.Single(entry.Users).UserId);
    }

    [Fact]
    public async Task GetLoanOutEntryByIdAsync_WhenMissing_ReturnsNull()
    {
        var response = await _service.GetLoanOutEntryByIdAsync(123, TestContext.Current.CancellationToken);

        Assert.Null(response);
    }

    [Fact]
    public async Task UpdateLoanOutEntryAsync_WhenMissing_ReturnsNull()
    {
        var response = await _service.UpdateLoanOutEntryAsync(
            123,
            CreateRequest(),
            TestContext.Current.CancellationToken
        );

        Assert.Null(response);
    }

    [Fact]
    public async Task DeleteLoanOutEntryAsync_WhenMissing_ReturnsFalse()
    {
        var deleted = await _service.DeleteLoanOutEntryAsync(123, TestContext.Current.CancellationToken);

        Assert.False(deleted);
    }

    private static LoanOutEntryRequest CreateRequest() =>
        new()
        {
            Title = "Loan out",
            StartAtUtc = Start,
            EndAtUtc = Start.AddHours(4),
            TimeZoneId = "America/Vancouver",
            LocationId = 5,
            LoanOutLocationId = 10,
            UserIds = [UserA],
        };
}
