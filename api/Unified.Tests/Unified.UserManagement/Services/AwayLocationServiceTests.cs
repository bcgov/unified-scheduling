using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.UserManagement;
using Unified.UserManagement.Models;
using Unified.UserManagement.Services;

namespace Unified.Tests.UserManagement.Services;

public class AwayLocationServiceTests : IAsyncLifetime
{
    private UnifiedDbContext _dbContext = null!;
    private AwayLocationService _service = null!;

    private static readonly Guid UserId = Guid.NewGuid();
    private const int LocationId = 1;
    private const int SecondLocationId = 2;

    public ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new UnifiedDbContext(options);
        _service = new AwayLocationService(_dbContext);

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    private async Task SeedCoreEntitiesAsync()
    {
        _dbContext.Locations.AddRange(
            new Location
            {
                Id = LocationId,
                AgencyId = "LOC-001",
                Name = "Victoria",
                Timezone = "America/Vancouver",
            },
            new Location
            {
                Id = SecondLocationId,
                AgencyId = "LOC-002",
                Name = "Vancouver",
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

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<UserAwayLocation> SeedAwayLocationAsync(
        DateTimeOffset? expiryAtUtc = null,
        DateTimeOffset? startAtUtc = null,
        DateTimeOffset? endAtUtc = null,
        int locationId = LocationId
    )
    {
        var awayLocation = new UserAwayLocation
        {
            UserId = UserId,
            LocationId = locationId,
            StartAtUtc = startAtUtc ?? DateTimeOffset.UtcNow.AddDays(-5),
            EndAtUtc = endAtUtc ?? DateTimeOffset.UtcNow.AddDays(25),
            ExpiryAtUtc = expiryAtUtc,
            Timezone = "America/Vancouver",
        };

        _dbContext.UserAwayLocations.Add(awayLocation);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return awayLocation;
    }

    #region GetByUserIdAsync

    [Fact]
    public async Task GetByUserIdAsync_Should_Return_Active_Away_Locations()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        await SeedAwayLocationAsync();

        // Act
        var result = await _service.GetByUserIdAsync(UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        var dto = result.First();
        Assert.Equal(UserId, dto.UserId);
        Assert.Equal(LocationId, dto.LocationId);
        Assert.Equal("Victoria", dto.LocationName);
        Assert.Equal("America/Vancouver", dto.LocationTimezone);
        Assert.Null(dto.ExpiryAtUtc);
    }

    [Fact]
    public async Task GetByUserIdAsync_Should_Exclude_Expired_Away_Locations()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        await SeedAwayLocationAsync(expiryAtUtc: DateTimeOffset.UtcNow.AddDays(-1));

        // Act
        var result = await _service.GetByUserIdAsync(UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByUserIdAsync_Should_Return_Empty_When_No_Away_Locations()
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
    public async Task GetByUserIdAsync_Should_Return_Multiple_Active_Away_Locations()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        await SeedAwayLocationAsync(startAtUtc: DateTimeOffset.UtcNow.AddDays(-10));
        await SeedAwayLocationAsync(startAtUtc: DateTimeOffset.UtcNow.AddDays(-5), locationId: SecondLocationId);

        // Act
        var result = await _service.GetByUserIdAsync(UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
    }

    #endregion

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_Should_Create_And_Return_Away_Location()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        var request = new AwayLocationRequestDto
        {
            LocationId = LocationId,
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-06-30T00:00:00.000-07:00",
            Timezone = "America/Vancouver",
            Comment = "Training visit",
        };

        // Act
        var result = await _service.CreateAsync(UserId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(0, result.Id);
        Assert.Equal(UserId, result.UserId);
        Assert.Equal(LocationId, result.LocationId);
        Assert.Equal("Victoria", result.LocationName);
        Assert.Equal("America/Vancouver", result.LocationTimezone);
        Assert.Equal("America/Vancouver", result.Timezone);
        Assert.Equal("Training visit", result.Comment);
        Assert.Null(result.ExpiryAtUtc);
    }

    [Fact]
    public async Task CreateAsync_Should_Use_Request_Timezone_When_Provided()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        var request = new AwayLocationRequestDto
        {
            LocationId = LocationId,
            StartDateTime = "2026-01-10T00:00:00.000-07:00",
            EndDateTime = "2026-06-30T00:00:00.000-07:00",
            Timezone = "America/Edmonton",
        };

        // Act
        var result = await _service.CreateAsync(UserId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("America/Edmonton", result.Timezone);
    }

    [Fact]
    public async Task CreateAsync_Should_Fall_Back_To_Location_Timezone_When_Request_Timezone_Is_Null()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        var request = new AwayLocationRequestDto
        {
            LocationId = LocationId,
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-06-30T00:00:00.000-07:00",
            Timezone = null,
        };

        // Act
        var result = await _service.CreateAsync(UserId, request, TestContext.Current.CancellationToken);

        // Assert — falls back to location.Timezone = "America/Vancouver"
        Assert.Equal("America/Vancouver", result.Timezone);
    }

    [Fact]
    public async Task CreateAsync_Should_Persist_Away_Location_To_Database()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        var request = new AwayLocationRequestDto
        {
            LocationId = LocationId,
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-06-30T00:00:00.000-07:00",
        };

        // Act
        await _service.CreateAsync(UserId, request, TestContext.Current.CancellationToken);

        // Assert
        var saved = await _dbContext.UserAwayLocations.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(UserId, saved.UserId);
        Assert.Equal(LocationId, saved.LocationId);
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_User_Not_Found()
    {
        // Arrange — no seed
        var request = new AwayLocationRequestDto
        {
            LocationId = LocationId,
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-06-30T00:00:00.000-07:00",
        };

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CreateAsync(Guid.NewGuid(), request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_Location_Not_Found()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        var request = new AwayLocationRequestDto
        {
            LocationId = 999,
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-06-30T00:00:00.000-07:00",
        };

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CreateAsync(UserId, request, TestContext.Current.CancellationToken)
        );
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_Should_Update_And_Return_Away_Location()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        var seeded = await SeedAwayLocationAsync();
        var newStart = DateTimeOffset.Parse("2026-02-01T00:00:00.000-08:00").ToUniversalTime();

        var request = new AwayLocationRequestDto
        {
            LocationId = SecondLocationId,
            StartDateTime = "2026-02-01T00:00:00.000-08:00",
            EndDateTime = "2026-08-01T00:00:00.000-07:00",
            Comment = "Updated comment",
        };

        // Act
        var result = await _service.UpdateAsync(UserId, seeded.Id, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(seeded.Id, result.Id);
        Assert.Equal(SecondLocationId, result.LocationId);
        Assert.Equal("Vancouver", result.LocationName);
        Assert.Equal("Updated comment", result.Comment);
        Assert.Equal(newStart, result.StartAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_Should_Throw_When_Away_Location_Not_Found()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        var request = new AwayLocationRequestDto
        {
            LocationId = LocationId,
            StartDateTime = "2026-02-01T00:00:00.000-08:00",
            EndDateTime = "2026-08-01T00:00:00.000-07:00",
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
        var request = new AwayLocationRequestDto
        {
            LocationId = LocationId,
            StartDateTime = "2026-02-01T00:00:00.000-08:00",
            EndDateTime = "2026-08-01T00:00:00.000-07:00",
        };

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.UpdateAsync(Guid.NewGuid(), 1, request, TestContext.Current.CancellationToken)
        );
    }

    #endregion

    #region ExpireAsync

    [Fact]
    public async Task ExpireAsync_Should_Set_ExpiryAtUtc_And_Return()
    {
        // Arrange
        await SeedCoreEntitiesAsync();
        var seeded = await SeedAwayLocationAsync();

        var request = new ExpireAwayLocationRequestDto { AwayLocationId = seeded.Id, ExpiryReason = "ENTRYERR" };

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
        var seeded = await SeedAwayLocationAsync();

        var request = new ExpireAwayLocationRequestDto { AwayLocationId = seeded.Id, ExpiryReason = "  ENTRYERR  " };

        // Act
        var result = await _service.ExpireAsync(UserId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("ENTRYERR", result.ExpiryReason);
    }

    [Fact]
    public async Task ExpireAsync_Should_Throw_When_Away_Location_Not_Found()
    {
        // Arrange
        await SeedCoreEntitiesAsync();

        var request = new ExpireAwayLocationRequestDto { AwayLocationId = 9999, ExpiryReason = "ENTRYERR" };

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ExpireAsync(UserId, request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task ExpireAsync_Should_Throw_When_User_Not_Found()
    {
        // Arrange — no seed

        var request = new ExpireAwayLocationRequestDto { AwayLocationId = 1, ExpiryReason = "ENTRYERR" };

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ExpireAsync(Guid.NewGuid(), request, TestContext.Current.CancellationToken)
        );
    }

    #endregion
}
