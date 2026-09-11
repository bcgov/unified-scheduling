using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.UserManagement;
using Unified.UserManagement;
using Unified.UserManagement.Models;
using Unified.UserManagement.Services;

namespace Unified.Tests.UserManagement.Services;

public class LeaveServiceTests : IAsyncLifetime
{
    private UnifiedDbContext _dbContext = null!;
    private LeaveService _service = null!;

    private static readonly Guid UserId = Guid.NewGuid();
    private const int LocationId = 1;
    private const string VacationCode = "VAC";
    private const string SickCode = "SICK";

    public ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new UnifiedDbContext(options);
        _service = new LeaveService(_dbContext);

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    private async Task SeedCoreEntitiesAsync()
    {
        _dbContext.Locations.Add(
            new Location
            {
                Id = LocationId,
                AgencyId = "LOC-001",
                Name = "Victoria",
                Timezone = "America/Vancouver",
            }
        );

        _dbContext.Users.Add(
            new User
            {
                Id = UserId,
                IdirName = "testuser",
                IsEnabled = true,
                FirstName = "Test",
                LastName = "User",
                Email = "test@example.com",
                Gender = Gender.Male,
                HomeLocationId = LocationId,
            }
        );

        _dbContext.LeaveTypes.AddRange(
            new LeaveType
            {
                Code = VacationCode,
                Description = "Vacation",
                IsPaid = true,
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            },
            new LeaveType
            {
                Code = SickCode,
                Description = "Sick",
                IsPaid = true,
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            }
        );

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<UserLeave> SeedLeaveAsync(
        string leaveTypeCode = VacationCode,
        DateTimeOffset? cancelledAtUtc = null,
        DateTimeOffset? startAtUtc = null,
        DateTimeOffset? endAtUtc = null
    )
    {
        var leaveType = await _dbContext.LeaveTypes.SingleAsync(
            x => x.Code == leaveTypeCode,
            TestContext.Current.CancellationToken
        );

        var calendarEvent = new Event
        {
            Title = "Leave - Test",
            StartAtUtc = startAtUtc ?? DateTimeOffset.UtcNow.AddDays(-1),
            EndAtUtc = endAtUtc ?? DateTimeOffset.UtcNow.AddDays(1),
            TimeZoneId = "America/Vancouver",
            EventTypeCode = UserManagementConstants.LeaveEventTypeCode,
            StatusTypeCode = CalendarEventStatusTypeCodes.Active,
            SourceModule = UserManagementConstants.SourceModule,
            CancelledAt = cancelledAtUtc,
        };
        _dbContext.Events.Add(calendarEvent);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var leave = new UserLeave
        {
            UserId = UserId,
            EventId = calendarEvent.Id,
            LeaveTypeId = leaveType.Id,
        };
        _dbContext.UserLeaves.Add(leave);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return leave;
    }

    #region GetByUserIdAsync

    [Fact]
    public async Task GetByUserIdAsync_Should_Return_Active_Leaves()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        await SeedLeaveAsync();

        // Act
        var result = await _service.GetByUserIdAsync(UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        var dto = result.First();
        Assert.Equal(UserId, dto.UserId);
        Assert.Equal(VacationCode, dto.LeaveTypeCode);
        Assert.Equal("Vacation", dto.LeaveTypeDescription);
        Assert.True(dto.IsPaid);
        Assert.Null(dto.ExpiryAtUtc);
    }

    [Fact]
    public async Task GetByUserIdAsync_Should_Exclude_Expired_Leaves()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        await SeedLeaveAsync(cancelledAtUtc: DateTimeOffset.UtcNow.AddDays(-1));

        // Act
        var result = await _service.GetByUserIdAsync(UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByUserIdAsync_Should_Return_Empty_When_No_Leaves()
    {
        // Arrange
        await SeedCoreEntitiesAsync();

        // Act
        var result = await _service.GetByUserIdAsync(UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByUserIdAsync_Should_Throw_When_User_Not_Found()
    {
        // Arrange — no seed

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.GetByUserIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task GetByUserIdAsync_Should_Order_By_StartDate_Descending()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        await SeedLeaveAsync(leaveTypeCode: VacationCode, startAtUtc: DateTimeOffset.UtcNow.AddDays(-10));
        await SeedLeaveAsync(leaveTypeCode: SickCode, startAtUtc: DateTimeOffset.UtcNow.AddDays(-2));

        // Act
        var result = await _service.GetByUserIdAsync(UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(SickCode, result.First().LeaveTypeCode);
    }

    #endregion

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_Should_Create_And_Return_Leave()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        var request = new LeaveRequestDto
        {
            LeaveTypeCode = VacationCode,
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-01-20T00:00:00.000-08:00",
            Timezone = "America/Vancouver",
            Comment = "Family trip",
        };

        // Act
        var result = await _service.CreateAsync(UserId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(0, result.Id);
        Assert.Equal(UserId, result.UserId);
        Assert.Equal(VacationCode, result.LeaveTypeCode);
        Assert.Equal("Vacation", result.LeaveTypeDescription);
        Assert.True(result.IsPaid);
        Assert.Equal("America/Vancouver", result.Timezone);
        Assert.Equal("Family trip", result.Comment);
        Assert.Null(result.ExpiryAtUtc);
    }

    [Fact]
    public async Task CreateAsync_Should_Use_Request_Timezone_When_Provided()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        var request = new LeaveRequestDto
        {
            LeaveTypeCode = VacationCode,
            StartDateTime = "2026-01-10T00:00:00.000-07:00",
            EndDateTime = "2026-01-20T00:00:00.000-07:00",
            Timezone = "America/Edmonton",
        };

        // Act
        var result = await _service.CreateAsync(UserId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("America/Edmonton", result.Timezone);
    }

    [Fact]
    public async Task CreateAsync_Should_Fall_Back_To_Home_Location_Timezone_When_Request_Timezone_Is_Null()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        var request = new LeaveRequestDto
        {
            LeaveTypeCode = VacationCode,
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-01-20T00:00:00.000-08:00",
            Timezone = null,
        };

        // Act
        var result = await _service.CreateAsync(UserId, request, TestContext.Current.CancellationToken);

        // Assert — falls back to home location.Timezone = "America/Vancouver"
        Assert.Equal("America/Vancouver", result.Timezone);
    }

    [Fact]
    public async Task CreateAsync_Should_Persist_Leave_To_Database()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        var request = new LeaveRequestDto
        {
            LeaveTypeCode = VacationCode,
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-01-20T00:00:00.000-08:00",
        };

        // Act
        await _service.CreateAsync(UserId, request, TestContext.Current.CancellationToken);

        // Assert
        var saved = await _dbContext
            .UserLeaves.Include(x => x.Event)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(UserId, saved.UserId);
        Assert.Equal(UserManagementConstants.LeaveEventTypeCode, saved.Event.EventTypeCode);
        Assert.Equal(UserManagementConstants.SourceModule, saved.Event.SourceModule);
    }

    [Fact]
    public async Task CreateAsync_Should_Persist_AllDay_When_True()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        var request = new LeaveRequestDto
        {
            LeaveTypeCode = VacationCode,
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-01-20T00:00:00.000-08:00",
            AllDay = true,
        };

        // Act
        var result = await _service.CreateAsync(UserId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.AllDay);
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_User_Not_Found()
    {
        // Arrange — no seed
        var request = new LeaveRequestDto
        {
            LeaveTypeCode = VacationCode,
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-01-20T00:00:00.000-08:00",
        };

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CreateAsync(Guid.NewGuid(), request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_LeaveType_Not_Found()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        var request = new LeaveRequestDto
        {
            LeaveTypeCode = "UNKNOWN",
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-01-20T00:00:00.000-08:00",
        };

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CreateAsync(UserId, request, TestContext.Current.CancellationToken)
        );
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_Should_Update_And_Return_Leave()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        var seeded = await SeedLeaveAsync();
        var newStart = DateTimeOffset.Parse("2026-02-01T00:00:00.000-08:00").ToUniversalTime();

        var request = new LeaveRequestDto
        {
            LeaveTypeCode = SickCode,
            StartDateTime = "2026-02-01T00:00:00.000-08:00",
            EndDateTime = "2026-02-05T00:00:00.000-08:00",
            Comment = "Updated comment",
        };

        // Act
        var result = await _service.UpdateAsync(UserId, seeded.Id, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(seeded.Id, result.Id);
        Assert.Equal(SickCode, result.LeaveTypeCode);
        Assert.Equal("Updated comment", result.Comment);
        Assert.Equal(newStart, result.StartAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_Should_Update_AllDay()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        var seeded = await SeedLeaveAsync();

        var request = new LeaveRequestDto
        {
            LeaveTypeCode = VacationCode,
            StartDateTime = "2026-02-01T00:00:00.000-08:00",
            EndDateTime = "2026-02-05T00:00:00.000-08:00",
            AllDay = true,
        };

        // Act
        var result = await _service.UpdateAsync(UserId, seeded.Id, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.AllDay);
    }

    [Fact]
    public async Task UpdateAsync_Should_Throw_When_Leave_Not_Found()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        var request = new LeaveRequestDto
        {
            LeaveTypeCode = VacationCode,
            StartDateTime = "2026-02-01T00:00:00.000-08:00",
            EndDateTime = "2026-02-05T00:00:00.000-08:00",
        };

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.UpdateAsync(UserId, 9999, request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task UpdateAsync_Should_Throw_When_User_Not_Found()
    {
        // Arrange — no seed
        var request = new LeaveRequestDto
        {
            LeaveTypeCode = VacationCode,
            StartDateTime = "2026-02-01T00:00:00.000-08:00",
            EndDateTime = "2026-02-05T00:00:00.000-08:00",
        };

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.UpdateAsync(Guid.NewGuid(), 1, request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task UpdateAsync_Should_Throw_When_Leave_Already_Expired()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        var seeded = await SeedLeaveAsync(cancelledAtUtc: DateTimeOffset.UtcNow.AddDays(-1));

        var request = new LeaveRequestDto
        {
            LeaveTypeCode = VacationCode,
            StartDateTime = "2026-02-01T00:00:00.000-08:00",
            EndDateTime = "2026-02-05T00:00:00.000-08:00",
        };

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateAsync(UserId, seeded.Id, request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task UpdateAsync_Should_Throw_When_LeaveType_Not_Found()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        var seeded = await SeedLeaveAsync();

        var request = new LeaveRequestDto
        {
            LeaveTypeCode = "UNKNOWN",
            StartDateTime = "2026-02-01T00:00:00.000-08:00",
            EndDateTime = "2026-02-05T00:00:00.000-08:00",
        };

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.UpdateAsync(UserId, seeded.Id, request, TestContext.Current.CancellationToken)
        );
    }

    #endregion

    #region ExpireAsync

    [Fact]
    public async Task ExpireAsync_Should_Set_ExpiryAtUtc_And_Return()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        var seeded = await SeedLeaveAsync();

        var request = new ExpireLeaveRequestDto { LeaveId = seeded.Id, ExpiryReason = "ENTRYERR" };

        // Act
        var result = await _service.ExpireAsync(UserId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result.ExpiryAtUtc);
        Assert.Equal("ENTRYERR", result.ExpiryReason);
    }

    [Fact]
    public async Task ExpireAsync_Should_Trim_ExpiryReason()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        var seeded = await SeedLeaveAsync();

        var request = new ExpireLeaveRequestDto { LeaveId = seeded.Id, ExpiryReason = "  ENTRYERR  " };

        // Act
        var result = await _service.ExpireAsync(UserId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("ENTRYERR", result.ExpiryReason);
    }

    [Fact]
    public async Task ExpireAsync_Should_Throw_When_Leave_Not_Found()
    {
        // Arrange
        await SeedCoreEntitiesAsync();

        var request = new ExpireLeaveRequestDto { LeaveId = 9999, ExpiryReason = "ENTRYERR" };

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ExpireAsync(UserId, request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task ExpireAsync_Should_Throw_When_User_Not_Found()
    {
        // Arrange — no seed

        var request = new ExpireLeaveRequestDto { LeaveId = 1, ExpiryReason = "ENTRYERR" };

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ExpireAsync(Guid.NewGuid(), request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task ExpireAsync_Should_Throw_When_Leave_Already_Expired()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        var seeded = await SeedLeaveAsync(cancelledAtUtc: DateTimeOffset.UtcNow.AddDays(-1));

        var request = new ExpireLeaveRequestDto { LeaveId = seeded.Id, ExpiryReason = "ENTRYERR" };

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ExpireAsync(UserId, request, TestContext.Current.CancellationToken)
        );
    }

    #endregion
}
