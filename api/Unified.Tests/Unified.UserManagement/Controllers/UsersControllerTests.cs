using Microsoft.AspNetCore.Mvc;
using Unified.Db.Models.UserManagement;
using Unified.UserManagement.Controllers;
using Unified.UserManagement.Models;
using Unified.UserManagement.Services;
using Unified.UserManagement.Validators;

namespace Unified.Tests.UserManagement.Controllers;

public class UsersControllerTests
{
    [Fact]
    public async Task Get_Should_Return_Ok_With_Users()
    {
        // Arrange
        var expectedUsers = new List<UserResponse> { CreateUserResponse("John", "Smith") };
        var fakeService = new FakeUserService { GetAllResult = expectedUsers };
        var controller = new UsersController(fakeService, new UserRequestValidator());
        var queryParams = new UserQueryParams { Search = "John" };

        // Act
        var result = await controller.Get(queryParams, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var users = Assert.IsAssignableFrom<IEnumerable<UserResponse>>(okResult.Value);
        Assert.Single(users);
        Assert.Same(queryParams, fakeService.LastQueryParams);
    }

    [Fact]
    public async Task GetById_Should_Return_Ok_When_Found()
    {
        // Arrange
        var expectedUser = CreateUserResponse("Jane", "Doe");
        var fakeService = new FakeUserService { GetByIdResult = expectedUser };
        var controller = new UsersController(fakeService, new UserRequestValidator());

        // Act
        var result = await controller.GetById(expectedUser.Id, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var user = Assert.IsType<UserResponse>(okResult.Value);
        Assert.Equal(expectedUser.Id, user.Id);
        Assert.Equal(expectedUser.FirstName, user.FirstName);
        Assert.Equal(expectedUser.LastName, user.LastName);
    }

    [Fact]
    public async Task GetById_Should_Return_NotFound_When_Missing()
    {
        // Arrange
        var fakeService = new FakeUserService { GetByIdResult = null };
        var controller = new UsersController(fakeService, new UserRequestValidator());

        // Act
        var result = await controller.GetById(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Create_Should_Return_Created_With_Location_And_Body()
    {
        // Arrange
        var createdUser = CreateUserResponse("New", "User");
        var fakeService = new FakeUserService { CreateResult = createdUser };
        var controller = new UsersController(fakeService, new UserRequestValidator());
        var request = new UserRequestDto
        {
            IdirName = "newuser",
            IsEnabled = true,
            FirstName = "New",
            LastName = "User",
            Email = "new.user@example.com",
            Gender = Gender.Male,
            Rank = "Sergeant",
            BadgeNumber = "BADGE-NEW",
            HomeLocationId = 1,
        };

        // Act
        var result = await controller.Create(request, TestContext.Current.CancellationToken);

        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result.Result);
        var user = Assert.IsType<UserResponse>(createdResult.Value);
        Assert.Equal($"/api/users/{createdUser.Id}", createdResult.Location);
        Assert.Equal(createdUser.Id, user.Id);
        Assert.Equal("New", user.FirstName);
        Assert.Equal("User", user.LastName);
    }

    [Fact]
    public async Task Update_Should_Return_Ok_When_Found()
    {
        // Arrange
        var updatedUser = CreateUserResponse("Updated", "Name");
        var fakeService = new FakeUserService { UpdateResult = updatedUser };
        var controller = new UsersController(fakeService, new UserRequestValidator());
        var request = new UserRequestDto
        {
            IdirName = "updateduser",
            IsEnabled = false,
            FirstName = "Updated",
            LastName = "Name",
            Email = "updated@example.com",
            Gender = Gender.Female,
            Rank = "Sergeant",
            BadgeNumber = "BADGE-UPDATED",
            HomeLocationId = 2,
        };

        // Act
        var result = await controller.Update(updatedUser.Id, request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var user = Assert.IsType<UserResponse>(okResult.Value);
        Assert.Equal(updatedUser.Id, user.Id);
        Assert.Equal("Updated", user.FirstName);
        Assert.Equal("Name", user.LastName);
    }

    [Fact]
    public async Task Update_Should_Return_NotFound_When_Missing()
    {
        // Arrange
        var fakeService = new FakeUserService { UpdateResult = null };
        var controller = new UsersController(fakeService, new UserRequestValidator());
        var request = new UserRequestDto
        {
            IdirName = "updateduser",
            IsEnabled = true,
            FirstName = "Updated",
            LastName = "Name",
            Email = "updated@example.com",
            Gender = Gender.Other,
            Rank = "Constable",
            BadgeNumber = "BADGE-UPDATED",
            HomeLocationId = 1,
        };

        // Act
        var result = await controller.Update(Guid.NewGuid(), request, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    private static UserResponse CreateUserResponse(string firstName, string lastName)
    {
        return new UserResponse(
            Id: Guid.NewGuid(),
            IdirName: "idir",
            IdirId: Guid.NewGuid(),
            IsEnabled: true,
            FirstName: firstName,
            LastName: lastName,
            Email: $"{firstName}.{lastName}@example.com",
            Gender: Gender.Other,
            Rank: "Deputy Sheriff",
            BadgeNumber: "BADGE-TEST",
            HomeLocationId: 1,
            LastLogin: DateTimeOffset.UtcNow
        );
    }

    private sealed class FakeUserService : IUserService
    {
        public IReadOnlyCollection<UserResponse> GetAllResult { get; init; } = [];

        public UserResponse? GetByIdResult { get; init; }

        public UserResponse CreateResult { get; init; } = CreateUserResponse("Created", "User");

        public UserResponse? UpdateResult { get; init; } = CreateUserResponse("Updated", "User");

        public UserQueryParams? LastQueryParams { get; private set; }

        public Task<IReadOnlyCollection<UserResponse>> GetAllAsync(
            UserQueryParams? queryParams = null,
            CancellationToken cancellationToken = default
        )
        {
            LastQueryParams = queryParams;
            return Task.FromResult(GetAllResult);
        }

        public Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetByIdResult);
        }

        public Task<UserResponse> CreateAsync(UserRequestDto request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateResult);
        }

        public Task<UserResponse?> UpdateAsync(
            Guid id,
            UserRequestDto request,
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult(UpdateResult);
        }
    }
}
