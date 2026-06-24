using Microsoft.AspNetCore.Mvc;
using Unified.UserManagement.Controllers;
using Unified.UserManagement.Models;
using Unified.UserManagement.Services;
using Unified.UserManagement.Validators;

namespace Unified.Tests.UserManagement.Controllers;

public class ActingPositionsControllerTests
{
    private static ActingPositionsController CreateController(FakeActingPositionService fakeService) =>
        new(fakeService, new ActingPositionRequestValidator(), new ExpireActingPositionRequestValidator());

    [Fact]
    public async Task GetAll_Should_Return_Ok_With_Positions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expectedPositions = new List<ActingPositionResponseDto>
        {
            CreateActingPositionResponse(userId, "SGT", "Sergeant"),
        };
        var fakeService = new FakeActingPositionService { GetByUserIdResult = expectedPositions };
        var controller = CreateController(fakeService);

        // Act
        var result = await controller.GetAll(userId, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var positions = Assert.IsAssignableFrom<IEnumerable<ActingPositionResponseDto>>(okResult.Value);
        Assert.Single(positions);
        Assert.Equal(userId, fakeService.LastGetByUserIdUserId);
    }

    [Fact]
    public async Task GetAll_Should_Throw_When_User_Missing()
    {
        // Arrange
        var fakeService = new FakeActingPositionService
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
        var createdPosition = CreateActingPositionResponse(userId, "CPL", "Corporal");
        var fakeService = new FakeActingPositionService { CreateResult = createdPosition };
        var controller = CreateController(fakeService);

        var request = new ActingPositionRequestDto
        {
            PositionTypeCode = "CPL",
            StartDate = "2026-01-10",
            Comment = "Acting due to vacancy",
        };

        // Act
        var result = await controller.Create(userId, request, TestContext.Current.CancellationToken);

        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result.Result);
        var position = Assert.IsType<ActingPositionResponseDto>(createdResult.Value);
        Assert.Equal($"/api/users/{userId}/acting-positions/{createdPosition.Id}", createdResult.Location);
        Assert.Equal(createdPosition.Id, position.Id);
        Assert.Equal(createdPosition.PositionTypeCode, position.PositionTypeCode);
        Assert.Equal(userId, fakeService.LastCreateUserId);
        Assert.Same(request, fakeService.LastCreateRequest);
    }

    [Fact]
    public async Task Create_Should_Throw_When_User_Missing()
    {
        // Arrange
        var fakeService = new FakeActingPositionService
        {
            CreateException = new KeyNotFoundException("User not found."),
        };
        var controller = CreateController(fakeService);
        var request = new ActingPositionRequestDto { PositionTypeCode = "SGT", StartDate = "2026-01-10" };

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            controller.Create(Guid.NewGuid(), request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Create_Should_Throw_Validation_Error_When_PositionTypeCode_Empty()
    {
        // Arrange
        var fakeService = new FakeActingPositionService();
        var controller = CreateController(fakeService);
        var request = new ActingPositionRequestDto { PositionTypeCode = "", StartDate = "2026-01-10" };

        // Act + Assert
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            controller.Create(Guid.NewGuid(), request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Update_Should_Return_Ok()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var updatedPosition = CreateActingPositionResponse(userId, "SGT", "Sergeant");
        var fakeService = new FakeActingPositionService { UpdateResult = updatedPosition };
        var controller = CreateController(fakeService);

        var request = new ActingPositionRequestDto
        {
            PositionTypeCode = "SGT",
            StartDate = "2026-02-01",
            Comment = "Updated comment",
        };

        // Act
        var result = await controller.Update(userId, 1001, request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var position = Assert.IsType<ActingPositionResponseDto>(okResult.Value);
        Assert.Equal(updatedPosition.Id, position.Id);
        Assert.Equal(userId, fakeService.LastUpdateUserId);
        Assert.Equal(1001, fakeService.LastUpdateActingPositionId);
        Assert.Same(request, fakeService.LastUpdateRequest);
    }

    [Fact]
    public async Task Update_Should_Throw_When_Position_Missing()
    {
        // Arrange
        var fakeService = new FakeActingPositionService
        {
            UpdateException = new KeyNotFoundException("Acting position not found."),
        };
        var controller = CreateController(fakeService);
        var request = new ActingPositionRequestDto { PositionTypeCode = "SGT", StartDate = "2026-01-10" };

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
        var expiredPosition = CreateActingPositionResponse(userId, "SGT", "Sergeant") with
        {
            ExpiryAtUtc = DateTimeOffset.UtcNow,
            ExpiryReason = "ENTRYERR",
        };
        var fakeService = new FakeActingPositionService { ExpireResult = expiredPosition };
        var controller = CreateController(fakeService);

        var request = new ExpireActingPositionRequestDto
        {
            ActingPositionId = expiredPosition.Id,
            ExpiryReason = "ENTRYERR",
        };

        // Act
        var result = await controller.Expire(userId, request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var position = Assert.IsType<ActingPositionResponseDto>(okResult.Value);
        Assert.Equal("ENTRYERR", position.ExpiryReason);
        Assert.NotNull(position.ExpiryAtUtc);
    }

    [Fact]
    public async Task Expire_Should_Throw_When_Position_Missing()
    {
        // Arrange
        var fakeService = new FakeActingPositionService
        {
            ExpireException = new KeyNotFoundException("Acting position not found."),
        };
        var controller = CreateController(fakeService);
        var request = new ExpireActingPositionRequestDto { ActingPositionId = 9999, ExpiryReason = "ENTRYERR" };

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            controller.Expire(Guid.NewGuid(), request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Expire_Should_Throw_Validation_Error_When_ExpiryReason_Empty()
    {
        // Arrange
        var fakeService = new FakeActingPositionService();
        var controller = CreateController(fakeService);
        var request = new ExpireActingPositionRequestDto { ActingPositionId = 1001, ExpiryReason = "" };

        // Act + Assert
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            controller.Expire(Guid.NewGuid(), request, TestContext.Current.CancellationToken)
        );
    }

    private static ActingPositionResponseDto CreateActingPositionResponse(
        Guid userId,
        string code,
        string description
    ) =>
        new()
        {
            Id = 1001,
            UserId = userId,
            PositionTypeCode = code,
            PositionTypeDescription = description,
            StartAtUtc = DateTimeOffset.UtcNow.AddDays(-10),
            ExpiryAtUtc = null,
            ExpiryReason = null,
            Comment = null,
        };

    private sealed class FakeActingPositionService : IActingPositionService
    {
        public IReadOnlyCollection<ActingPositionResponseDto> GetByUserIdResult { get; init; } = [];
        public Exception? GetByUserIdException { get; init; }
        public Guid LastGetByUserIdUserId { get; private set; }

        public ActingPositionResponseDto CreateResult { get; init; } =
            new()
            {
                Id = 1001,
                UserId = Guid.NewGuid(),
                PositionTypeCode = "SGT",
                PositionTypeDescription = "Sergeant",
                StartAtUtc = DateTimeOffset.UtcNow,
                ExpiryAtUtc = null,
                ExpiryReason = null,
                Comment = null,
            };
        public Exception? CreateException { get; init; }
        public Guid LastCreateUserId { get; private set; }
        public ActingPositionRequestDto? LastCreateRequest { get; private set; }

        public ActingPositionResponseDto UpdateResult { get; init; } =
            new()
            {
                Id = 1001,
                UserId = Guid.NewGuid(),
                PositionTypeCode = "CPL",
                PositionTypeDescription = "Corporal",
                StartAtUtc = DateTimeOffset.UtcNow,
                ExpiryAtUtc = null,
                ExpiryReason = null,
                Comment = null,
            };
        public Exception? UpdateException { get; init; }
        public Guid LastUpdateUserId { get; private set; }
        public int LastUpdateActingPositionId { get; private set; }
        public ActingPositionRequestDto? LastUpdateRequest { get; private set; }

        public ActingPositionResponseDto ExpireResult { get; init; } =
            new()
            {
                Id = 1001,
                UserId = Guid.NewGuid(),
                PositionTypeCode = "SGT",
                PositionTypeDescription = "Sergeant",
                StartAtUtc = DateTimeOffset.UtcNow.AddDays(-30),
                ExpiryAtUtc = DateTimeOffset.UtcNow,
                ExpiryReason = "ENTRYERR",
                Comment = null,
            };
        public Exception? ExpireException { get; init; }

        public Task<IReadOnlyCollection<ActingPositionResponseDto>> GetByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default
        )
        {
            LastGetByUserIdUserId = userId;
            if (GetByUserIdException is not null)
            {
                return Task.FromException<IReadOnlyCollection<ActingPositionResponseDto>>(GetByUserIdException);
            }

            return Task.FromResult(GetByUserIdResult);
        }

        public Task<ActingPositionResponseDto> CreateAsync(
            Guid userId,
            ActingPositionRequestDto request,
            CancellationToken cancellationToken = default
        )
        {
            LastCreateUserId = userId;
            LastCreateRequest = request;
            if (CreateException is not null)
            {
                return Task.FromException<ActingPositionResponseDto>(CreateException);
            }

            return Task.FromResult(CreateResult);
        }

        public Task<ActingPositionResponseDto> UpdateAsync(
            Guid userId,
            int actingPositionId,
            ActingPositionRequestDto request,
            CancellationToken cancellationToken = default
        )
        {
            LastUpdateUserId = userId;
            LastUpdateActingPositionId = actingPositionId;
            LastUpdateRequest = request;
            if (UpdateException is not null)
            {
                return Task.FromException<ActingPositionResponseDto>(UpdateException);
            }

            return Task.FromResult(UpdateResult);
        }

        public Task<ActingPositionResponseDto> ExpireAsync(
            Guid userId,
            ExpireActingPositionRequestDto request,
            CancellationToken cancellationToken = default
        )
        {
            if (ExpireException is not null)
            {
                return Task.FromException<ActingPositionResponseDto>(ExpireException);
            }

            return Task.FromResult(ExpireResult);
        }
    }
}
