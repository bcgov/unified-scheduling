using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Common.ImageFormat;
using Unified.Db.Models.UserManagement;
using Unified.UserManagement.Controllers;
using Unified.UserManagement.Models;
using Unified.UserManagement.Services;
using Unified.UserManagement.Validators;

namespace Unified.Tests.UserManagement.Controllers;

public class UsersControllerTests
{
    private static UsersController CreateController(FakeUserService fakeService)
    {
        return new UsersController(
            fakeService,
            new UserRequestValidator(),
            new AssignUserRoleRequestValidator(),
            new ExpireUserRoleRequestValidator()
        );
    }

    [Fact]
    public async Task Get_Should_Return_Ok_With_Users()
    {
        // Arrange
        var expectedUsers = new List<UserResponse> { CreateUserResponse("John", "Smith") };
        var fakeService = new FakeUserService { GetAllResult = expectedUsers };
        var controller = CreateController(fakeService);
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
        var controller = CreateController(fakeService);

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
        var controller = CreateController(fakeService);

        // Act
        var result = await controller.GetById(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetRoles_Should_Return_Ok_With_User_Roles()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expectedRoles = new List<UserRoleResponseDto>
        {
            new()
            {
                Id = 9001,
                UserId = userId,
                RoleId = 2,
                EffectiveDate = DateTimeOffset.UtcNow.AddDays(-10),
                ExpiryDate = null,
            },
        };
        var fakeService = new FakeUserService { GetRolesResult = expectedRoles };
        var controller = CreateController(fakeService);

        // Act
        var result = await controller.GetRoles(userId, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var roles = Assert.IsAssignableFrom<IEnumerable<UserRoleResponseDto>>(okResult.Value);
        Assert.Single(roles);
    }

    [Fact]
    public async Task GetRoles_Should_Throw_When_User_Missing()
    {
        // Arrange
        var fakeService = new FakeUserService { GetRolesException = new KeyNotFoundException("User not found.") };
        var controller = CreateController(fakeService);

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            controller.GetRoles(Guid.NewGuid(), TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Create_Should_Return_Created_With_Location_And_Body()
    {
        // Arrange
        var createdUser = CreateUserResponse("New", "User");
        var fakeService = new FakeUserService { CreateResult = createdUser };
        var controller = CreateController(fakeService);

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
        var controller = CreateController(fakeService);

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
        var controller = CreateController(fakeService);

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

    [Fact]
    public async Task AssignRole_Should_Return_Ok_When_User_Found()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var assignedUserRole = new UserRoleResponseDto
        {
            Id = 5001,
            UserId = userId,
            RoleId = 5,
            EffectiveDate = DateTimeOffset.UtcNow,
            ExpiryDate = null,
            ExpiryReason = null,
        };
        var fakeService = new FakeUserService { AssignRoleResult = assignedUserRole };
        var controller = CreateController(fakeService);

        var request = new AssignUserRoleRequestDto
        {
            RoleId = 5,
            EffectiveDate = "2026-01-10",
            ExpiryDate = "2026-02-10",
        };

        // Act
        var result = await controller.AssignRole(userId, request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<UserRoleResponseDto>(okResult.Value);
        Assert.Equal(5001, response.Id);
        Assert.Equal(userId, response.UserId);
        Assert.NotNull(fakeService.LastAssignRoleRequest);
        Assert.Equal(request.ExpiryDate, fakeService.LastAssignRoleRequest!.ExpiryDate);
    }

    [Fact]
    public async Task AssignRole_Should_Throw_When_User_Missing()
    {
        // Arrange
        var fakeService = new FakeUserService { AssignRoleException = new KeyNotFoundException("User not found.") };
        var controller = CreateController(fakeService);

        var request = new AssignUserRoleRequestDto { RoleId = 5, EffectiveDate = "2026-01-10" };

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            controller.AssignRole(Guid.NewGuid(), request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task ExpireRole_Should_Return_Ok_When_User_Role_Exists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expiredUserRole = new UserRoleResponseDto
        {
            Id = 5002,
            UserId = userId,
            RoleId = 5,
            EffectiveDate = DateTimeOffset.UtcNow.AddDays(-30),
            ExpiryDate = DateTimeOffset.UtcNow,
            ExpiryReason = "PERSONAL",
        };
        var fakeService = new FakeUserService { ExpireRoleResult = expiredUserRole };
        var controller = CreateController(fakeService);

        var request = new ExpireUserRoleRequestDto { RoleId = 5, ExpiryReason = "PERSONAL" };

        // Act
        var result = await controller.ExpireRole(userId, request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<UserRoleResponseDto>(okResult.Value);
        Assert.Equal(expiredUserRole.Id, response.Id);
        Assert.Equal(expiredUserRole.ExpiryReason, response.ExpiryReason);
    }

    [Fact]
    public async Task ExpireRole_Should_Throw_When_User_Role_Missing()
    {
        // Arrange
        var fakeService = new FakeUserService { ExpireRoleException = new KeyNotFoundException("Role not found.") };
        var controller = CreateController(fakeService);

        var request = new ExpireUserRoleRequestDto { RoleId = 5, ExpiryReason = "ENTRYERR" };

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            controller.ExpireRole(Guid.NewGuid(), request, TestContext.Current.CancellationToken)
        );
    }

    // Smallest valid byte sequences recognised by ImageFormatDetector.
    private static byte[] MinimalJpeg() => [.. ImageFormatDetector.JpegSignature, 0xE0];

    private static byte[] MinimalPng() => [.. ImageFormatDetector.PngSignature];

    private static UserResponse CreateUserResponse(string firstName, string lastName)
    {
        return new UserResponse
        {
            Id = Guid.NewGuid(),
            IdirName = "idir",
            IdirId = Guid.NewGuid(),
            IsEnabled = true,
            FirstName = firstName,
            LastName = lastName,
            Email = $"{firstName}.{lastName}@example.com",
            Gender = Gender.Other,
            Rank = "Deputy Sheriff",
            BadgeNumber = "BADGE-TEST",
            HomeLocationId = 1,
            LastLogin = DateTimeOffset.UtcNow,
            PendingRegistration: false
        };
    }

    // --- Photo tests ---

    [Fact]
    public async Task GetPhoto_Should_Return_File_When_User_Has_Photo()
    {
        // Arrange
        var photoBytes = "fake-image-data"u8.ToArray();
        var fakeService = new FakeUserService { GetPhotoResult = photoBytes };
        var controller = CreateController(fakeService);

        // Act
        var result = await controller.GetPhoto(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("image/jpeg", fileResult.ContentType);
        Assert.Equal(photoBytes, fileResult.FileContents);
    }

    [Fact]
    public async Task GetPhoto_Should_Return_NotFound_When_User_Has_No_Photo()
    {
        // Arrange
        var fakeService = new FakeUserService { GetPhotoResult = null };
        var controller = CreateController(fakeService);

        // Act
        var result = await controller.GetPhoto(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UploadPhoto_Should_Return_Ok_With_Updated_User()
    {
        // Arrange
        var updatedUser = CreateUserResponse("Jane", "Doe") with
        {
            PhotoUrl = "/api/users/jane/photo",
        };
        var fakeService = new FakeUserService { UploadPhotoResult = updatedUser };
        var controller = CreateController(fakeService);
        var photoBytes = MinimalJpeg();
        IFormFile formFile = new FormFile(new MemoryStream(photoBytes), 0, photoBytes.Length, "photo", "avatar.jpg");

        // Act
        var result = await controller.UploadPhoto(updatedUser.Id, formFile, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var user = Assert.IsType<UserResponse>(okResult.Value);
        Assert.Equal(updatedUser.Id, user.Id);
        Assert.Equal("/api/users/jane/photo", user.PhotoUrl);
        Assert.NotNull(fakeService.LastUploadedPhoto);
        Assert.Equal(photoBytes.Length, fakeService.LastUploadedPhoto!.Length);
    }

    [Fact]
    public async Task UploadPhoto_Should_Return_NotFound_When_User_Missing()
    {
        // Arrange
        var fakeService = new FakeUserService { UploadPhotoResult = null };
        var controller = CreateController(fakeService);
        var photoBytes = MinimalJpeg();
        IFormFile formFile = new FormFile(new MemoryStream(photoBytes), 0, photoBytes.Length, "photo", "avatar.jpg");

        // Act
        var result = await controller.UploadPhoto(Guid.NewGuid(), formFile, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UploadPhoto_Should_Throw_When_Format_Is_Unsupported()
    {
        // Arrange — file bytes do not match JPEG or PNG magic
        var fakeService = new FakeUserService { UploadPhotoResult = CreateUserResponse("A", "B") };
        var controller = CreateController(fakeService);
        var photoBytes = "fake-image-data"u8.ToArray();
        IFormFile formFile = new FormFile(new MemoryStream(photoBytes), 0, photoBytes.Length, "photo", "image.webp");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.UploadPhoto(Guid.NewGuid(), formFile, TestContext.Current.CancellationToken)
        );
    }

    private sealed class FakeUserService : IUserService
    {
        public IReadOnlyCollection<UserResponse> GetAllResult { get; init; } = [];

        public UserResponse? GetByIdResult { get; init; }

        public UserResponse CreateResult { get; init; } = CreateUserResponse("Created", "User");

        public UserResponse? UpdateResult { get; init; } = CreateUserResponse("Updated", "User");

        public IReadOnlyCollection<UserRoleResponseDto> GetRolesResult { get; init; } = [];

        public Exception? GetRolesException { get; init; }

        public UserRoleResponseDto AssignRoleResult { get; init; } =
            new UserRoleResponseDto
            {
                Id = 5001,
                UserId = Guid.NewGuid(),
                RoleId = 1,
                EffectiveDate = DateTimeOffset.UtcNow,
                ExpiryDate = null,
                ExpiryReason = null,
            };

        public Exception? AssignRoleException { get; init; }

        public UserRoleResponseDto ExpireRoleResult { get; init; } =
            new UserRoleResponseDto
            {
                Id = 5002,
                UserId = Guid.NewGuid(),
                RoleId = 1,
                EffectiveDate = DateTimeOffset.UtcNow.AddDays(-30),
                ExpiryDate = DateTimeOffset.UtcNow,
                ExpiryReason = "ENTRYERR",
            };

        public Exception? ExpireRoleException { get; init; }

        public UserQueryParams? LastQueryParams { get; private set; }

        public AssignUserRoleRequestDto? LastAssignRoleRequest { get; private set; }

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

        public Task<IReadOnlyCollection<UserRoleResponseDto>> GetRolesAsync(
            Guid id,
            CancellationToken cancellationToken = default
        )
        {
            if (GetRolesException is not null)
            {
                return Task.FromException<IReadOnlyCollection<UserRoleResponseDto>>(GetRolesException);
            }

            return Task.FromResult(GetRolesResult);
        }

        public Task<UserRoleResponseDto> AssignRoleAsync(
            Guid id,
            AssignUserRoleRequestDto request,
            CancellationToken cancellationToken = default
        )
        {
            if (AssignRoleException is not null)
            {
                return Task.FromException<UserRoleResponseDto>(AssignRoleException);
            }

            LastAssignRoleRequest = request;

            return Task.FromResult(AssignRoleResult);
        }

        public Task<UserRoleResponseDto> ExpireRoleAsync(
            Guid id,
            ExpireUserRoleRequestDto request,
            CancellationToken cancellationToken = default
        )
        {
            if (ExpireRoleException is not null)
            {
                return Task.FromException<UserRoleResponseDto>(ExpireRoleException);
            }

            return Task.FromResult(ExpireRoleResult);
        }

        // --- Photo ---
        public byte[]? GetPhotoResult { get; init; }

        public UserResponse? UploadPhotoResult { get; init; }

        public byte[]? LastUploadedPhoto { get; private set; }

        public Task<byte[]?> GetPhotoAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(GetPhotoResult);

        public Task<UserResponse?> UploadPhotoAsync(
            Guid id,
            byte[] photo,
            CancellationToken cancellationToken = default
        )
        {
            LastUploadedPhoto = photo;
            return Task.FromResult(UploadPhotoResult);
        }
    }
}
