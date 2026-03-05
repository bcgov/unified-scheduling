using Microsoft.EntityFrameworkCore;
using Unified.Auth.Data;
using Unified.Auth.Data.Entities;
using Unified.Auth.Models;
using Unified.Auth.Services;
using Xunit.Sdk;

namespace Unified.Tests.Features.Auth.Services;

public class UserServiceTests : IAsyncLifetime
{
    private AuthDbContext _dbContext = null!;
    private UserService _userService = null!;

    public ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AuthDbContext(options);
        _userService = new UserService(_dbContext);

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    private async Task SeedTestData()
    {
        var users = new[]
        {
            new UserEntity
            {
                Id = Guid.NewGuid(),
                IdirName = "jsmith",
                IdirId = Guid.NewGuid(),
                IsEnabled = true,
                FirstName = "John",
                LastName = "Smith",
                Email = "john.smith@example.com",
                HomeLocationId = 1,
                LastLogin = DateTimeOffset.UtcNow.AddDays(-1),
            },
            new UserEntity
            {
                Id = Guid.NewGuid(),
                IdirName = "jdoe",
                IdirId = Guid.NewGuid(),
                IsEnabled = true,
                FirstName = "Jane",
                LastName = "Doe",
                Email = "jane.doe@example.com",
                HomeLocationId = 2,
                LastLogin = DateTimeOffset.UtcNow.AddDays(-2),
            },
            new UserEntity
            {
                Id = Guid.NewGuid(),
                IdirName = "bjones",
                IdirId = Guid.NewGuid(),
                IsEnabled = false,
                FirstName = "Bob",
                LastName = "Jones",
                Email = "bob.jones@example.com",
                HomeLocationId = 1,
                LastLogin = DateTimeOffset.UtcNow.AddDays(-3),
            },
            new UserEntity
            {
                Id = Guid.NewGuid(),
                IdirName = "ajohnson",
                IdirId = Guid.NewGuid(),
                IsEnabled = true,
                FirstName = "Alice",
                LastName = "Johnson",
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
    public async Task GetAllAsync_Should_Filter_By_FirstName_Only()
    {
        // Arrange
        await SeedTestData();
        var queryParams = new UserQueryParams { FirstName = "John" };

        // Act
        var result = await _userService.GetAllAsync(queryParams, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal("John", result.First().FirstName);
        Assert.Equal("Smith", result.First().LastName);
    }

    [Fact]
    public async Task GetAllAsync_Should_Filter_By_LastName_Only()
    {
        // Arrange
        await SeedTestData();
        var queryParams = new UserQueryParams { LastName = "Doe" };

        // Act
        var result = await _userService.GetAllAsync(queryParams, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal("Jane", result.First().FirstName);
        Assert.Equal("Doe", result.First().LastName);
    }

    [Fact]
    public async Task GetAllAsync_Should_Filter_By_FirstName_Or_LastName()
    {
        // Arrange
        await SeedTestData();
        var queryParams = new UserQueryParams { FirstName = "John", LastName = "Doe" };

        // Act
        var result = await _userService.GetAllAsync(queryParams, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, u => u.FirstName == "John" && u.LastName == "Smith");
        Assert.Contains(result, u => u.FirstName == "Jane" && u.LastName == "Doe");
    }

    [Fact]
    public async Task GetAllAsync_Should_Handle_Partial_FirstName_Match()
    {
        // Arrange
        await SeedTestData();
        var queryParams = new UserQueryParams { FirstName = "Jo" };

        // Act
        var result = await _userService.GetAllAsync(queryParams, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal("John", result.First().FirstName);
    }

    [Fact]
    public async Task GetAllAsync_Should_Handle_Partial_LastName_Match()
    {
        // Arrange
        await SeedTestData();
        var queryParams = new UserQueryParams { LastName = "on" }; // Matches "Jones" and "Johnson"

        // Act
        var result = await _userService.GetAllAsync(queryParams, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, u => u.LastName == "Jones");
        Assert.Contains(result, u => u.LastName == "Johnson");
    }

    [Fact]
    public async Task GetAllAsync_Should_Return_Empty_When_No_Match()
    {
        // Arrange
        await SeedTestData();
        var queryParams = new UserQueryParams { FirstName = "NonExistent" };

        // Act
        var result = await _userService.GetAllAsync(queryParams, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_Should_Handle_Empty_String_Filters()
    {
        // Arrange
        await SeedTestData();
        var queryParams = new UserQueryParams { FirstName = "", LastName = "" };

        // Act
        var result = await _userService.GetAllAsync(queryParams, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(4, result.Count);
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
        var request = new CreateUserRequest(
            IdirName: "testuser",
            IdirId: Guid.NewGuid(),
            IsEnabled: true,
            FirstName: "Test",
            LastName: "User",
            Email: "test.user@example.com",
            HomeLocationId: 1
        );

        // Act
        var result = await _userService.CreateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("testuser", result.IdirName);
        Assert.Equal("Test", result.FirstName);
        Assert.Equal("User", result.LastName);
        Assert.Equal("test.user@example.com", result.Email);

        var userInDb = await _dbContext.Users.FindAsync([result.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(userInDb);
    }

    [Fact]
    public async Task CreateAsync_Should_Trim_String_Fields()
    {
        // Arrange
        var request = new CreateUserRequest(
            IdirName: "  testuser  ",
            IdirId: Guid.NewGuid(),
            IsEnabled: true,
            FirstName: "  Test  ",
            LastName: "  User  ",
            Email: "  test.user@example.com  ",
            HomeLocationId: 1
        );

        // Act
        var result = await _userService.CreateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("testuser", result.IdirName);
        Assert.Equal("Test", result.FirstName);
        Assert.Equal("User", result.LastName);
        Assert.Equal("test.user@example.com", result.Email);
    }

    [Fact]
    public async Task UpdateAsync_Should_Update_Existing_User()
    {
        // Arrange
        await SeedTestData();
        var existingUser = await _dbContext.Users.FirstAsync(TestContext.Current.CancellationToken);
        var request = new UpdateUserRequest(
            IsEnabled: false,
            FirstName: "Updated",
            LastName: "Name",
            Email: "updated@example.com",
            HomeLocationId: 5
        );

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
        var request = new UpdateUserRequest(
            IsEnabled: true,
            FirstName: "Test",
            LastName: "User",
            Email: "test@example.com",
            HomeLocationId: 1
        );

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
        var request = new UpdateUserRequest(
            IsEnabled: true,
            FirstName: "  Updated  ",
            LastName: "  Name  ",
            Email: "  updated@example.com  ",
            HomeLocationId: 1
        );

        // Act
        var result = await _userService.UpdateAsync(existingUser.Id, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated", result.FirstName);
        Assert.Equal("Name", result.LastName);
        Assert.Equal("updated@example.com", result.Email);
    }
}
