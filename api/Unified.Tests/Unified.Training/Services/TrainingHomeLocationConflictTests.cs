using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unified.Calendar.Conflicts;
using Unified.Calendar.Models;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Common.Calendar.Conflicts;
using Unified.Common.Contracts;
using Unified.Common.Events;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.Scheduling;
using Unified.Db.Models.Stats;
using Unified.Db.Models.UserManagement;
using Unified.Scheduling.Services;
using Unified.Tests.TestHelpers;
using Unified.Training.Handlers;
using Unified.Training.Models;
using Unified.Training.Services;
using Unified.UserManagement.FeatureFlags;
using Unified.UserManagement.Models;
using Unified.UserManagement.Services;

namespace Unified.Tests.Training.Services;

public sealed class TrainingHomeLocationConflictTests : IAsyncLifetime
{
    private static readonly Guid UserId = new("11111111-1111-4111-8111-111111111111");
    private static readonly Guid OtherUserId = new("22222222-2222-4222-8222-222222222222");
    private static readonly DateTimeOffset TrainingDay = new(2026, 7, 13, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LateAssignment = TrainingDay.AddDays(1).AddHours(6).AddMinutes(15);
    private UnifiedDbContext _db = null!;
    private CalendarConflictService _conflicts = null!;
    private FailingTimeZoneResolver _resolver = null!;
    private FailureAfterInvalidation _failure = null!;
    private ServiceProvider _services = null!;
    private IServiceScope _scope = null!;
    private UserService _userService = null!;

    public async ValueTask InitializeAsync()
    {
        _db = new SqliteTestUnifiedDbContext(
            new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite("Data Source=:memory:").Options
        );
        await _db.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await _db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        _db.Locations.AddRange(
            new Location
            {
                Id = 1,
                AgencyId = "A1",
                Name = "Vancouver",
                Timezone = "America/Vancouver",
            },
            new Location
            {
                Id = 2,
                AgencyId = "A2",
                Name = "Edmonton",
                Timezone = "America/Edmonton",
            },
            new Location
            {
                Id = 3,
                AgencyId = "A3",
                Name = "Surrey",
                Timezone = "America/Vancouver",
            }
        );
        _db.Users.AddRange(
            new User
            {
                Id = UserId,
                IdirName = "training-user",
                HomeLocationId = 1,
            },
            new User
            {
                Id = OtherUserId,
                IdirName = "other-user",
                HomeLocationId = 1,
            }
        );
        _db.Trainings.Add(
            new global::Unified.Db.Models.Training.Training
            {
                Id = 1,
                Code = "FIRST-AID",
                Description = "Course",
            }
        );
        _db.EventTypes.AddRange(new EventType { Code = "assignment" }, new EventType { Code = "shift" });
        _db.EventStatusTypes.Add(new EventStatusType { Code = CalendarEventStatusTypeCodes.Active });
        _db.StatGroups.Add(new StatGroup { Id = 1, Name = "Group" });
        _db.StatCategories.Add(
            new StatCategory
            {
                Id = 10,
                GroupId = 1,
                Name = "Category",
            }
        );
        _db.SubCategories.Add(
            new SubCategory
            {
                Id = 11,
                CategoryId = 10,
                Name = "Subcategory",
            }
        );
        _db.AssignmentDefinitions.Add(
            new AssignmentDefinition
            {
                Id = 100,
                LocationId = 1,
                Name = "Coverage",
                NormalizedName = "COVERAGE",
                CategoryId = 10,
                SubCategoryId = 11,
                DefaultCapacity = 1,
                EffectiveDateUtc = TrainingDay.AddDays(-1),
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var time = new TimeZoneService();
        _resolver = new FailingTimeZoneResolver(
            new CalendarTimeZoneResolver(
                Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "UTC" }),
                time
            )
        );
        _conflicts = new CalendarConflictService(
            [
                new TrainingConflictParticipantProvider(_db, _resolver, time),
                new SchedulingConflictParticipantProvider(
                    _db,
                    SchedulingLoanOutTestServices.CreateAvailability(_db, new TimeZoneService())
                ),
            ],
            _db,
            CalendarConflictTestContext.Projection(OtherUserId),
            CalendarConflictTestRules.Scheduling
        );
        _failure = new FailureAfterInvalidation(_db);
        _services = new ServiceCollection()
            .AddSingleton(_db)
            .AddSingleton<ICalendarConflictService>(_conflicts)
            .AddScoped<
                IEntitySideEffect<UserHomeLocationChangedSignal>,
                InvalidateTrainingOverridesOnHomeLocationChangeHandler
            >()
            .AddSingleton<IEntitySideEffect<UserHomeLocationChangedSignal>>(_failure)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _scope = _services.CreateScope();
        _userService = CreateUserService(new EventDispatcher(_scope.ServiceProvider));
    }

    public async ValueTask DisposeAsync()
    {
        _scope.Dispose();
        await _services.DisposeAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task UpdateAsync_WhenHomeTimeZoneRemovesConflict_InvalidatesOverrideWithoutReactivation()
    {
        var original = await SeedOverriddenTrainingAsync(UserId, LateAssignment);
        var unrelated = await SeedOverriddenTrainingAsync(OtherUserId, LateAssignment);

        await UpdateHomeLocationAsync(2);

        var invalidated = await OverrideAsync(original.Id);
        Assert.NotNull(invalidated.InvalidatedOn);
        Assert.Empty(await ConflictsAsync(UserId));
        Assert.Null((await OverrideAsync(unrelated.Id)).InvalidatedOn);
        Assert.True(Assert.Single(await ConflictsAsync(OtherUserId)).IsOverridden);

        await UpdateHomeLocationAsync(1);

        Assert.False(Assert.Single(await ConflictsAsync(UserId)).IsOverridden);
        Assert.Equal(invalidated.InvalidatedOn, (await OverrideAsync(original.Id)).InvalidatedOn);
        Assert.Equal(unrelated.CreatedOn, (await OverrideAsync(unrelated.Id)).CreatedOn);
        Assert.Null((await OverrideAsync(unrelated.Id)).UpdatedOn);
        Assert.All(
            await _db.UserTrainings.ToListAsync(TestContext.Current.CancellationToken),
            record =>
            {
                Assert.Equal(TrainingDay, record.AwardedOn);
                Assert.Equal(TrainingDay, record.EndingOn);
            }
        );
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task UpdateAsync_WhenConflictStillExists_PreservesOverride(int homeLocationId)
    {
        var original = await SeedOverriddenTrainingAsync(UserId, TrainingDay.AddHours(17));

        await UpdateHomeLocationAsync(homeLocationId);

        Assert.Null((await OverrideAsync(original.Id)).InvalidatedOn);
        Assert.True(Assert.Single(await ConflictsAsync(UserId)).IsOverridden);
    }

    [Fact]
    public async Task UpdateAsync_WhenRecheckFails_RollsBackUserLocationAndOverride()
    {
        var original = await SeedOverriddenTrainingAsync(UserId, LateAssignment);
        _resolver.ThrowOnResolve = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => UpdateHomeLocationAsync(2));

        _resolver.ThrowOnResolve = false;
        _db.ChangeTracker.Clear();
        Assert.Equal(
            1,
            (
                await _db.Users.SingleAsync(user => user.Id == UserId, TestContext.Current.CancellationToken)
            ).HomeLocationId
        );
        Assert.Null((await OverrideAsync(original.Id)).InvalidatedOn);
        Assert.True(Assert.Single(await ConflictsAsync(UserId)).IsOverridden);
    }

    [Fact]
    public async Task UpdateAsync_WhenLaterSideEffectFails_RollsBackPersistedOverrideInvalidationAndUser()
    {
        var original = await SeedOverriddenTrainingAsync(UserId, LateAssignment);
        _failure.Enabled = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => UpdateHomeLocationAsync(2));

        _db.ChangeTracker.Clear();
        Assert.Equal(
            1,
            (
                await _db.Users.SingleAsync(user => user.Id == UserId, TestContext.Current.CancellationToken)
            ).HomeLocationId
        );
        Assert.Null((await OverrideAsync(original.Id)).InvalidatedOn);
    }

    [Fact]
    public async Task UpdateAsync_WhenHomeLocationIsUnchanged_DoesNotRecheckOverrides()
    {
        var original = await SeedOverriddenTrainingAsync(UserId, LateAssignment);
        _resolver.ThrowOnResolve = true;

        await UpdateHomeLocationAsync(1);

        Assert.Null((await OverrideAsync(original.Id)).InvalidatedOn);
    }

    [Fact]
    public async Task UpdateAsync_WithoutRegisteredHandlers_PersistsHomeLocation()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var service = CreateUserService(new EventDispatcher(services));

        await UpdateHomeLocationAsync(2, service);

        Assert.Equal(
            2,
            (
                await _db
                    .Users.AsNoTracking()
                    .SingleAsync(user => user.Id == UserId, TestContext.Current.CancellationToken)
            ).HomeLocationId
        );
    }

    private UserService CreateUserService(IEventDispatcher dispatcher) =>
        new(
            _db,
            Options.Create(new UserManagementFeatureFlags { Enabled = true }),
            NullLogger<UserService>.Instance,
            new TimeZoneService(),
            dispatcher
        );

    private Task<UserResponse?> UpdateHomeLocationAsync(int locationId, UserService? service = null) =>
        (service ?? _userService).UpdateAsync(
            UserId,
            new UserRequestDto
            {
                IdirName = "training-user",
                IsEnabled = true,
                FirstName = "Training",
                LastName = "User",
                Email = "training@example.com",
                Gender = Gender.Other,
                Rank = "",
                EmployeeNumber = "1",
                HomeLocationId = locationId,
            },
            TestContext.Current.CancellationToken
        );

    private async Task<CalendarConflictOverride> SeedOverriddenTrainingAsync(
        Guid userId,
        DateTimeOffset assignmentStart
    )
    {
        var training = await new UserTrainingService(_db, _conflicts).CreateAsync(
            new UserTrainingRequest
            {
                UserId = userId,
                TrainingId = 1,
                AwardedOn = TrainingDay,
                EndingOn = TrainingDay,
            },
            TestContext.Current.CancellationToken
        );
        var assignment = new AssignmentEntry
        {
            AssignmentDefinitionId = 100,
            CategoryId = 10,
            SubCategoryId = 11,
            Capacity = 1,
            Event = NewEvent("assignment", assignmentStart),
        };
        _db.ShiftAssignmentEntries.Add(
            new ShiftAssignmentEntry
            {
                AssignmentEntry = assignment,
                ShiftEntry = new ShiftEntry
                {
                    Event = NewEvent("shift", assignmentStart),
                    Users = [new ShiftEntryUser { UserId = userId }],
                },
                Users = [new ShiftAssignmentEntryUser { UserId = userId }],
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _conflicts.CreateOverrideAsync(
            new CalendarConflictAcknowledgement
            {
                FirstSourceModule = "training",
                FirstEventId = TrainingConflictParticipantProvider.IdentityFor(training.Id).EventId,
                SecondSourceModule = "scheduling",
                SecondEventId = assignment.EventId.ToString(),
                ResourceId = userId,
                Note = "Approved",
            },
            OtherUserId,
            TestContext.Current.CancellationToken
        );
        return await _db
            .CalendarConflictOverrides.AsNoTracking()
            .SingleAsync(item => item.ResourceId == userId, TestContext.Current.CancellationToken);
    }

    private static Event NewEvent(string eventType, DateTimeOffset start) =>
        new()
        {
            Title = eventType,
            SourceModule = "scheduling",
            EventTypeCode = eventType,
            StatusTypeCode = CalendarEventStatusTypeCodes.Active,
            TimeZoneId = "UTC",
            StartAtUtc = start,
            EndAtUtc = start.AddMinutes(30),
        };

    private Task<CalendarConflictOverride> OverrideAsync(int id) =>
        _db
            .CalendarConflictOverrides.AsNoTracking()
            .SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);

    private Task<IReadOnlyCollection<CalendarConflict>> ConflictsAsync(Guid userId) =>
        _conflicts.GetConflictsAsync(
            new CalendarConflictQuery(TrainingDay, TrainingDay.AddDays(2), [userId]),
            TestContext.Current.CancellationToken
        );

    private sealed class FailingTimeZoneResolver(ICalendarTimeZoneResolver inner) : ICalendarTimeZoneResolver
    {
        public bool ThrowOnResolve { get; set; }

        public TimeZoneInfo Resolve(string? requestedTimeZoneId, string? fallbackTimeZoneId = null) =>
            ThrowOnResolve
                ? throw new InvalidOperationException("Conflict recheck failed.")
                : inner.Resolve(requestedTimeZoneId, fallbackTimeZoneId);
    }

    private sealed class FailureAfterInvalidation(UnifiedDbContext db)
        : IEntitySideEffect<UserHomeLocationChangedSignal>
    {
        public bool Enabled { get; set; }

        public async Task HandleAsync(UserHomeLocationChangedSignal signal, CancellationToken cancellationToken)
        {
            if (!Enabled)
                return;
            Assert.NotNull(db.Database.CurrentTransaction);
            Assert.Equal(
                2,
                (
                    await db.Users.AsNoTracking().SingleAsync(user => user.Id == signal.UserId, cancellationToken)
                ).HomeLocationId
            );
            Assert.NotNull(
                (await db.CalendarConflictOverrides.AsNoTracking().SingleAsync(cancellationToken)).InvalidatedOn
            );
            throw new InvalidOperationException("Side effect failed after invalidation was persisted.");
        }
    }
}
