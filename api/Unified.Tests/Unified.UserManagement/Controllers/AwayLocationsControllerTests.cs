using Microsoft.AspNetCore.Mvc;
using Unified.UserManagement.Controllers;
using Unified.UserManagement.Models;
using Unified.UserManagement.Services;
using Unified.UserManagement.Validators;

namespace Unified.Tests.UserManagement.Controllers;

public class AwayLocationsControllerTests
{
    private static AwayLocationsController CreateController(FakeAwayLocationService fakeService) =>
        new(fakeService, new AwayLocationRequestValidator(), new ExpireAwayLocationRequestValidator());

    [Fact]
    public async Task GetAll_Should_Return_Ok_With_AwayLocations()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expectedAwayLocations = new List<AwayLocationResponseDto>
        {
            CreateAwayLocationResponse(userId, 1, "Victoria"),
        };
        var fakeService = new FakeAwayLocationService { GetByUserIdResult = expectedAwayLocations };
        var controller = CreateController(fakeService);

        // Act
        var result = await controller.GetAll(userId, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var awayLocations = Assert.IsAssignableFrom<IEnumerable<AwayLocationResponseDto>>(okResult.Value);
        Assert.Single(awayLocations);
        Assert.Equal(userId, fakeService.LastGetByUserIdUserId);
    }

    [Fact]
    public async Task GetAll_Should_Throw_When_User_Missing()
    {
        // Arrange
        var fakeService = new FakeAwayLocationService
        {
            GetByUserIdException = new KeyNotFoundException("User not found."),
        };
        var controller = CreateController(fakeService);

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            controller.GetAll(Guid.NewGuid(), TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Create_Should_Return_Created_With_Location_And_Body()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var createdAwayLocation = CreateAwayLocationResponse(userId, 1, "Victoria");
        var fakeService = new FakeAwayLocationService { CreateResult = createdAwayLocation };
        var controller = CreateController(fakeService);

        var request = new AwayLocationRequestDto
        {
            LocationId = 1,
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-06-30T00:00:00.000-07:00",
            Comment = "Training at Victoria courthouse",
        };

        // Act
        var result = await controller.Create(userId, request, TestContext.Current.CancellationToken);

        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result.Result);
        var awayLocation = Assert.IsType<AwayLocationResponseDto>(createdResult.Value);
        Assert.Equal($"/api/users/{userId}/away-locations/{createdAwayLocation.Id}", createdResult.Location);
        Assert.Equal(createdAwayLocation.Id, awayLocation.Id);
        Assert.Equal(createdAwayLocation.LocationName, awayLocation.LocationName);
        Assert.Equal(userId, fakeService.LastCreateUserId);
        Assert.Same(request, fakeService.LastCreateRequest);
    }

    [Fact]
    public async Task Create_Should_Throw_When_User_Missing()
    {
        // Arrange
        var fakeService = new FakeAwayLocationService { CreateException = new KeyNotFoundException("User not found.") };
        var controller = CreateController(fakeService);
        var request = new AwayLocationRequestDto
        {
            LocationId = 1,
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-06-30T00:00:00.000-07:00",
        };

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            controller.Create(Guid.NewGuid(), request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Create_Should_Throw_Validation_Error_When_LocationId_Is_Zero()
    {
        // Arrange
        var fakeService = new FakeAwayLocationService();
        var controller = CreateController(fakeService);
        var request = new AwayLocationRequestDto
        {
            LocationId = 0,
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-06-30T00:00:00.000-07:00",
        };

        // Act + Assert
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            controller.Create(Guid.NewGuid(), request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Create_Should_Throw_Validation_Error_When_EndDateTime_Empty()
    {
        // Arrange
        var fakeService = new FakeAwayLocationService();
        var controller = CreateController(fakeService);
        var request = new AwayLocationRequestDto
        {
            LocationId = 1,
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "",
        };

        // Act + Assert
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            controller.Create(Guid.NewGuid(), request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Create_Should_Throw_Validation_Error_When_EndDateTime_Before_StartDateTime()
    {
        // Arrange
        var fakeService = new FakeAwayLocationService();
        var controller = CreateController(fakeService);
        var request = new AwayLocationRequestDto
        {
            LocationId = 1,
            StartDateTime = "2026-06-01T00:00:00.000-07:00",
            EndDateTime = "2026-01-01T00:00:00.000-08:00",
        };

        // Act + Assert
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            controller.Create(Guid.NewGuid(), request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Create_Should_Throw_Validation_Error_When_EndDateTime_Equal_To_StartDateTime()
    {
        // Arrange
        var fakeService = new FakeAwayLocationService();
        var controller = CreateController(fakeService);
        var request = new AwayLocationRequestDto
        {
            LocationId = 1,
            StartDateTime = "2026-01-10T08:30:00.000-08:00",
            EndDateTime = "2026-01-10T08:30:00.000-08:00",
        };

        // Act + Assert
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            controller.Create(Guid.NewGuid(), request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Create_Should_Not_Throw_When_StartDateTime_And_EndDateTime_Include_Time()
    {
        // Arrange
        var fakeService = new FakeAwayLocationService();
        var controller = CreateController(fakeService);
        var request = new AwayLocationRequestDto
        {
            LocationId = 1,
            StartDateTime = "2026-01-10T08:30:00.000-08:00",
            EndDateTime = "2026-01-10T17:00:00.000-08:00",
        };

        // Act
        var result = await controller.Create(Guid.NewGuid(), request, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<CreatedResult>(result.Result);
    }

    [Fact]
    public async Task Create_Should_Throw_Validation_Error_When_DateTime_Invalid_Format()
    {
        // Arrange
        var fakeService = new FakeAwayLocationService();
        var controller = CreateController(fakeService);
        var request = new AwayLocationRequestDto
        {
            LocationId = 1,
            StartDateTime = "2026-01-10",
            EndDateTime = "2026-06-30",
        };

        // Act + Assert
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            controller.Create(Guid.NewGuid(), request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Create_Should_Pass_Timezone_Through_To_Service()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var fakeService = new FakeAwayLocationService();
        var controller = CreateController(fakeService);
        var request = new AwayLocationRequestDto
        {
            LocationId = 1,
            StartDateTime = "2026-01-10T08:30:00.000-08:00",
            EndDateTime = "2026-01-10T17:00:00.000-08:00",
            Timezone = "America/Vancouver",
        };

        // Act
        await controller.Create(userId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("America/Vancouver", fakeService.LastCreateRequest?.Timezone);
    }

    [Fact]
    public async Task Update_Should_Return_Ok()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var updatedAwayLocation = CreateAwayLocationResponse(userId, 2, "Vancouver");
        var fakeService = new FakeAwayLocationService { UpdateResult = updatedAwayLocation };
        var controller = CreateController(fakeService);

        var request = new AwayLocationRequestDto
        {
            LocationId = 2,
            StartDateTime = "2026-02-01T00:00:00.000-08:00",
            EndDateTime = "2026-08-01T00:00:00.000-07:00",
            Comment = "Updated comment",
        };

        // Act
        var result = await controller.Update(userId, 1001, request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var awayLocation = Assert.IsType<AwayLocationResponseDto>(okResult.Value);
        Assert.Equal(updatedAwayLocation.Id, awayLocation.Id);
        Assert.Equal(userId, fakeService.LastUpdateUserId);
        Assert.Equal(1001, fakeService.LastUpdateAwayLocationId);
        Assert.Same(request, fakeService.LastUpdateRequest);
    }

    [Fact]
    public async Task Update_Should_Throw_When_AwayLocation_Missing()
    {
        // Arrange
        var fakeService = new FakeAwayLocationService
        {
            UpdateException = new KeyNotFoundException("Away location not found."),
        };
        var controller = CreateController(fakeService);
        var request = new AwayLocationRequestDto
        {
            LocationId = 1,
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-06-30T00:00:00.000-07:00",
        };

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            controller.Update(Guid.NewGuid(), 9999, request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Expire_Should_Return_Ok()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expiredAwayLocation = CreateAwayLocationResponse(userId, 1, "Victoria") with
        {
            ExpiryAtUtc = DateTimeOffset.UtcNow,
            ExpiryReason = "ENTRYERR",
        };
        var fakeService = new FakeAwayLocationService { ExpireResult = expiredAwayLocation };
        var controller = CreateController(fakeService);

        var request = new ExpireAwayLocationRequestDto
        {
            AwayLocationId = expiredAwayLocation.Id,
            ExpiryReason = "ENTRYERR",
        };

        // Act
        var result = await controller.Expire(userId, request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var awayLocation = Assert.IsType<AwayLocationResponseDto>(okResult.Value);
        Assert.Equal("ENTRYERR", awayLocation.ExpiryReason);
        Assert.NotNull(awayLocation.ExpiryAtUtc);
    }

    [Fact]
    public async Task Expire_Should_Throw_When_AwayLocation_Missing()
    {
        // Arrange
        var fakeService = new FakeAwayLocationService
        {
            ExpireException = new KeyNotFoundException("Away location not found."),
        };
        var controller = CreateController(fakeService);
        var request = new ExpireAwayLocationRequestDto { AwayLocationId = 9999, ExpiryReason = "ENTRYERR" };

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            controller.Expire(Guid.NewGuid(), request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Expire_Should_Throw_Validation_Error_When_ExpiryReason_Empty()
    {
        // Arrange
        var fakeService = new FakeAwayLocationService();
        var controller = CreateController(fakeService);
        var request = new ExpireAwayLocationRequestDto { AwayLocationId = 1001, ExpiryReason = "" };

        // Act + Assert
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            controller.Expire(Guid.NewGuid(), request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Expire_Should_Throw_Validation_Error_When_AwayLocationId_Is_Zero()
    {
        // Arrange
        var fakeService = new FakeAwayLocationService();
        var controller = CreateController(fakeService);
        var request = new ExpireAwayLocationRequestDto { AwayLocationId = 0, ExpiryReason = "ENTRYERR" };

        // Act + Assert
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            controller.Expire(Guid.NewGuid(), request, TestContext.Current.CancellationToken)
        );
    }

    private static AwayLocationResponseDto CreateAwayLocationResponse(
        Guid userId,
        int locationId,
        string locationName
    ) =>
        new()
        {
            Id = 1001,
            UserId = userId,
            LocationId = locationId,
            LocationName = locationName,
            LocationTimezone = "America/Vancouver",
            StartAtUtc = DateTimeOffset.UtcNow.AddDays(-10),
            EndAtUtc = DateTimeOffset.UtcNow.AddDays(20),
            ExpiryAtUtc = null,
            ExpiryReason = null,
            Comment = null,
            Timezone = "America/Vancouver",
        };

    private sealed class FakeAwayLocationService : IAwayLocationService
    {
        public IReadOnlyCollection<AwayLocationResponseDto> GetByUserIdResult { get; init; } = [];
        public Exception? GetByUserIdException { get; init; }
        public Guid LastGetByUserIdUserId { get; private set; }

        public AwayLocationResponseDto CreateResult { get; init; } =
            new()
            {
                Id = 1001,
                UserId = Guid.NewGuid(),
                LocationId = 1,
                LocationName = "Victoria",
                LocationTimezone = "America/Vancouver",
                StartAtUtc = DateTimeOffset.UtcNow,
                EndAtUtc = DateTimeOffset.UtcNow.AddDays(30),
                ExpiryAtUtc = null,
                ExpiryReason = null,
                Comment = null,
                Timezone = "America/Vancouver",
            };
        public Exception? CreateException { get; init; }
        public Guid LastCreateUserId { get; private set; }
        public AwayLocationRequestDto? LastCreateRequest { get; private set; }

        public AwayLocationResponseDto UpdateResult { get; init; } =
            new()
            {
                Id = 1001,
                UserId = Guid.NewGuid(),
                LocationId = 2,
                LocationName = "Vancouver",
                LocationTimezone = "America/Vancouver",
                StartAtUtc = DateTimeOffset.UtcNow,
                EndAtUtc = DateTimeOffset.UtcNow.AddDays(30),
                ExpiryAtUtc = null,
                ExpiryReason = null,
                Comment = null,
                Timezone = "America/Vancouver",
            };
        public Exception? UpdateException { get; init; }
        public Guid LastUpdateUserId { get; private set; }
        public int LastUpdateAwayLocationId { get; private set; }
        public AwayLocationRequestDto? LastUpdateRequest { get; private set; }

        public AwayLocationResponseDto ExpireResult { get; init; } =
            new()
            {
                Id = 1001,
                UserId = Guid.NewGuid(),
                LocationId = 1,
                LocationName = "Victoria",
                LocationTimezone = "America/Vancouver",
                StartAtUtc = DateTimeOffset.UtcNow.AddDays(-30),
                EndAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                ExpiryAtUtc = DateTimeOffset.UtcNow,
                ExpiryReason = "ENTRYERR",
                Comment = null,
                Timezone = "America/Vancouver",
            };
        public Exception? ExpireException { get; init; }

        public Task<IReadOnlyCollection<AwayLocationResponseDto>> GetByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default
        )
        {
            LastGetByUserIdUserId = userId;
            if (GetByUserIdException is not null)
            {
                return Task.FromException<IReadOnlyCollection<AwayLocationResponseDto>>(GetByUserIdException);
            }

            return Task.FromResult(GetByUserIdResult);
        }

        public Task<AwayLocationResponseDto> CreateAsync(
            Guid userId,
            AwayLocationRequestDto request,
            CancellationToken cancellationToken = default
        )
        {
            LastCreateUserId = userId;
            LastCreateRequest = request;
            if (CreateException is not null)
            {
                return Task.FromException<AwayLocationResponseDto>(CreateException);
            }

            return Task.FromResult(CreateResult);
        }

        public Task<AwayLocationResponseDto> UpdateAsync(
            Guid userId,
            int awayLocationId,
            AwayLocationRequestDto request,
            CancellationToken cancellationToken = default
        )
        {
            LastUpdateUserId = userId;
            LastUpdateAwayLocationId = awayLocationId;
            LastUpdateRequest = request;
            if (UpdateException is not null)
            {
                return Task.FromException<AwayLocationResponseDto>(UpdateException);
            }

            return Task.FromResult(UpdateResult);
        }

        public Task<AwayLocationResponseDto> ExpireAsync(
            Guid userId,
            ExpireAwayLocationRequestDto request,
            CancellationToken cancellationToken = default
        )
        {
            if (ExpireException is not null)
            {
                return Task.FromException<AwayLocationResponseDto>(ExpireException);
            }

            return Task.FromResult(ExpireResult);
        }
    }
}
