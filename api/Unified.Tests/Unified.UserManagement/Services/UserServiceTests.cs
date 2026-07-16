using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Common.Helpers.Extensions;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.UserManagement;
using Unified.FeatureFlags;
using Unified.UserManagement.Models;
using Unified.UserManagement.Services;

namespace Unified.Tests.UserManagement.Services;

public class UserServiceTests : IAsyncLifetime
{
    private UnifiedDbContext _dbContext = null!;
    private UserService _userService = null!;

    private sealed class TestFeatureFlags(FeatureFlags.FeatureFlags current) : IFeatureFlags
    {
        public FeatureFlags.FeatureFlags Current { get; } = current;
    }

    public ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new UnifiedDbContext(options);
        _userService = CreateUserService(userBadgeNumberEnabled: false);

        return ValueTask.CompletedTask;
    }

    private UserService CreateUserService(bool userBadgeNumberEnabled)
    {
        return new UserService(
            _dbContext,
            new TestFeatureFlags(
                new FeatureFlags.FeatureFlags { StatsModule = true, UserBadgeNumber = userBadgeNumberEnabled }
            ),
            NullLogger<UserService>.Instance
        );
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    private async Task SeedTestData()
    {
        var locations = new[]
        {
            new Location
            {
                Id = 1,
                AgencyId = "LOC-001",
                Name = "Vancouver Court",
                Timezone = "America/Vancouver",
            },
            new Location
            {
                Id = 2,
                AgencyId = "LOC-002",
                Name = "Surrey Court",
                Timezone = "America/Vancouver",
            },
        };

        var users = new[]
        {
            new User
            {
                Id = Guid.NewGuid(),
                IdirName = "jsmith",
                IdirId = Guid.NewGuid(),
                IsEnabled = true,
                FirstName = "John",
                LastName = "Smith",
                Gender = Gender.Male,
                BadgeNumber = "BADGE-001",
                Email = "john.smith@example.com",
                HomeLocationId = 1,
                LastLogin = DateTimeOffset.UtcNow.AddDays(-1),
            },
            new User
            {
                Id = Guid.NewGuid(),
                IdirName = "jdoe",
                IdirId = Guid.NewGuid(),
                IsEnabled = true,
                FirstName = "Jane",
                LastName = "Doe",
                Gender = Gender.Female,
                BadgeNumber = "BADGE-002",
                Email = "jane.doe@example.com",
                HomeLocationId = 2,
                LastLogin = DateTimeOffset.UtcNow.AddDays(-2),
            },
            new User
            {
                Id = Guid.NewGuid(),
                IdirName = "bjones",
                IdirId = Guid.NewGuid(),
                IsEnabled = false,
                FirstName = "Bob",
                LastName = "Jones",
                Gender = Gender.Male,
                BadgeNumber = "BADGE-003",
                Email = "bob.jones@example.com",
                HomeLocationId = 1,
                LastLogin = DateTimeOffset.UtcNow.AddDays(-3),
            },
            new User
            {
                Id = Guid.NewGuid(),
                IdirName = "ajohnson",
                IdirId = Guid.NewGuid(),
                IsEnabled = true,
                FirstName = "Alice",
                LastName = "Johnson",
                Gender = Gender.Female,
                BadgeNumber = "BADGE-004",
                Email = "alice.johnson@example.com",
                HomeLocationId = null,
                LastLogin = DateTimeOffset.UtcNow.AddDays(-4),
            },
        };

        _dbContext.Locations.AddRange(locations);
        _dbContext.Users.AddRange(users);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetAllAsync_Should_Return_All_Users_When_No_QueryParams()
    {
        // Arrange
        await SeedTestData();

        // Act
        var result = await _userService.GetAllAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Collection(
            result,
            user => Assert.Equal("Doe", user.LastName),
            user => Assert.Equal("Johnson", user.LastName),
            user => Assert.Equal("Jones", user.LastName),
            user => Assert.Equal("Smith", user.LastName)
        );
    }

    [Fact]
    public async Task GetAllAsync_Should_Filter_By_Search_Matching_FirstName()
    {
        // Arrange
        await SeedTestData();
        var queryParams = new UserQueryParams { Search = "John" };

        // Act
        var result = await _userService.GetAllAsync(queryParams, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, u => u.FirstName == "John" && u.LastName == "Smith");
        Assert.Contains(result, u => u.FirstName == "Alice" && u.LastName == "Johnson");
    }

    [Fact]
    public async Task GetAllAsync_Should_Filter_By_Search_Matching_LastName()
    {
        // Arrange
        await SeedTestData();
        var queryParams = new UserQueryParams { Search = "Doe" };

        // Act
        var result = await _userService.GetAllAsync(queryParams, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal("Jane", result.First().FirstName);
        Assert.Equal("Doe", result.First().LastName);
    }

    [Fact]
    public async Task GetAllAsync_Should_Handle_Partial_Search_Match_On_FirstName()
    {
        // Arrange
        await SeedTestData();
        var queryParams = new UserQueryParams { Search = "Jo" };

        // Act
        var result = await _userService.GetAllAsync(queryParams, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(result, u => u.FirstName == "John" && u.LastName == "Smith");
        Assert.Contains(result, u => u.FirstName == "Alice" && u.LastName == "Johnson");
        Assert.Contains(result, u => u.FirstName == "Bob" && u.LastName == "Jones");
    }

    [Fact]
    public async Task GetAllAsync_Should_Return_Empty_When_No_Match()
    {
        // Arrange
        await SeedTestData();
        var queryParams = new UserQueryParams { Search = "NonExistent" };

        // Act
        var result = await _userService.GetAllAsync(queryParams, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_Should_Handle_Empty_Search_Filter()
    {
        // Arrange
        await SeedTestData();
        var queryParams = new UserQueryParams { Search = "" };

        // Act
        var result = await _userService.GetAllAsync(queryParams, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public async Task GetAllAsync_Should_Search_BadgeNumber_When_FeatureFlag_Enabled()
    {
        // Arrange
        await SeedTestData();
        var userService = CreateUserService(userBadgeNumberEnabled: true);
        var queryParams = new UserQueryParams { Search = "BADGE-003" };

        // Act
        var result = await userService.GetAllAsync(queryParams, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal("Bob", result.First().FirstName);
        Assert.Equal("Jones", result.First().LastName);
    }

    [Fact]
    public async Task GetAllAsync_Should_Not_Search_BadgeNumber_When_FeatureFlag_Disabled()
    {
        // Arrange
        await SeedTestData();
        var userService = CreateUserService(userBadgeNumberEnabled: false);
        var queryParams = new UserQueryParams { Search = "BADGE-003" };

        // Act
        var result = await userService.GetAllAsync(queryParams, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_User_When_Found()
    {
        // Arrange
        await SeedTestData();
        var existingUser = await _dbContext.Users.FirstAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _userService.GetByIdAsync(existingUser.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(existingUser.Id, result.Id);
        Assert.Equal(existingUser.FirstName, result.FirstName);
        Assert.Equal(existingUser.LastName, result.LastName);
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_Null_When_Not_Found()
    {
        // Arrange
        await SeedTestData();

        // Act
        var result = await _userService.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_Should_Create_New_User()
    {
        // Arrange
        var request = new UserRequestDto
        {
            IdirName = "TESTUSER",
            IsEnabled = true,
            FirstName = "Test",
            LastName = "User",
            Email = "test.user@example.com",
            Gender = Gender.Other,
            Rank = "Deputy Sheriff",
            BadgeNumber = "BADGE-NEW",
            HomeLocationId = 1,
        };

        // Act
        var result = await _userService.CreateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("testuser", result.IdirName);
        Assert.Null(result.IdirId);
        Assert.Equal("Test", result.FirstName);
        Assert.Equal("User", result.LastName);
        Assert.Equal("test.user@example.com", result.Email);
        Assert.Equal(Gender.Other, result.Gender);
        Assert.Equal("Deputy Sheriff", result.Rank);
        Assert.True(result.PendingRegistration);

        var userInDb = await _dbContext.Users.FindAsync([result.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(userInDb);
        Assert.Equal("testuser", userInDb.IdirName);
        Assert.Null(userInDb.IdirId);
        Assert.Null(userInDb.LastLogin);
        Assert.True(userInDb.PendingRegistration);
    }

    [Fact]
    public async Task CreateAsync_Should_Mark_User_As_PendingRegistration_When_IdirId_Is_Missing()
    {
        // Arrange
        var request = new UserRequestDto
        {
            IdirName = "pendinguser",
            IsEnabled = true,
            FirstName = "Pending",
            LastName = "User",
            Email = "pending.user@example.com",
            Gender = Gender.Other,
            Rank = "Deputy Sheriff",
            BadgeNumber = "BADGE-PENDING",
            HomeLocationId = 1,
        };

        // Act
        var result = await _userService.CreateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var userInDb = await _dbContext.Users.FindAsync([result.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(userInDb);
        Assert.True(userInDb.PendingRegistration);
        Assert.Null(userInDb.IdirId);
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_IdirName_Normalizes_To_Existing_User()
    {
        // Arrange
        await SeedTestData();
        var request = new UserRequestDto
        {
            IdirName = "  JSMITH  ",
            IsEnabled = true,
            FirstName = "Duplicate",
            LastName = "User",
            Email = "duplicate.user@example.com",
            Gender = Gender.Other,
            Rank = "Deputy Sheriff",
            BadgeNumber = "BADGE-DUPLICATE",
            HomeLocationId = 1,
        };

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _userService.CreateAsync(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task UpdateAsync_Should_Throw_When_IdirName_Normalizes_To_Another_User()
    {
        await SeedTestData();
        var existingUser = await _dbContext.Users.SingleAsync(
            u => u.IdirName == "jdoe",
            TestContext.Current.CancellationToken
        );
        var request = new UserRequestDto
        {
            IdirName = "  JSMITH ",
            IsEnabled = true,
            FirstName = existingUser.FirstName,
            LastName = existingUser.LastName,
            Email = existingUser.Email,
            Gender = existingUser.Gender,
            Rank = existingUser.Rank,
            BadgeNumber = existingUser.BadgeNumber,
            HomeLocationId = existingUser.HomeLocationId ?? 1,
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _userService.UpdateAsync(existingUser.Id, request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task CreateAsync_Should_Trim_String_Fields()
    {
        // Arrange
        var request = new UserRequestDto
        {
            IdirName = "  TestUser  ",
            IsEnabled = true,
            FirstName = "  Test  ",
            LastName = "  User  ",
            Email = "  test.user@example.com  ",
            Gender = Gender.Female,
            Rank = "  Sergeant  ",
            BadgeNumber = "  BADGE-TRIM  ",
            HomeLocationId = 1,
        };

        // Act
        var result = await _userService.CreateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("testuser", result.IdirName);
        Assert.Equal("Test", result.FirstName);
        Assert.Equal("User", result.LastName);
        Assert.Equal("test.user@example.com", result.Email);
        Assert.Equal(Gender.Female, result.Gender);
        Assert.Equal("Sergeant", result.Rank);

        var userInDb = await _dbContext.Users.FindAsync([result.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(userInDb);
        Assert.Equal("testuser", userInDb.IdirName);
    }

    [Fact]
    public async Task UpdateAsync_Should_Update_Existing_User()
    {
        // Arrange
        await SeedTestData();
        var existingUser = await _dbContext.Users.FirstAsync(TestContext.Current.CancellationToken);
        var originalLastLogin = existingUser.LastLogin;
        var originalIdirId = existingUser.IdirId;
        var request = new UserRequestDto
        {
            IdirName = "UpdatedUser ",
            IsEnabled = false,
            FirstName = "Updated",
            LastName = "Name",
            Email = "updated@example.com",
            Gender = Gender.Male,
            Rank = "Sergeant",
            BadgeNumber = "BADGE-005",
            HomeLocationId = 5,
        };

        // Act
        var result = await _userService.UpdateAsync(existingUser.Id, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(existingUser.Id, result.Id);
        Assert.False(result.IsEnabled);
        Assert.Equal("updateduser", result.IdirName);
        Assert.Equal("Updated", result.FirstName);
        Assert.Equal("Name", result.LastName);
        Assert.Equal(originalIdirId, result.IdirId);
        Assert.Equal("updated@example.com", result.Email);
        Assert.Equal(5, result.HomeLocationId);

        var userInDb = await _dbContext.Users.FindAsync([existingUser.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(userInDb);
        Assert.Equal("updateduser", userInDb.IdirName);
        Assert.Equal(originalIdirId, userInDb.IdirId);
        Assert.Equal(originalLastLogin, userInDb.LastLogin);
    }

    [Fact]
    public async Task UpdateAsync_Should_Return_Null_When_User_Not_Found()
    {
        // Arrange
        await SeedTestData();
        var request = new UserRequestDto
        {
            IdirName = "testuser",
            IsEnabled = true,
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            Gender = Gender.Female,
            Rank = "Deputy Sheriff",
            BadgeNumber = "BADGE-001",
            HomeLocationId = 1,
        };

        // Act
        var result = await _userService.UpdateAsync(Guid.NewGuid(), request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_Should_Trim_String_Fields()
    {
        // Arrange
        await SeedTestData();
        var existingUser = await _dbContext.Users.FirstAsync(TestContext.Current.CancellationToken);
        var request = new UserRequestDto
        {
            IdirName = "  UpdatedUser  ",
            IsEnabled = true,
            FirstName = "  Updated  ",
            LastName = "  Name  ",
            Email = "  updated@example.com  ",
            Gender = Gender.Other,
            Rank = "  Deputy Sheriff  ",
            BadgeNumber = "  BADGE-001  ",
            HomeLocationId = 1,
        };

        // Act
        var result = await _userService.UpdateAsync(existingUser.Id, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("updateduser", result.IdirName);
        Assert.Equal("Updated", result.FirstName);
        Assert.Equal("Name", result.LastName);
        Assert.Equal("updated@example.com", result.Email);

        var userInDb = await _dbContext.Users.FindAsync([existingUser.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(userInDb);
        Assert.Equal("updateduser", userInDb.IdirName);
    }

    [Fact]
    public async Task UpdateAsync_Should_Preserve_Existing_IdirId_And_PendingRegistration_When_Request_Omits_IdirId()
    {
        // Arrange
        await SeedTestData();
        var existingUser = await _dbContext.Users.FirstAsync(TestContext.Current.CancellationToken);
        existingUser.PendingRegistration = true;
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new UserRequestDto
        {
            IdirName = "updateduser",
            IsEnabled = true,
            FirstName = "Updated",
            LastName = "User",
            Email = "updated.user@example.com",
            Gender = Gender.Other,
            Rank = "Deputy Sheriff",
            BadgeNumber = "BADGE-001",
            HomeLocationId = 1,
        };

        // Act
        var result = await _userService.UpdateAsync(existingUser.Id, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        var userInDb = await _dbContext.Users.FindAsync([existingUser.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(userInDb);
        Assert.Equal(existingUser.IdirId, userInDb.IdirId);
        Assert.True(userInDb.PendingRegistration);
    }

    [Fact]
    public async Task AssignRoleAsync_Should_Create_UserRole_When_User_And_Role_Exist()
    {
        // Arrange
        await SeedTestData();
        var user = await _dbContext.Users.FirstAsync(TestContext.Current.CancellationToken);
        _dbContext.Roles.Add(
            new Role
            {
                Id = 100,
                Name = "Supervisor",
                Description = "Supervisor role",
            }
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Frontend sends date strings only (yyyy-MM-dd format).
        var requestEffectiveDate = "2026-01-10";
        var requestExpiryDate = "2026-01-20";
        // DB stores UTC instants.
        var expectedStoredEffectiveDateUtc = new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.Zero);
        var expectedStoredExpiryDateUtc = new DateTimeOffset(2026, 1, 21, 7, 59, 59, 999, TimeSpan.Zero);
        // API response should be converted back to user's home-location timezone.
        var expectedResponseEffectiveDate = new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.FromHours(-8));
        var expectedResponseExpiryDate = new DateTimeOffset(2026, 1, 20, 23, 59, 59, 999, TimeSpan.FromHours(-8));
        var request = new AssignUserRoleRequestDto
        {
            RoleId = 100,
            EffectiveDate = requestEffectiveDate,
            ExpiryDate = requestExpiryDate,
        };

        // Act
        var result = await _userService.AssignRoleAsync(user.Id, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(100, result.RoleId);
        Assert.Equal(expectedResponseEffectiveDate, result.EffectiveDate);
        Assert.Equal(expectedResponseExpiryDate, result.ExpiryDate);
        Assert.Null(result.ExpiryReason);

        var userRole = await _dbContext.UserRoles.SingleAsync(
            x => x.UserId == user.Id && x.RoleId == 100,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(expectedStoredEffectiveDateUtc, userRole.EffectiveDate);
        Assert.Equal(expectedStoredExpiryDateUtc, userRole.ExpiryDate);
        Assert.Null(userRole.ExpiryReason);
    }

    [Fact]
    public async Task AssignRoleAsync_Should_Update_Existing_UserRole_And_Clear_ExpiryReason()
    {
        // Arrange
        await SeedTestData();
        var user = await _dbContext.Users.FirstAsync(TestContext.Current.CancellationToken);
        _dbContext.Roles.Add(
            new Role
            {
                Id = 101,
                Name = "Court Lead",
                Description = "Court Lead role",
            }
        );

        var existingEffectiveDate = DateTimeOffset.UtcNow.AddDays(-20);
        var existingExpiryDate = DateTimeOffset.UtcNow.AddDays(10);
        _dbContext.UserRoles.Add(
            new UserRole
            {
                UserId = user.Id,
                RoleId = 101,
                EffectiveDate = existingEffectiveDate,
                ExpiryDate = existingExpiryDate,
                ExpiryReason = "PERSONAL",
            }
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Frontend sends date strings only (yyyy-MM-dd format).
        var updatedRequestEffectiveDate = "2026-02-10";
        var updatedRequestExpiryDate = "2026-02-15";
        // DB stores UTC instants.
        var expectedStoredUpdatedEffectiveDateUtc = new DateTimeOffset(2026, 2, 10, 8, 0, 0, TimeSpan.Zero);
        var expectedStoredUpdatedExpiryDateUtc = new DateTimeOffset(2026, 2, 16, 7, 59, 59, 999, TimeSpan.Zero);
        // API response should be converted back to user's home-location timezone.
        var expectedResponseUpdatedEffectiveDate = new DateTimeOffset(2026, 2, 10, 0, 0, 0, TimeSpan.FromHours(-8));
        var expectedResponseUpdatedExpiryDate = new DateTimeOffset(
            2026,
            2,
            15,
            23,
            59,
            59,
            999,
            TimeSpan.FromHours(-8)
        );
        var request = new AssignUserRoleRequestDto
        {
            RoleId = 101,
            EffectiveDate = updatedRequestEffectiveDate,
            ExpiryDate = updatedRequestExpiryDate,
        };

        // Act
        var result = await _userService.AssignRoleAsync(user.Id, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedResponseUpdatedEffectiveDate, result.EffectiveDate);
        Assert.Equal(expectedResponseUpdatedExpiryDate, result.ExpiryDate);
        Assert.Null(result.ExpiryReason);

        var userRole = await _dbContext.UserRoles.SingleAsync(
            x => x.UserId == user.Id && x.RoleId == 101,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(expectedStoredUpdatedEffectiveDateUtc, userRole.EffectiveDate);
        Assert.Equal(expectedStoredUpdatedExpiryDateUtc, userRole.ExpiryDate);
        Assert.Null(userRole.ExpiryReason);
    }

    [Fact]
    public async Task AssignRoleAsync_Should_Throw_When_User_Does_Not_Exist()
    {
        // Arrange
        _dbContext.Roles.Add(
            new Role
            {
                Id = 100,
                Name = "Supervisor",
                Description = "Supervisor role",
            }
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _userService.AssignRoleAsync(
                Guid.NewGuid(),
                new AssignUserRoleRequestDto { RoleId = 100, EffectiveDate = "2026-01-10" },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task AssignRoleAsync_Should_Throw_When_Role_Does_Not_Exist()
    {
        // Arrange
        await SeedTestData();
        var user = await _dbContext.Users.FirstAsync(TestContext.Current.CancellationToken);

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _userService.AssignRoleAsync(
                user.Id,
                new AssignUserRoleRequestDto { RoleId = 9999, EffectiveDate = "2026-01-10" },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task GetRolesAsync_Should_Return_Assigned_Roles_For_User()
    {
        // Arrange
        await SeedTestData();
        var user = await _dbContext.Users.FirstAsync(TestContext.Current.CancellationToken);
        _dbContext.Roles.Add(
            new Role
            {
                Id = 200,
                Name = "Court Lead",
                Description = "Court Lead role",
            }
        );
        _dbContext.UserRoles.Add(
            new UserRole
            {
                UserId = user.Id,
                RoleId = 200,
                EffectiveDate = new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.Zero),
                ExpiryDate = new DateTimeOffset(2026, 1, 21, 7, 59, 59, 999, TimeSpan.Zero),
            }
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _userService.GetRolesAsync(user.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        var role = result.Single();
        Assert.Equal(user.Id, role.UserId);
        Assert.Equal(200, role.RoleId);
        Assert.Equal(new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.FromHours(-8)), role.EffectiveDate);
        Assert.Equal(new DateTimeOffset(2026, 1, 20, 23, 59, 59, 999, TimeSpan.FromHours(-8)), role.ExpiryDate);
    }

    [Fact]
    public async Task GetRolesAsync_Should_Throw_When_User_Does_Not_Exist()
    {
        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _userService.GetRolesAsync(Guid.NewGuid(), TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task ExpireRoleAsync_Should_Expire_UserRole_When_Assignment_Exists()
    {
        // Arrange
        await SeedTestData();
        var user = await _dbContext.Users.FirstAsync(TestContext.Current.CancellationToken);
        _dbContext.Roles.Add(
            new Role
            {
                Id = 201,
                Name = "Court Lead",
                Description = "Court Lead role",
            }
        );
        _dbContext.UserRoles.Add(
            new UserRole
            {
                UserId = user.Id,
                RoleId = 201,
                EffectiveDate = DateTimeOffset.UtcNow.AddDays(-5),
                ExpiryDate = null,
                ExpiryReason = null,
            }
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var beforeExpire = DateTimeOffset.UtcNow;
        var request = new ExpireUserRoleRequestDto { RoleId = 201, ExpiryReason = "ENTRYERR" };

        // Act
        var result = await _userService.ExpireRoleAsync(user.Id, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(201, result.RoleId);
        Assert.NotNull(result.ExpiryDate);
        Assert.Equal("ENTRYERR", result.ExpiryReason);

        var userRole = await _dbContext.UserRoles.SingleAsync(
            x => x.UserId == user.Id && x.RoleId == 201,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(userRole.ExpiryDate);
        Assert.True(userRole.ExpiryDate >= beforeExpire);
        Assert.Equal("ENTRYERR", userRole.ExpiryReason);

        const string timezoneId = "America/Vancouver";
        Assert.Equal(userRole.EffectiveDate.ToTimeZone(timezoneId), result.EffectiveDate);
        Assert.Equal(userRole.ExpiryDate?.ToTimeZone(timezoneId), result.ExpiryDate);
    }

    [Fact]
    public async Task ExpireRoleAsync_Should_Throw_When_User_Does_Not_Exist()
    {
        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _userService.ExpireRoleAsync(
                Guid.NewGuid(),
                new ExpireUserRoleRequestDto { RoleId = 100, ExpiryReason = "PERSONAL" },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task ExpireRoleAsync_Should_Throw_When_Role_Assignment_Does_Not_Exist()
    {
        // Arrange
        await SeedTestData();
        var user = await _dbContext.Users.FirstAsync(TestContext.Current.CancellationToken);

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _userService.ExpireRoleAsync(
                user.Id,
                new ExpireUserRoleRequestDto { RoleId = 9999, ExpiryReason = "PERSONAL" },
                TestContext.Current.CancellationToken
            )
        );
    }

    // --- Photo tests ---

    [Fact]
    public async Task GetPhotoAsync_Should_Return_Photo_When_User_Has_Photo()
    {
        // Arrange
        var photoBytes = "fake-image-data"u8.ToArray();
        var user = new User
        {
            Id = Guid.NewGuid(),
            IdirName = "photouser",
            IsEnabled = true,
            FirstName = "Photo",
            LastName = "User",
            Email = "photo@example.com",
            Gender = Gender.Other,
            Photo = photoBytes,
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _userService.GetPhotoAsync(user.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(photoBytes, result);
    }

    [Fact]
    public async Task GetPhotoAsync_Should_Return_Null_When_User_Has_No_Photo()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            IdirName = "nophotouser",
            IsEnabled = true,
            FirstName = "No",
            LastName = "Photo",
            Email = "nophoto@example.com",
            Gender = Gender.Other,
            Photo = null,
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _userService.GetPhotoAsync(user.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPhotoAsync_Should_Return_Null_When_User_Does_Not_Exist()
    {
        // Act
        var result = await _userService.GetPhotoAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UploadPhotoAsync_Should_Store_Photo_And_Return_Updated_User()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            IdirName = "uploaduser",
            IsEnabled = true,
            FirstName = "Upload",
            LastName = "User",
            Email = "upload@example.com",
            Gender = Gender.Other,
            Photo = null,
            LastPhotoUpdate = null,
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var photoBytes = "new-photo-bytes"u8.ToArray();
        var beforeUpload = DateTimeOffset.UtcNow;

        // Act
        var result = await _userService.UploadPhotoAsync(user.Id, photoBytes, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.NotNull(result.LastPhotoUpdate);
        Assert.True(result.LastPhotoUpdate >= beforeUpload);

        var persisted = await _dbContext.Users.FindAsync([user.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(persisted);
        Assert.Equal(photoBytes, persisted!.Photo);
        Assert.NotNull(persisted.LastPhotoUpdate);
    }

    [Fact]
    public async Task UploadPhotoAsync_Should_Replace_Existing_Photo()
    {
        // Arrange
        var originalPhoto = "original-photo"u8.ToArray();
        var user = new User
        {
            Id = Guid.NewGuid(),
            IdirName = "replaceuser",
            IsEnabled = true,
            FirstName = "Replace",
            LastName = "User",
            Email = "replace@example.com",
            Gender = Gender.Other,
            Photo = originalPhoto,
            LastPhotoUpdate = DateTimeOffset.UtcNow.AddDays(-1),
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var newPhoto = "replaced-photo"u8.ToArray();
        var beforeUpload = DateTimeOffset.UtcNow;

        // Act
        var result = await _userService.UploadPhotoAsync(user.Id, newPhoto, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.LastPhotoUpdate >= beforeUpload);

        var persisted = await _dbContext.Users.FindAsync([user.Id], TestContext.Current.CancellationToken);
        Assert.Equal(newPhoto, persisted!.Photo);
    }

    [Fact]
    public async Task UploadPhotoAsync_Should_Return_Null_When_User_Does_Not_Exist()
    {
        // Act
        var result = await _userService.UploadPhotoAsync(
            Guid.NewGuid(),
            "photo"u8.ToArray(),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }
}
