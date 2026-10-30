using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Calendar.Mappings;
using Unified.Calendar.Services;
using Unified.Common.Validation;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.LoanOut;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.UserManagement;
using Unified.Infrastructure.ErrorHandling;
using Unified.LoanOut.Constants;
using Unified.LoanOut.Factories;
using Unified.LoanOut.Mappers;
using Unified.LoanOut.Models;
using Unified.LoanOut.Services;
using Unified.Scheduling.Services;
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
    private readonly FakeSchedulingLoanOutService _schedulingLoanOutService = new();

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
            calendarEntryService,
            _schedulingLoanOutService
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
        Assert.True(entry.Event.AllDay);
        Assert.True(response.AllDay);
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
    public async Task UpdateLoanOutEntryAsync_AlwaysPersistsEventAsAllDay()
    {
        var created = await _service.CreateLoanOutEntryAsync(CreateRequest(), TestContext.Current.CancellationToken);
        var entry = await _db.LoanOutEntries.Include(item => item.Event).SingleAsync();
        entry.Event!.AllDay = false;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await _service.UpdateLoanOutEntryAsync(
            created.Id,
            CreateRequest(),
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(response);
        Assert.True(response.AllDay);
        Assert.True(entry.Event.AllDay);
    }

    [Fact]
    public async Task DeleteLoanOutEntryAsync_WhenMissing_ReturnsFalse()
    {
        var deleted = await _service.DeleteLoanOutEntryAsync(123, TestContext.Current.CancellationToken);

        Assert.False(deleted);
    }

    [Fact]
    public async Task UpdateAndDeleteLoanOutEntryAsync_WhenShiftExistsAtLoanOutLocation_ThrowsConflict()
    {
        var created = await _service.CreateLoanOutEntryAsync(CreateRequest(), TestContext.Current.CancellationToken);
        _schedulingLoanOutService.HasShiftConflict = true;

        await Assert.ThrowsAsync<ConflictValidationException>(() =>
            _service.UpdateLoanOutEntryAsync(
                created.Id,
                CreateRequest() with
                {
                    LoanOutLocationId = 5,
                    Title = "Blocked update",
                },
                TestContext.Current.CancellationToken
            )
        );
        Assert.Equal(10, _schedulingLoanOutService.LastCheckedLocationId);
        _db.ChangeTracker.Clear();
        var retained = await _db
            .LoanOutEntries.Include(entry => entry.Event)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(10, retained.LoanOutLocationId);
        Assert.Equal("Loan out", retained.Event!.Title);
        await Assert.ThrowsAsync<ConflictValidationException>(() =>
            _service.DeleteLoanOutEntryAsync(created.Id, TestContext.Current.CancellationToken)
        );
    }

    [Theory]
    [InlineData(false, true, 999)]
    [InlineData(true, true, 999)]
    [InlineData(false, false, 999)]
    [InlineData(true, false, 999)]
    [InlineData(false, true, 0)]
    [InlineData(true, true, -1)]
    [InlineData(false, false, 0)]
    [InlineData(true, false, -1)]
    public async Task LocationGuard_InvalidLocation_RejectsWithoutSaving(bool update, bool source, int locationId)
    {
        var ct = TestContext.Current.CancellationToken;
        var original = update ? await _service.CreateLoanOutEntryAsync(CreateRequest(), ct) : null;
        var request = source
            ? CreateRequest() with
            {
                LocationId = locationId,
                Title = "Rejected",
            }
            : CreateRequest() with
            {
                LoanOutLocationId = locationId,
                Title = "Rejected",
            };
        var before = await _db.LoanOutEntries.CountAsync(ct);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            if (update)
                await _service.UpdateLoanOutEntryAsync(original!.Id, request, ct);
            else
                await _service.CreateLoanOutEntryAsync(request, ct);
        });
        Assert.Contains(
            source ? "home location" : "loan-out location",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
        _db.ChangeTracker.Clear();
        Assert.Equal(before, await _db.LoanOutEntries.CountAsync(ct));
        Assert.False(await _db.Events.AnyAsync(item => item.Title == "Rejected", ct));
    }

    [Fact]
    public async Task LocationGuard_OptionalSource_AllowsMissingSource()
    {
        var result = await _service.CreateLoanOutEntryAsync(
            CreateRequest() with
            {
                LocationId = null,
            },
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData(false, "missing")]
    [InlineData(true, "missing")]
    [InlineData(false, "mixed")]
    [InlineData(true, "mixed")]
    [InlineData(false, "empty")]
    [InlineData(true, "empty")]
    [InlineData(false, "empty-id")]
    [InlineData(true, "empty-id")]
    [InlineData(false, "duplicate")]
    [InlineData(true, "duplicate")]
    public async Task UserGuard_InvalidUsers_RejectsWithoutSaving(bool update, string scenario)
    {
        var ct = TestContext.Current.CancellationToken;
        var original = update ? await _service.CreateLoanOutEntryAsync(CreateRequest(), ct) : null;
        var missing = new Guid("99999999-9999-9999-9999-999999999999");
        Guid[] users = scenario switch
        {
            "empty" => [],
            "empty-id" => [Guid.Empty],
            "duplicate" => [UserA, UserA],
            "mixed" => [UserA, missing],
            _ => [missing],
        };
        var request = CreateRequest() with { UserIds = users, Title = "Rejected users" };
        var before = await _db.LoanOutEntries.CountAsync(ct);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            if (update)
                await _service.UpdateLoanOutEntryAsync(original!.Id, request, ct);
            else
                await _service.CreateLoanOutEntryAsync(request, ct);
        });
        if (scenario is "missing" or "mixed")
            Assert.Equal("One or more selected users were not found.", exception.Message);
        _db.ChangeTracker.Clear();
        Assert.Equal(before, await _db.LoanOutEntries.CountAsync(ct));
        Assert.False(await _db.Events.AnyAsync(item => item.Title == "Rejected users", ct));
        if (update)
        {
            var saved = await _db.LoanOutEntries.Include(entry => entry.Users).SingleAsync(ct);
            Assert.Equal(UserA, Assert.Single(saved.Users).UserId);
        }
    }

    [Theory]
    [InlineData("user", "One or more selected users were not found.")]
    [InlineData("destination", "Loan-out location 999 was not found.")]
    [InlineData("source", "A valid home location is required.")]
    public async Task LoanOutSave_GuardFailure_ReturnsReadableDetail(string scenario, string message)
    {
        var ct = TestContext.Current.CancellationToken;
        var request = CreateRequest() with
        {
            LocationId = scenario == "source" ? 0 : null,
            LoanOutLocationId = scenario == "destination" ? 999 : 10,
            UserIds = scenario == "user" ? [new Guid("99999999-9999-9999-9999-999999999999")] : [UserA],
        };
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateLoanOutEntryAsync(request, ct)
        );
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddLogging();
        services.AddProblemDetails();
        await using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider, Response = { Body = new MemoryStream() } };
        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance,
            provider.GetRequiredService<IProblemDetailsService>()
        );

        Assert.True(await handler.TryHandleAsync(context, exception, ct));

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var payload = await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: ct);
        Assert.Equal(message, payload.RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain("INVALID", payload.RootElement.GetRawText());
        Assert.False(await _db.LoanOutEntries.AnyAsync(ct));
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

    private sealed class FakeSchedulingLoanOutService : ISchedulingLoanOutService
    {
        public Task RestoreHomeLocationUserShiftsAsync(
            IReadOnlyCollection<LoanOutEntry> removedEntries,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public bool HasShiftConflict { get; set; }
        public int? LastCheckedLocationId { get; private set; }

        public Task<IReadOnlyList<IReadOnlyCollection<Guid>>> GetAssignableUserIdsByOccurrenceAsync(
            IReadOnlyCollection<Guid> userIds,
            IReadOnlyCollection<ShiftAssignmentLoanOutContext> assignmentOccurrences,
            int? locationId,
            string? timeZoneId,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyList<IReadOnlyCollection<Guid>>>([]);

        public Task<bool> HasShiftAtLoanOutLocationAsync(
            IEnumerable<LoanOutEntry> loanOutEntries,
            int loanOutLocationId,
            CancellationToken cancellationToken = default
        )
        {
            LastCheckedLocationId = loanOutLocationId;
            return Task.FromResult(HasShiftConflict);
        }

        public Task<bool> TransferLoanedOutUserShift(
            LoanOutEntry entry,
            bool deleteAssignments = false,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(false);

        public Task<bool> TransferLoanedOutUserShift(
            LoanOutSeries series,
            bool deleteAssignments = false,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(false);
    }
}
