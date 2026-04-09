using Microsoft.EntityFrameworkCore;
using Unified.Db;
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
            )
        );
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    private async Task SeedTestData()
    {
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
            IdirName = "testuser",
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
        Assert.Equal("Test", result.FirstName);
        Assert.Equal("User", result.LastName);
        Assert.Equal("test.user@example.com", result.Email);
        Assert.Equal(Gender.Other, result.Gender);
        Assert.Equal("Deputy Sheriff", result.Rank);

        var userInDb = await _dbContext.Users.FindAsync([result.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(userInDb);
    }

    [Fact]
    public async Task CreateAsync_Should_Trim_String_Fields()
    {
        // Arrange
        var request = new UserRequestDto
        {
            IdirName = "  testuser  ",
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
    }

    [Fact]
    public async Task UpdateAsync_Should_Update_Existing_User()
    {
        // Arrange
        await SeedTestData();
        var existingUser = await _dbContext.Users.FirstAsync(TestContext.Current.CancellationToken);
        var request = new UserRequestDto
        {
            IdirName = "updateduser",
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
        Assert.Equal("Updated", result.FirstName);
        Assert.Equal("Name", result.LastName);
        Assert.Equal("updated@example.com", result.Email);
        Assert.Equal(5, result.HomeLocationId);
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
            IdirName = "  updateduser  ",
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
        Assert.Equal("Updated", result.FirstName);
        Assert.Equal("Name", result.LastName);
        Assert.Equal("updated@example.com", result.Email);
    }
}
