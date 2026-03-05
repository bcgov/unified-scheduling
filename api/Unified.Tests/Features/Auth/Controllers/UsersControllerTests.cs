using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unified.Auth.Controllers;
using Unified.Auth.Data;
using Unified.Auth.Data.Entities;
using Unified.Auth.Models;
using Unified.Auth.Services;

namespace Unified.Tests.Features.Auth.Controllers;

public class UsersControllerTests : IAsyncLifetime
{
    private AuthDbContext _dbContext = null!;
    private UsersController _controller = null!;
    private UserService _userService = null!;

    public ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AuthDbContext(options);
        _userService = new UserService(_dbContext);
        _controller = new UsersController(_userService);

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    private async Task<UserEntity> SeedSingleUser()
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            IdirName = "jsmith",
            IdirId = Guid.NewGuid(),
            IsEnabled = true,
            FirstName = "John",
            LastName = "Smith",
            Email = "john.smith@example.com",
            HomeLocationId = 1,
            LastLogin = DateTimeOffset.UtcNow,
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user;
    }

    private async Task SeedMultipleUsers()
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
                LastLogin = DateTimeOffset.UtcNow,
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
                LastLogin = DateTimeOffset.UtcNow,
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
                LastLogin = DateTimeOffset.UtcNow,
            },
        };

        _dbContext.Users.AddRange(users);
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Get_Should_Return_Ok_With_All_Users()
    {
        // Arrange
        await SeedMultipleUsers();

        // Act
        var result = await _controller.Get(null, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var users = Assert.IsAssignableFrom<IEnumerable<UserResponse>>(okResult.Value);
        Assert.Equal(3, users.Count());
    }

    [Fact]
    public async Task Get_Should_Return_Ok_With_Filtered_Users_By_FirstName()
    {
        // Arrange
        await SeedMultipleUsers();
        var queryParams = new UserQueryParams { FirstName = "John" };

        // Act
        var result = await _controller.Get(queryParams, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var users = Assert.IsAssignableFrom<IEnumerable<UserResponse>>(okResult.Value);
        Assert.Single(users);
        Assert.Equal("John", users.First().FirstName);
    }

    [Fact]
    public async Task Get_Should_Return_Ok_With_Filtered_Users_By_LastName()
    {
        // Arrange
        await SeedMultipleUsers();
        var queryParams = new UserQueryParams { LastName = "Doe" };

        // Act
        var result = await _controller.Get(queryParams, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var users = Assert.IsAssignableFrom<IEnumerable<UserResponse>>(okResult.Value);
        Assert.Single(users);
        Assert.Equal("Doe", users.First().LastName);
    }

    [Fact]
    public async Task Get_Should_Return_Ok_With_Filtered_Users_By_FirstName_Or_LastName()
    {
        // Arrange
        await SeedMultipleUsers();
        var queryParams = new UserQueryParams { FirstName = "John", LastName = "Doe" };

        // Act
        var result = await _controller.Get(queryParams, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var users = Assert.IsAssignableFrom<IEnumerable<UserResponse>>(okResult.Value).ToList();
        Assert.Equal(2, users.Count);
        Assert.Contains(users, u => u.FirstName == "John" && u.LastName == "Smith");
        Assert.Contains(users, u => u.FirstName == "Jane" && u.LastName == "Doe");
    }

    [Fact]
    public async Task Get_Should_Return_Empty_List_When_No_Users_Match()
    {
        // Arrange
        await SeedMultipleUsers();
        var queryParams = new UserQueryParams { FirstName = "NonExistent" };

        // Act
        var result = await _controller.Get(queryParams, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var users = Assert.IsAssignableFrom<IEnumerable<UserResponse>>(okResult.Value);
        Assert.Empty(users);
    }

    [Fact]
    public async Task GetById_Should_Return_Ok_With_User_When_Found()
    {
        // Arrange
        var seededUser = await SeedSingleUser();

        // Act
        var result = await _controller.GetById(seededUser.Id, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var user = Assert.IsType<UserResponse>(okResult.Value);
        Assert.Equal(seededUser.Id, user.Id);
        Assert.Equal(seededUser.FirstName, user.FirstName);
        Assert.Equal(seededUser.LastName, user.LastName);
    }

    [Fact]
    public async Task GetById_Should_Return_NotFound_When_User_Does_Not_Exist()
    {
        // Arrange
        await SeedSingleUser();

        // Act
        var result = await _controller.GetById(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Create_Should_Return_Created_With_User()
    {
        // Arrange
        var request = new CreateUserRequest(
            IdirName: "newuser",
            IdirId: Guid.NewGuid(),
            IsEnabled: true,
            FirstName: "New",
            LastName: "User",
            Email: "new.user@example.com",
            HomeLocationId: 1
        );

        // Act
        var result = await _controller.Create(request, CancellationToken.None);

        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result.Result);
        var user = Assert.IsType<UserResponse>(createdResult.Value);
        Assert.Equal("New", user.FirstName);
        Assert.Equal("User", user.LastName);
        Assert.Equal("new.user@example.com", user.Email);
        Assert.Equal($"/api/users/{user.Id}", createdResult.Location);
    }

    [Fact]
    public async Task Create_Should_Persist_User_To_Database()
    {
        // Arrange
        var request = new CreateUserRequest(
            IdirName: "newuser",
            IdirId: Guid.NewGuid(),
            IsEnabled: true,
            FirstName: "New",
            LastName: "User",
            Email: "new.user@example.com",
            HomeLocationId: 1
        );

        // Act
        var result = await _controller.Create(request, CancellationToken.None);

        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result.Result);
        var user = Assert.IsType<UserResponse>(createdResult.Value);

        var userInDb = await _dbContext.Users.FindAsync(user.Id);
        Assert.NotNull(userInDb);
        Assert.Equal("New", userInDb.FirstName);
    }

    [Fact]
    public async Task Update_Should_Return_Ok_With_Updated_User()
    {
        // Arrange
        var seededUser = await SeedSingleUser();
        var request = new UpdateUserRequest(
            IsEnabled: false,
            FirstName: "Updated",
            LastName: "Name",
            Email: "updated@example.com",
            HomeLocationId: 5
        );

        // Act
        var result = await _controller.Update(seededUser.Id, request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var user = Assert.IsType<UserResponse>(okResult.Value);
        Assert.Equal(seededUser.Id, user.Id);
        Assert.False(user.IsEnabled);
        Assert.Equal("Updated", user.FirstName);
        Assert.Equal("Name", user.LastName);
        Assert.Equal("updated@example.com", user.Email);
        Assert.Equal(5, user.HomeLocationId);
    }

    [Fact]
    public async Task Update_Should_Return_NotFound_When_User_Does_Not_Exist()
    {
        // Arrange
        await SeedSingleUser();
        var request = new UpdateUserRequest(
            IsEnabled: true,
            FirstName: "Updated",
            LastName: "Name",
            Email: "updated@example.com",
            HomeLocationId: 1
        );

        // Act
        var result = await _controller.Update(Guid.NewGuid(), request, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }
}
