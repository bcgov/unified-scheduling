using FluentValidation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unified.Calendar;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Leave;
using Unified.Db.Models.LoanOut;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.Scheduling;
using Unified.Db.Models.Stats;
using Unified.Db.Models.UserManagement;
using Unified.Leave.Constants;
using Unified.Leave.FeatureFlags;
using Unified.LoanOut.Constants;
using Unified.Scheduling;
using Unified.Scheduling.Models;
using Unified.Scheduling.Options;
using Unified.Scheduling.Services;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.Scheduling.Services;

public sealed class WorkingHoursServiceTests : IAsyncLifetime
{
    private static readonly Guid UserA = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserB = new("22222222-2222-2222-2222-222222222222");
    private static readonly DateOnly BusinessDate = new(2026, 6, 1);

    private SqliteConnection connection = null!;
    private UnifiedDbContext db = null!;
    private WorkingHoursService service = null!;
    private readonly LeaveFeatureFlags leaveFlags = new() { Enabled = true };
    private ShiftLoanOutAvailabilityService availability = null!;

    public async ValueTask InitializeAsync()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var dbOptions = new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(connection).Options;
        db = new SqliteTestUnifiedDbContext(dbOptions);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await SeedBaseDataAsync();

        var resolver = new CalendarTimeZoneResolver(
            Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "America/Vancouver" }),
            new TimeZoneService()
        );
        availability = SchedulingLoanOutTestServices.CreateAvailability(db, new TimeZoneService(), resolver);
        service = new WorkingHoursService(
            NullLogger<WorkingHoursService>.Instance,
            db,
            Options.Create(new WorkingHoursOptions { DefaultLunchMinutes = 60, MaxQueryRangeDays = 31 }),
            new TimeZoneService(),
            Options.Create(leaveFlags),
            resolver,
            availability
        );
    }

    public async ValueTask DisposeAsync()
    {
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(5, false)]
    [InlineData(9, false)]
    [InlineData(null, true)]
    [InlineData(5, true)]
    [InlineData(9, true)]
    public async Task QueryAsync_PartialRecurringLoanOut_FiltersOnlyAwayShiftUserPairs(int? locationId, bool onlyUserA)
    {
        var originals = new List<ShiftEntry>();
        var destinations = new List<ShiftEntry>();
        var loanOutSeries = new LoanOutSeries
        {
            LoanOutLocationId = 9,
            EventSeries = new EventSeries
            {
                Title = "Recurring loan-out",
                StartAtUtc = At(7),
                EndAtUtc = AtDay(2, 7),
                TimeZoneId = "America/Vancouver",
                EventTypeCode = LoanOutConstants.LoanOutEventTypeCode,
                StatusTypeCode = CalendarEventStatusTypeCodes.Active,
                LocationId = 5,
                RecurrenceRule = "FREQ=DAILY;INTERVAL=2;COUNT=2",
            },
        };
        for (var day = 1; day <= 3; day++)
        {
            var shift = await AddShiftAsync(AtDay(day, 16), AtDay(day + 1, 0), userIds: [UserA, UserB]);
            originals.Add(shift);
            if (day == 2)
                continue;
            // A shift-wide marker must not suppress the employee who remains at home.
            shift.Event!.IsException = true;
            shift.Event.ExceptionReason = nameof(CalendarEventExceptionReasonCode.IsAway);
            loanOutSeries.LoanOutEntries.Add(
                new LoanOutEntry
                {
                    LoanOutLocationId = 9,
                    Event = new Event
                    {
                        Title = "Loan-out",
                        StartAtUtc = AtDay(day, 7),
                        EndAtUtc = AtDay(day + 1, 7),
                        TimeZoneId = "America/Vancouver",
                        SourceModule = LoanOutConstants.SourceModule,
                        EventTypeCode = LoanOutConstants.LoanOutEventTypeCode,
                        StatusTypeCode = CalendarEventStatusTypeCodes.Active,
                        LocationId = 5,
                        EventSeries = loanOutSeries.EventSeries,
                    },
                    Users = [new LoanOutEntryUser { UserId = UserA }],
                }
            );
            destinations.Add(await AddShiftAsync(AtDay(day, 16), AtDay(day + 1, 0), locationId: 9));
        }
        db.LoanOutSeries.Add(loanOutSeries);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var results = await QueryAsync(
            new WorkingHoursQuery
            {
                StartDate = BusinessDate,
                EndDate = BusinessDate.AddDays(2),
                ShiftLocationIds = locationId.HasValue ? [locationId.Value] : [5, 9],
                UserIds = onlyUserA ? [UserA] : null,
            }
        );

        var expected = new List<(int, Guid)>();
        if (locationId != 9)
        {
            expected.Add((originals[1].Id, UserA));
            if (!onlyUserA)
                expected.AddRange(originals.Select(shift => (shift.Id, UserB)));
        }
        if (locationId != 5)
            expected.AddRange(destinations.Select(shift => (shift.Id, UserA)));
        Assert.Equal(expected.Order(), results.Select(result => (result.ShiftEntryId, result.UserId)).Order());
        Assert.All(results.GroupBy(result => (result.Date, result.UserId)), group => Assert.Single(group));
    }

    [Fact]
    public async Task QueryAsync_LocationWithoutTimeZone_UsesCalendarDefaultForLoanOutEligibility()
    {
        var token = TestContext.Current.CancellationToken;
        var location = await db.Locations.FindAsync([5], token);
        location!.Timezone = "";
        var shift = await AddShiftAsync(AtDay(2, 6), AtDay(2, 14), timeZoneId: null);
        var loanOut = new LoanOutEntry
        {
            LoanOutLocationId = 9,
            Event = new Event
            {
                Title = "Next-day loan-out",
                StartAtUtc = AtDay(2, 7),
                EndAtUtc = AtDay(3, 7),
                EventTypeCode = LoanOutConstants.LoanOutEventTypeCode,
                StatusTypeCode = CalendarEventStatusTypeCodes.Active,
                LocationId = 5,
            },
            Users = [new LoanOutEntryUser { UserId = UserA }],
        };
        db.LoanOutEntries.Add(loanOut);
        await db.SaveChangesAsync(token);

        // The shift starts on June 1 in the configured default zone; the June 2 loan-out does not apply.
        Assert.Equal(shift.Id, Assert.Single(await QueryAsync()).ShiftEntryId);

        loanOut.Event.StartAtUtc = At(7);
        loanOut.Event.EndAtUtc = AtDay(2, 7);
        await db.SaveChangesAsync(token);
        Assert.Empty(await QueryAsync());
    }

    [Fact]
    public async Task QueryAsync_LoanOutDisabled_PreservesOriginalShiftResults()
    {
        var shift = await AddShiftAsync(At(9), At(17));
        db.LoanOutEntries.Add(
            new LoanOutEntry
            {
                LoanOutLocationId = 9,
                Event = new Event
                {
                    Title = "Loan-out",
                    StartAtUtc = At(7),
                    EndAtUtc = AtDay(2, 7),
                    EventTypeCode = LoanOutConstants.LoanOutEventTypeCode,
                    StatusTypeCode = CalendarEventStatusTypeCodes.Active,
                    LocationId = 5,
                },
                Users = [new LoanOutEntryUser { UserId = UserA }],
            }
        );
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var disabledService = new WorkingHoursService(
            NullLogger<WorkingHoursService>.Instance,
            db,
            Options.Create(new WorkingHoursOptions()),
            new TimeZoneService(),
            Options.Create(leaveFlags),
            new CalendarTimeZoneResolver(
                Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "America/Vancouver" }),
                new TimeZoneService()
            ),
            SchedulingLoanOutTestServices.CreateAvailability(db, new TimeZoneService(), loanOutEnabled: false)
        );

        var result = Assert.Single(
            await disabledService.QueryAsync(
                new WorkingHoursQuery { StartDate = BusinessDate, EndDate = BusinessDate },
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(shift.Id, result.ShiftEntryId);
        Assert.Equal(420, result.CreditedMinutes);
        Assert.Equal(0, result.OvertimeMinutes);
        Assert.Empty(await QueryAsync());
    }

    [Fact]
    public async Task AddShiftLunchMinutes_ExistingRowsRemainUnknownAndExplicitZeroIsPreserved()
    {
        await using var legacyConnection = new SqliteConnection("Data Source=:memory:");
        await legacyConnection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(legacyConnection).Options;
        await using var legacyDb = new SqliteTestUnifiedDbContext(options);
        await legacyDb.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "ShiftEntries" ("Id" INTEGER PRIMARY KEY);
            CREATE TABLE "ShiftSeries" ("Id" INTEGER PRIMARY KEY);
            INSERT INTO "ShiftEntries" ("Id") VALUES (1);
            INSERT INTO "ShiftSeries" ("Id") VALUES (1);
            """,
            TestContext.Current.CancellationToken
        );

        var migration = new global::Unified.Db.Migrations.AddShiftLunchMinutes();
        var commands = legacyDb.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations);
        foreach (var command in commands)
            await legacyDb.Database.ExecuteSqlRawAsync(command.CommandText, TestContext.Current.CancellationToken);

        foreach (var table in new[] { "ShiftEntries", "ShiftSeries" })
        {
            using var command = legacyConnection.CreateCommand();
            command.CommandText = $"""
                INSERT INTO "{table}" ("Id", "LunchAvailableMinutes", "WorkedLunchMinutes") VALUES (2, 0, 0);
                SELECT "LunchAvailableMinutes", "WorkedLunchMinutes" FROM "{table}" ORDER BY "Id";
                """;
            await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
            Assert.True(reader.IsDBNull(0));
            Assert.True(reader.IsDBNull(1));
            Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0, reader.GetInt32(0));
            Assert.Equal(0, reader.GetInt32(1));
        }
    }

    [Fact]
    public async Task QueryAsync_NormalShiftWithoutOvertime_ReturnsPaidShiftMinutesOnly()
    {
        // Arrange
        await AddShiftAsync(At(9), At(17));

        // Act
        var result = Assert.Single(await QueryAsync());

        // Assert
        Assert.Equal(420, result.PaidShiftMinutes);
        Assert.Equal(0, result.PaidOutsideShiftMinutes);
        Assert.Equal(420, result.WorkedMinutes);
        Assert.Equal(0, result.PaidLeaveMinutes);
        Assert.Equal(0, result.UnpaidLeaveMinutes);
        Assert.Equal(420, result.CreditedMinutes);
        Assert.Equal(0, result.OvertimeMinutes);
    }

    [Fact]
    public async Task QueryAsync_PreShiftAssignment_CountsOnlyPreShiftMinutes()
    {
        // Arrange
        var shift = await AddShiftAsync(At(9), At(17));
        await AddAssignmentAsync(shift, At(8), At(10));

        // Act
        var result = Assert.Single(await QueryAsync());

        // Assert
        Assert.Equal(60, result.PaidOutsideShiftMinutes);
        Assert.Equal(480, result.CreditedMinutes);
        Assert.Equal(60, result.OvertimeMinutes);
    }

    [Fact]
    public async Task QueryAsync_PostShiftAssignment_CountsOnlyPostShiftMinutes()
    {
        // Arrange
        var shift = await AddShiftAsync(At(9), At(17));
        await AddAssignmentAsync(shift, At(16), At(18));

        // Act
        var result = Assert.Single(await QueryAsync());

        // Assert
        Assert.Equal(60, result.PaidOutsideShiftMinutes);
        Assert.Equal(480, result.CreditedMinutes);
        Assert.Equal(60, result.OvertimeMinutes);
    }

    [Fact]
    public async Task QueryAsync_AssignmentSpansBothSidesOfShift_CountsBothOutsidePortions()
    {
        // Arrange
        var shift = await AddShiftAsync(At(9), At(17));
        await AddAssignmentAsync(shift, At(8), At(18));

        // Act
        var result = Assert.Single(await QueryAsync());

        // Assert
        Assert.Equal(120, result.PaidOutsideShiftMinutes);
        Assert.Equal(540, result.CreditedMinutes);
        Assert.Equal(120, result.OvertimeMinutes);
    }

    [Fact]
    public async Task QueryAsync_OverlappingOutsideShiftAssignments_DoesNotDoubleCountOverlap()
    {
        // Arrange
        var shift = await AddShiftAsync(At(9), At(17));
        await AddAssignmentAsync(shift, At(7), At(8, 30));
        await AddAssignmentAsync(shift, At(8), At(10));

        // Act
        var result = Assert.Single(await QueryAsync());

        // Assert
        Assert.Equal(120, result.PaidOutsideShiftMinutes);
        Assert.Equal(540, result.WorkedMinutes);
        Assert.Equal(0, result.PaidLeaveMinutes);
        Assert.Equal(0, result.UnpaidLeaveMinutes);
        Assert.Equal(540, result.CreditedMinutes);
        Assert.Equal(120, result.OvertimeMinutes);
    }

    [Fact]
    public async Task QueryAsync_NonOverlappingOutsideShiftAssignments_CountsBothAssignments()
    {
        // Arrange
        var shift = await AddShiftAsync(At(9), At(17));
        await AddAssignmentAsync(shift, At(8), At(9));
        await AddAssignmentAsync(shift, At(17), At(18));

        // Act
        var result = Assert.Single(await QueryAsync());

        // Assert
        Assert.Equal(120, result.PaidOutsideShiftMinutes);
        Assert.Equal(540, result.CreditedMinutes);
    }

    [Fact]
    public async Task QueryAsync_AssignmentForOneUserOnSharedShift_DoesNotAffectOtherUser()
    {
        // Arrange
        var shift = await AddShiftAsync(At(9), At(17), userIds: [UserA, UserB]);
        await AddAssignmentAsync(shift, At(8), At(9), [UserA]);

        // Act
        var results = await QueryAsync();

        // Assert
        Assert.Equal(60, Assert.Single(results, result => result.UserId == UserA).PaidOutsideShiftMinutes);
        Assert.Equal(0, Assert.Single(results, result => result.UserId == UserB).PaidOutsideShiftMinutes);
    }

    [Fact]
    public async Task QueryAsync_MultiUserShift_ReturnsOneResultPerUser()
    {
        // Arrange
        await AddShiftAsync(At(9), At(17), userIds: [UserA, UserB]);

        // Act
        var results = await QueryAsync();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal([UserA, UserB], results.Select(result => result.UserId).Order().ToArray());
    }

    [Fact]
    public async Task QueryAsync_FullyWorkedLunch_ContributesTowardOvertimeThreshold()
    {
        // Arrange
        await AddShiftAsync(At(9), At(17), lunchAvailableMinutes: 60, workedLunchMinutes: 60);

        // Act
        var result = Assert.Single(await QueryAsync());

        // Assert
        Assert.Equal(480, result.PaidShiftMinutes);
        Assert.Equal(60, result.WorkedLunchMinutes);
        Assert.Equal(480, result.WorkedMinutes);
        Assert.Equal(0, result.PaidLeaveMinutes);
        Assert.Equal(0, result.UnpaidLeaveMinutes);
        Assert.Equal(480, result.CreditedMinutes);
        Assert.Equal(60, result.OvertimeMinutes);
    }

    [Fact]
    public async Task QueryAsync_PartiallyWorkedLunch_CountsOnlyWorkedLunchMinutes()
    {
        // Arrange
        await AddShiftAsync(At(9), At(17), lunchAvailableMinutes: 60, workedLunchMinutes: 30);

        // Act
        var result = Assert.Single(await QueryAsync());

        // Assert
        Assert.Equal(450, result.PaidShiftMinutes);
        Assert.Equal(60, result.LunchAvailableMinutes);
        Assert.Equal(30, result.WorkedLunchMinutes);
        Assert.Equal(30, result.OvertimeMinutes);
    }

    [Fact]
    public async Task QueryAsync_OvernightShift_UsesShiftStartBusinessDate()
    {
        // Arrange
        await AddShiftAsync(At(23), AtDay(2, 7));

        // Act
        var result = Assert.Single(await QueryAsync());

        // Assert
        Assert.Equal(BusinessDate, result.Date);
        Assert.Equal(420, result.PaidShiftMinutes);
    }

    [Fact]
    public async Task QueryAsync_UtcStartAfterMidnightButLocalStartBeforeMidnight_UsesLocalBusinessDate()
    {
        // Arrange
        await AddShiftAsync(AtDay(2, 6, 30), AtDay(2, 14, 30), timeZoneId: "America/Vancouver");

        // Act
        var result = Assert.Single(await QueryAsync());

        // Assert
        Assert.Equal(BusinessDate, result.Date);
    }

    [Fact]
    public async Task QueryAsync_LocationAndUserFilters_ReturnsTheirIntersection()
    {
        // Arrange
        await AddShiftAsync(At(9), At(17), locationId: 5, userIds: [UserA]);
        await AddShiftAsync(At(10), At(18), locationId: 5, userIds: [UserB]);
        await AddShiftAsync(At(11), At(19), locationId: 9, userIds: [UserB]);

        // Act
        var result = Assert.Single(
            await QueryAsync(
                new WorkingHoursQuery
                {
                    StartDate = BusinessDate,
                    EndDate = BusinessDate,
                    ShiftLocationIds = [5],
                    UserIds = [UserB],
                }
            )
        );

        // Assert
        Assert.Equal(UserB, result.UserId);
        Assert.Equal(420, result.PaidShiftMinutes);
    }

    [Fact]
    public async Task QueryAsync_CancelledShift_ExcludesShift()
    {
        // Arrange
        await AddShiftAsync(At(9), At(17), statusTypeCode: CalendarEventStatusTypeCodes.Cancelled);

        // Act
        var results = await QueryAsync();

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task QueryAsync_CancelledAssignment_ExcludesAssignmentMinutes()
    {
        // Arrange
        var shift = await AddShiftAsync(At(9), At(17));
        await AddAssignmentAsync(shift, At(8), At(9), statusTypeCode: CalendarEventStatusTypeCodes.Cancelled);

        // Act
        var result = Assert.Single(await QueryAsync());

        // Assert
        Assert.Equal(0, result.PaidOutsideShiftMinutes);
        Assert.Equal(420, result.CreditedMinutes);
    }

    [Fact]
    public async Task QueryAsync_DateRangeExceedsConfiguredMaximum_ThrowsValidationException()
    {
        // Arrange
        var query = new WorkingHoursQuery { StartDate = BusinessDate, EndDate = BusinessDate.AddDays(31) };

        // Act
        var exception = await Assert.ThrowsAsync<ValidationException>(() => QueryAsync(query));

        // Assert
        Assert.Contains("cannot exceed 31 days", exception.Message);
    }

    [Fact]
    public async Task QueryAsync_CancelledToken_DoesNotReturnResults()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.QueryAsync(
                new WorkingHoursQuery { StartDate = BusinessDate, EndDate = BusinessDate },
                cancellation.Token
            )
        );
    }

    [Fact]
    public async Task QueryAsync_NoShifts_ReturnsNoLeaveOnlyResults()
    {
        await AddLeaveAsync(At(9), At(17), true);
        Assert.Empty(await QueryAsync());
    }

    [Theory]
    [InlineData(true, 9, 17, 0, 0, 420, 0, 420, 0)]
    [InlineData(false, 9, 17, 0, 0, 0, 420, 0, 0)]
    [InlineData(true, 10, 13, 0, 240, 180, 0, 420, 0)]
    [InlineData(false, 10, 13, 0, 240, 0, 180, 240, 0)]
    [InlineData(true, 10, 13, 60, 300, 180, 0, 480, 60)]
    [InlineData(false, 10, 13, 60, 300, 0, 180, 300, 0)]
    [InlineData(true, 10, 13, 30, 270, 180, 0, 450, 30)]
    [InlineData(false, 10, 13, 30, 270, 0, 180, 270, 0)]
    [InlineData(true, 9, 17, 60, 0, 420, 0, 420, 0)]
    [InlineData(false, 9, 17, 60, 0, 0, 420, 0, 0)]
    [InlineData(true, 9, 16, 60, 0, 420, 0, 420, 0)]
    [InlineData(false, 9, 16, 60, 0, 0, 420, 0, 0)]
    [InlineData(true, 8, 18, 0, 0, 420, 0, 420, 0)]
    [InlineData(false, 8, 18, 0, 0, 0, 420, 0, 0)]
    public async Task QueryAsync_LeaveAndWorkedLunch_SeparatesWorkCreditAndPotentialOvertime(
        bool paid,
        int start,
        int end,
        int workedLunch,
        int worked,
        int paidLeave,
        int unpaidLeave,
        int credited,
        int overtime
    )
    {
        await AddShiftAsync(At(9), At(17), workedLunchMinutes: workedLunch);
        await AddLeaveAsync(At(start), At(end), paid);

        var result = Assert.Single(await QueryAsync());

        var expectedWorkedLunch = worked - (420 - paidLeave - unpaidLeave);
        Assert.Equal(420 + expectedWorkedLunch, result.PaidShiftMinutes);
        Assert.Equal(expectedWorkedLunch, result.WorkedLunchMinutes);
        Assert.Equal(worked, result.WorkedMinutes);
        Assert.Equal(paidLeave, result.PaidLeaveMinutes);
        Assert.Equal(unpaidLeave, result.UnpaidLeaveMinutes);
        Assert.Equal(credited, result.CreditedMinutes);
        Assert.Equal(overtime, result.OvertimeMinutes);
        Assert.InRange(result.PaidLeaveMinutes!.Value + result.UnpaidLeaveMinutes!.Value, 0, 420);
        Assert.All(
            new[]
            {
                result.PaidShiftMinutes,
                result.WorkedLunchMinutes,
                result.WorkedMinutes,
                result.PaidLeaveMinutes,
                result.UnpaidLeaveMinutes,
                result.CreditedMinutes,
                result.OvertimeMinutes,
            },
            value => Assert.True(value >= 0)
        );
    }

    [Theory]
    // Client-confirmed scenarios 1-4: regular work, lunch, leave and outside-shift assignments.
    [InlineData(4, true, 60, 0, 240, 240, 0, 480, 60)]
    [InlineData(4, true, 0, 1, 240, 240, 0, 480, 60)]
    [InlineData(4, false, 0, 1, 240, 0, 240, 240, 0)]
    [InlineData(5, true, 0, 3, 300, 300, 0, 600, 180)]
    public async Task QueryAsync_ClientConfirmedScenarios_ReturnsAgreedWorkLeaveCreditAndOvertime(
        int leaveHours,
        bool paid,
        int workedLunch,
        int outsideHours,
        int worked,
        int paidLeave,
        int unpaidLeave,
        int credited,
        int overtime
    )
    {
        var shift = await AddShiftAsync(At(9), At(17), workedLunchMinutes: workedLunch);
        await AddLeaveAsync(At(13), At(17), paid);
        if (leaveHours == 5)
            await AddLeaveAsync(At(9), At(10), paid);
        if (outsideHours > 0)
            await AddAssignmentAsync(shift, At(17), At(17 + outsideHours));

        var result = Assert.Single(await QueryAsync());

        Assert.Equal(worked, result.WorkedMinutes);
        Assert.Equal(paidLeave, result.PaidLeaveMinutes);
        Assert.Equal(unpaidLeave, result.UnpaidLeaveMinutes);
        Assert.Equal(credited, result.CreditedMinutes);
        Assert.Equal(overtime, result.OvertimeMinutes);
    }

    [Theory]
    [InlineData(420, 120)]
    [InlineData(480, 60)]
    [InlineData(540, 0)]
    public async Task QueryAsync_ConfiguredWorkingDayThreshold_ChangesOnlyOvertime(int threshold, int overtime)
    {
        service = new WorkingHoursService(
            NullLogger<WorkingHoursService>.Instance,
            db,
            Options.Create(new WorkingHoursOptions { FullWorkingDayMinutes = threshold }),
            new TimeZoneService(),
            Options.Create(leaveFlags),
            new CalendarTimeZoneResolver(
                Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "America/Vancouver" }),
                new TimeZoneService()
            ),
            availability
        );
        var shift = await AddShiftAsync(At(9), At(17), workedLunchMinutes: 60);
        await AddLeaveAsync(At(13), At(17), true);
        await AddAssignmentAsync(shift, At(17), At(18));

        var result = Assert.Single(await QueryAsync());

        Assert.Equal(300, result.WorkedMinutes);
        Assert.Equal(240, result.PaidLeaveMinutes);
        Assert.Equal(0, result.UnpaidLeaveMinutes);
        Assert.Equal(540, result.CreditedMinutes);
        Assert.Equal(overtime, result.OvertimeMinutes);
    }

    [Theory]
    [InlineData(true, 180, 0, 480, 60)]
    [InlineData(false, 0, 180, 300, 0)]
    public async Task QueryAsync_LeaveWithAssignments_UsesCreditedTimeForPotentialOvertime(
        bool paid,
        int paidLeave,
        int unpaidLeave,
        int credited,
        int overtime
    )
    {
        var shift = await AddShiftAsync(At(9), At(17));
        await AddLeaveAsync(At(10), At(13), paid);
        await AddAssignmentAsync(shift, At(10), At(13));
        await AddAssignmentAsync(shift, At(16), At(18));
        await AddAssignmentAsync(shift, At(17), At(18));

        var result = Assert.Single(await QueryAsync());

        Assert.Equal(300, result.WorkedMinutes);
        Assert.Equal(paidLeave, result.PaidLeaveMinutes);
        Assert.Equal(unpaidLeave, result.UnpaidLeaveMinutes);
        Assert.Equal(credited, result.CreditedMinutes);
        Assert.Equal(60, result.PaidOutsideShiftMinutes);
        Assert.Equal(overtime, result.OvertimeMinutes);
    }

    [Fact]
    public async Task QueryAsync_FiveHoursPaidLeaveAndFiveHoursWork_ExceedsSevenHourThresholdByThreeHours()
    {
        await AddShiftAsync(At(7), At(18));
        await AddLeaveAsync(At(7), At(12), true);

        var result = Assert.Single(await QueryAsync());

        Assert.Equal(300, result.WorkedMinutes);
        Assert.Equal(300, result.PaidLeaveMinutes);
        Assert.Equal(0, result.UnpaidLeaveMinutes);
        Assert.Equal(600, result.CreditedMinutes);
        Assert.Equal(180, result.OvertimeMinutes);
    }

    [Theory]
    [InlineData(6, 0)]
    [InlineData(10, 210)]
    public async Task QueryAsync_NonSevenHourShift_UsesConfiguredThreshold(int durationHours, int overtime)
    {
        var shift = await AddShiftAsync(At(7), At(7 + durationHours), workedLunchMinutes: 30);
        await AddAssignmentAsync(shift, At(6), At(7));

        var result = Assert.Single(await QueryAsync());

        Assert.Equal(durationHours * 60 + 30, result.WorkedMinutes);
        Assert.Equal(0, result.PaidLeaveMinutes);
        Assert.Equal(0, result.UnpaidLeaveMinutes);
        Assert.Equal(durationHours * 60 + 30, result.CreditedMinutes);
        Assert.Equal(overtime, result.OvertimeMinutes);
    }

    [Theory]
    [InlineData(false, 240, 0, 180)]
    [InlineData(true, 240, 60, 120)]
    public async Task QueryAsync_OverlappingLeave_UsesUnionAndConservativeUnpaidClassification(
        bool mixed,
        int worked,
        int paid,
        int unpaid
    )
    {
        await AddShiftAsync(At(9), At(17));
        await AddLeaveAsync(At(10), At(12), mixed);
        await AddLeaveAsync(At(11), At(13), false);
        await AddLeaveAsync(At(11), At(12), mixed);

        var result = Assert.Single(await QueryAsync());

        Assert.Equal(worked, result.WorkedMinutes);
        Assert.Equal(paid, result.PaidLeaveMinutes);
        Assert.Equal(unpaid, result.UnpaidLeaveMinutes);
        Assert.Equal(worked + paid, result.CreditedMinutes);
    }

    [Fact]
    public async Task QueryAsync_OverlappingPaidLeave_CountsEachMinuteOnlyOnce()
    {
        await AddShiftAsync(At(9), At(17));
        await AddLeaveAsync(At(10), At(12), true);
        await AddLeaveAsync(At(11), At(13), true);

        var result = Assert.Single(await QueryAsync());
        Assert.Equal(180, result.PaidLeaveMinutes);
        Assert.Equal(240, result.WorkedMinutes);
        Assert.Equal(420, result.CreditedMinutes);
    }

    [Fact]
    public async Task QueryAsync_MixedLeaveExceedsRegularTime_CapsCombinedLeaveConservatively()
    {
        await AddShiftAsync(At(9), At(17));
        await AddLeaveAsync(At(9), At(13), true);
        await AddLeaveAsync(At(13), At(17), false);

        var result = Assert.Single(await QueryAsync());

        Assert.Equal(0, result.WorkedMinutes);
        Assert.Equal(180, result.PaidLeaveMinutes);
        Assert.Equal(240, result.UnpaidLeaveMinutes);
        Assert.Equal(180, result.CreditedMinutes);
    }

    [Fact]
    public async Task QueryAsync_UserWideLeaveOnSharedShift_AppliesOnlyToMatchingFilteredUser()
    {
        await AddShiftAsync(At(9), At(17), userIds: [UserA, UserB]);
        await AddLeaveAsync(At(10), At(13), true, locationId: 9);
        await AddLeaveAsync(AtDay(2, 10), AtDay(2, 13), true);

        var results = await QueryAsync(
            new WorkingHoursQuery
            {
                StartDate = BusinessDate,
                EndDate = BusinessDate,
                ShiftLocationIds = [5],
            }
        );
        Assert.Equal(180, Assert.Single(results, x => x.UserId == UserA).PaidLeaveMinutes);
        Assert.Equal(0, Assert.Single(results, x => x.UserId == UserB).PaidLeaveMinutes);

        var filtered = Assert.Single(
            await QueryAsync(
                new WorkingHoursQuery
                {
                    StartDate = BusinessDate,
                    EndDate = BusinessDate,
                    ShiftLocationIds = [5],
                    UserIds = [UserB],
                }
            )
        );
        Assert.Equal(UserB, filtered.UserId);
        Assert.Equal(420, filtered.WorkedMinutes);
    }

    [Fact]
    public async Task QueryAsync_CancelledInvalidAndNonOverlappingLeave_DoesNotAdjustShift()
    {
        await AddShiftAsync(At(9), At(17));
        await AddLeaveAsync(At(10), At(13), true, status: CalendarEventStatusTypeCodes.Cancelled);
        await AddLeaveAsync(At(10), null, false);
        await AddLeaveAsync(At(8), At(9), true);
        await AddLeaveAsync(At(17), At(18), false);

        var result = Assert.Single(await QueryAsync());
        Assert.Equal(420, result.WorkedMinutes);
        Assert.Equal(0, result.PaidLeaveMinutes);
        Assert.Equal(0, result.UnpaidLeaveMinutes);
    }

    [Fact]
    public async Task QueryAsync_MaterializedRecurringLeave_IsIncluded()
    {
        await AddShiftAsync(At(9), At(17));
        await AddLeaveAsync(At(10), At(13), true, recurring: true);

        var result = Assert.Single(await QueryAsync());
        Assert.Equal(180, result.PaidLeaveMinutes);
        Assert.Equal(240, result.WorkedMinutes);
    }

    [Fact]
    public async Task QueryAsync_OvernightLeave_IsClippedAndKeepsLocalStartBusinessDate()
    {
        await AddShiftAsync(AtDay(2, 6), AtDay(2, 14), timeZoneId: "America/Vancouver");
        await AddLeaveAsync(AtDay(2, 4), AtDay(2, 8), true);

        var result = Assert.Single(await QueryAsync());
        Assert.Equal(BusinessDate, result.Date);
        Assert.Equal(120, result.PaidLeaveMinutes);
        Assert.Equal(300, result.WorkedMinutes);
    }

    [Theory]
    [InlineData(null, "America/Vancouver", 0, 1)]
    [InlineData("", "America/Vancouver", 0, 1)]
    [InlineData("UTC", "America/Vancouver", 0, 2)]
    [InlineData(null, "America/Toronto", 6, 2)]
    [InlineData("America/Vancouver", "America/Toronto", 6, 1)]
    [InlineData(null, "", 6, 1)]
    public async Task QueryAsync_UsesEffectiveTimeZoneForBusinessDateAndFiltering(
        string? eventTimeZoneId,
        string locationTimeZoneId,
        int utcHour,
        int expectedDay
    )
    {
        var location = await db.Locations.FindAsync([5], TestContext.Current.CancellationToken);
        location!.Timezone = locationTimeZoneId;
        var start = new DateTimeOffset(2026, 7, 2, utcHour, 0, 0, TimeSpan.Zero);
        await AddShiftAsync(start, start.AddHours(8), timeZoneId: eventTimeZoneId);
        var date = new DateOnly(2026, 7, expectedDay);

        var result = Assert.Single(await QueryAsync(new WorkingHoursQuery { StartDate = date, EndDate = date }));
        Assert.Equal(date, result.Date);
        Assert.Equal(420, result.PaidShiftMinutes);

        var otherDate = new DateOnly(2026, 7, expectedDay == 1 ? 2 : 1);
        Assert.Empty(await QueryAsync(new WorkingHoursQuery { StartDate = otherDate, EndDate = otherDate }));
    }

    [Theory]
    [InlineData("America/Vancouver")]
    [InlineData(null)]
    public async Task QueryAsync_DaylightSavingTransition_UsesElapsedUtcMinutes(string? timeZoneId)
    {
        var start = new DateTimeOffset(2026, 11, 1, 7, 0, 0, TimeSpan.Zero);
        await AddShiftAsync(start, start.AddHours(9), timeZoneId: timeZoneId);
        await AddLeaveAsync(start.AddHours(1), start.AddHours(3), true);

        var date = new DateOnly(2026, 11, 1);
        var result = Assert.Single(await QueryAsync(new WorkingHoursQuery { StartDate = date, EndDate = date }));
        Assert.Equal(date, result.Date);
        Assert.Equal(480, result.PaidShiftMinutes);
        Assert.Equal(120, result.PaidLeaveMinutes);
        Assert.Equal(360, result.WorkedMinutes);
    }

    [Fact]
    public async Task QueryAsync_LeaveDisabled_IgnoresLeaveAndPreservesWorkAndOvertime()
    {
        leaveFlags.Enabled = false;
        var shift = await AddShiftAsync(At(9), At(17), workedLunchMinutes: 30);
        await AddLeaveAsync(At(9), At(17), false);
        await AddAssignmentAsync(shift, At(17), At(18));

        var result = Assert.Single(await QueryAsync());
        Assert.Equal(510, result.WorkedMinutes);
        Assert.Equal(510, result.CreditedMinutes);
        Assert.Equal(90, result.OvertimeMinutes);
        Assert.Equal(0, result.PaidLeaveMinutes);
        Assert.Equal(0, result.UnpaidLeaveMinutes);
    }

    [Fact]
    public async Task QueryAsync_OnlyThirtyRegularMinutesRemain_CapsEffectiveWorkedLunch()
    {
        await AddShiftAsync(At(9), At(17), workedLunchMinutes: 60);
        await AddLeaveAsync(At(9), At(15, 30), true);
        var result = Assert.Single(await QueryAsync());
        Assert.Equal(30, result.WorkedLunchMinutes);
        Assert.Equal(60, result.WorkedMinutes);
        Assert.Equal(390, result.PaidLeaveMinutes);
        Assert.Equal(0, result.UnpaidLeaveMinutes);
        Assert.Equal(450, result.CreditedMinutes);
        Assert.Equal(30, result.OvertimeMinutes);
    }

    [Fact]
    public async Task QueryAsync_UnknownLunch_IsUnavailableUntilExplicitlyEdited()
    {
        var shift = await AddShiftAsync(At(9), At(17));
        shift.LunchAvailableMinutes = null;
        shift.WorkedLunchMinutes = null;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var legacy = Assert.Single(await QueryAsync());
        Assert.Null(legacy.PaidShiftMinutes);
        Assert.Null(legacy.WorkedLunchMinutes);
        Assert.Null(legacy.WorkedMinutes);
        Assert.Null(legacy.CreditedMinutes);
        Assert.Null(legacy.OvertimeMinutes);

        shift.LunchAvailableMinutes = 0;
        shift.WorkedLunchMinutes = 0;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var explicitZero = Assert.Single(await QueryAsync());
        Assert.Equal(480, explicitZero.WorkedMinutes);
        Assert.Equal(60, explicitZero.OvertimeMinutes);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(9999, 12, 31)]
    public async Task QueryAsync_BoundaryDate_DoesNotOverflow(int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);
        Assert.Empty(await QueryAsync(new WorkingHoursQuery { StartDate = date, EndDate = date }));
    }

    [Fact]
    public async Task OnMaterializedEventCreatedAsync_ShiftSeriesLunchValuesDiffer_PreservesBothValuesIndependently()
    {
        // Arrange
        var shiftSeries = new ShiftSeries { LunchAvailableMinutes = 45, WorkedLunchMinutes = 15 };
        var eventSeries = new EventSeries
        {
            Title = "Series",
            StartAtUtc = At(9),
            EndAtUtc = At(17),
        };
        var eventEntity = new Event
        {
            Title = "Occurrence",
            StartAtUtc = At(9),
            EndAtUtc = At(17),
        };
        var handler = new ShiftSeriesMaterializationHandler(db);

        // Act
        await handler.OnMaterializedEventCreatedAsync(
            eventSeries,
            eventEntity,
            new SeriesEntry { StartAtUtc = At(9), EndAtUtc = At(17) },
            new ShiftSeriesMaterializationContext { ShiftSeries = shiftSeries, UserIds = [UserA] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var entry = Assert.Single(db.ShiftEntries.Local);
        Assert.Equal(45, entry.LunchAvailableMinutes);
        Assert.Equal(15, entry.WorkedLunchMinutes);
    }

    private async Task<ShiftEntry> AddShiftAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        int locationId = 5,
        IReadOnlyCollection<Guid>? userIds = null,
        int lunchAvailableMinutes = 60,
        int workedLunchMinutes = 0,
        string? timeZoneId = "UTC",
        string statusTypeCode = CalendarEventStatusTypeCodes.Active
    )
    {
        var shift = new ShiftEntry
        {
            Event = new Event
            {
                Title = "Shift",
                StartAtUtc = start,
                EndAtUtc = end,
                TimeZoneId = timeZoneId,
                EventTypeCode = SchedulingConstants.ShiftEventTypeCode,
                StatusTypeCode = statusTypeCode,
                SourceModule = SchedulingConstants.SourceModule,
                LocationId = locationId,
            },
            LunchAvailableMinutes = lunchAvailableMinutes,
            WorkedLunchMinutes = workedLunchMinutes,
            Users = (userIds ?? [UserA]).Select(userId => new ShiftEntryUser { UserId = userId }).ToList(),
        };

        db.ShiftEntries.Add(shift);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return shift;
    }

    private async Task AddAssignmentAsync(
        ShiftEntry shift,
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlyCollection<Guid>? userIds = null,
        string statusTypeCode = CalendarEventStatusTypeCodes.Active
    )
    {
        var assignment = new AssignmentEntry
        {
            AssignmentDefinitionId = 100,
            Event = new Event
            {
                Title = "Assignment",
                StartAtUtc = start,
                EndAtUtc = end,
                TimeZoneId = "UTC",
                EventTypeCode = SchedulingConstants.AssignmentEventTypeCode,
                StatusTypeCode = statusTypeCode,
                SourceModule = SchedulingConstants.SourceModule,
                LocationId = 5,
            },
            Capacity = 10,
            CategoryId = 10,
            SubCategoryId = 11,
        };
        var link = new ShiftAssignmentEntry
        {
            ShiftEntry = shift,
            AssignmentEntry = assignment,
            Users = (userIds ?? [UserA]).Select(userId => new ShiftAssignmentEntryUser { UserId = userId }).ToList(),
        };

        db.ShiftAssignmentEntries.Add(link);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task AddLeaveAsync(
        DateTimeOffset start,
        DateTimeOffset? end,
        bool paid,
        int? locationId = null,
        string status = CalendarEventStatusTypeCodes.Active,
        bool recurring = false
    )
    {
        var entry = new LeaveEntry
        {
            LeaveTypeId = paid ? 201 : 202,
            Event = new Event
            {
                Title = "Leave",
                StartAtUtc = start,
                EndAtUtc = end,
                TimeZoneId = "UTC",
                SourceModule = LeaveConstants.SourceModule,
                EventTypeCode = LeaveConstants.LeaveEventTypeCode,
                StatusTypeCode = status,
                LocationId = locationId,
            },
            Users = [new LeaveEntryUser { UserId = UserA }],
        };
        if (recurring)
        {
            entry.LeaveSeries = new LeaveSeries
            {
                LeaveTypeId = entry.LeaveTypeId,
                EventSeries = new EventSeries
                {
                    Title = "Recurring leave",
                    StartAtUtc = start,
                    EndAtUtc = end,
                    EventTypeCode = LeaveConstants.LeaveEventTypeCode,
                    StatusTypeCode = CalendarEventStatusTypeCodes.Active,
                    RecurrenceRule = "FREQ=DAILY;COUNT=2",
                },
            };
            entry.Event.EventSeries = entry.LeaveSeries.EventSeries;
        }
        db.LeaveEntries.Add(entry);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private Task<IReadOnlyCollection<WorkingHoursResult>> QueryAsync(WorkingHoursQuery? query = null) =>
        service.QueryAsync(
            query ?? new WorkingHoursQuery { StartDate = BusinessDate, EndDate = BusinessDate },
            TestContext.Current.CancellationToken
        );

    private async Task SeedBaseDataAsync()
    {
        db.EventTypes.AddRange(
            CreateEventType(SchedulingConstants.ShiftEventTypeCode),
            CreateEventType(SchedulingConstants.AssignmentEventTypeCode),
            CreateEventType(LoanOutConstants.LoanOutEventTypeCode),
            CreateEventType(LeaveConstants.LeaveEventTypeCode)
        );
        db.EventStatusTypes.AddRange(
            CreateStatusType(CalendarEventStatusTypeCodes.Active),
            CreateStatusType(CalendarEventStatusTypeCodes.Cancelled)
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
                Timezone = "America/Toronto",
            }
        );
        db.Users.AddRange(CreateUser(UserA, "A"), CreateUser(UserB, "B"));
        db.LeaveTypes.AddRange(
            new LeaveType
            {
                Id = 201,
                Code = "paid",
                Description = "Paid",
                IsPaid = true,
            },
            new LeaveType
            {
                Id = 202,
                Code = "unpaid",
                Description = "Unpaid",
                IsPaid = false,
            }
        );
        db.StatGroups.Add(new StatGroup { Id = 1, Name = "Group" });
        db.StatCategories.Add(
            new StatCategory
            {
                Id = 10,
                GroupId = 1,
                Name = "Category",
            }
        );
        db.SubCategories.Add(
            new SubCategory
            {
                Id = 11,
                CategoryId = 10,
                Name = "Subcategory",
            }
        );
        db.AssignmentDefinitions.Add(
            new AssignmentDefinition
            {
                Id = 100,
                LocationId = 5,
                Name = "Definition",
                NormalizedName = "DEFINITION",
                CategoryId = 10,
                SubCategoryId = 11,
                DefaultCapacity = 10,
                EffectiveDateUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            }
        );

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static DateTimeOffset At(int hour, int minute = 0) => new(2026, 6, 1, hour, minute, 0, TimeSpan.Zero);

    private static DateTimeOffset AtDay(int day, int hour, int minute = 0) =>
        new(2026, 6, day, hour, minute, 0, TimeSpan.Zero);

    private static EventType CreateEventType(string code) =>
        new()
        {
            Code = code,
            Description = code,
            EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

    private static EventStatusType CreateStatusType(string code) =>
        new()
        {
            Code = code,
            Description = code,
            EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
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
