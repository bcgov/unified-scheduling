using Microsoft.AspNetCore.Mvc;
using Unified.UserManagement.Controllers;
using Unified.UserManagement.Models;
using Unified.UserManagement.Services;
using Unified.UserManagement.Validators;

namespace Unified.Tests.UserManagement.Controllers;

public class LeavesControllerTests
{
    private static LeavesController CreateController(FakeLeaveService fakeService) =>
        new(fakeService, new LeaveRequestValidator(), new ExpireLeaveRequestValidator());

    [Fact]
    public async Task GetAll_Should_Return_Ok_With_Leaves()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expectedLeaves = new List<LeaveResponseDto> { CreateLeaveResponse(userId, "VAC") };
        var fakeService = new FakeLeaveService { GetByUserIdResult = expectedLeaves };
        var controller = CreateController(fakeService);

        // Act
        var result = await controller.GetAll(userId, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var leaves = Assert.IsAssignableFrom<IEnumerable<LeaveResponseDto>>(okResult.Value);
        Assert.Single(leaves);
        Assert.Equal(userId, fakeService.LastGetByUserIdUserId);
    }

    [Fact]
    public async Task GetAll_Should_Throw_When_User_Missing()
    {
        // Arrange
        var fakeService = new FakeLeaveService { GetByUserIdException = new KeyNotFoundException("User not found.") };
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
        var createdLeave = CreateLeaveResponse(userId, "VAC");
        var fakeService = new FakeLeaveService { CreateResult = createdLeave };
        var controller = CreateController(fakeService);

        var request = new LeaveRequestDto
        {
            LeaveTypeCode = "VAC",
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-01-20T00:00:00.000-08:00",
            Comment = "Family trip",
        };

        // Act
        var result = await controller.Create(userId, request, TestContext.Current.CancellationToken);

        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result.Result);
        var leave = Assert.IsType<LeaveResponseDto>(createdResult.Value);
        Assert.Equal($"/api/users/{userId}/leaves/{createdLeave.Id}", createdResult.Location);
        Assert.Equal(createdLeave.Id, leave.Id);
        Assert.Equal(userId, fakeService.LastCreateUserId);
        Assert.Same(request, fakeService.LastCreateRequest);
    }

    [Fact]
    public async Task Create_Should_Throw_When_User_Missing()
    {
        // Arrange
        var fakeService = new FakeLeaveService { CreateException = new KeyNotFoundException("User not found.") };
        var controller = CreateController(fakeService);
        var request = new LeaveRequestDto
        {
            LeaveTypeCode = "VAC",
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-01-20T00:00:00.000-08:00",
        };

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            controller.Create(Guid.NewGuid(), request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Create_Should_Throw_When_LeaveType_Missing()
    {
        // Arrange
        var fakeService = new FakeLeaveService
        {
            CreateException = new KeyNotFoundException("LeaveType 'UNKNOWN' not found."),
        };
        var controller = CreateController(fakeService);
        var request = new LeaveRequestDto
        {
            LeaveTypeCode = "UNKNOWN",
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-01-20T00:00:00.000-08:00",
        };

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            controller.Create(Guid.NewGuid(), request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Create_Should_Throw_Validation_Error_When_LeaveTypeCode_Empty()
    {
        // Arrange
        var fakeService = new FakeLeaveService();
        var controller = CreateController(fakeService);
        var request = new LeaveRequestDto
        {
            LeaveTypeCode = "",
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-01-20T00:00:00.000-08:00",
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
        var fakeService = new FakeLeaveService();
        var controller = CreateController(fakeService);
        var request = new LeaveRequestDto
        {
            LeaveTypeCode = "VAC",
            StartDateTime = "2026-06-01T00:00:00.000-07:00",
            EndDateTime = "2026-01-01T00:00:00.000-08:00",
        };

        // Act + Assert
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            controller.Create(Guid.NewGuid(), request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Create_Should_Throw_Validation_Error_When_DateTime_Invalid_Format()
    {
        // Arrange
        var fakeService = new FakeLeaveService();
        var controller = CreateController(fakeService);
        var request = new LeaveRequestDto
        {
            LeaveTypeCode = "VAC",
            StartDateTime = "2026-01-10",
            EndDateTime = "2026-01-20",
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
        var fakeService = new FakeLeaveService();
        var controller = CreateController(fakeService);
        var request = new LeaveRequestDto
        {
            LeaveTypeCode = "VAC",
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
        var updatedLeave = CreateLeaveResponse(userId, "SICK");
        var fakeService = new FakeLeaveService { UpdateResult = updatedLeave };
        var controller = CreateController(fakeService);

        var request = new LeaveRequestDto
        {
            LeaveTypeCode = "SICK",
            StartDateTime = "2026-02-01T00:00:00.000-08:00",
            EndDateTime = "2026-02-05T00:00:00.000-08:00",
            Comment = "Updated comment",
        };

        // Act
        var result = await controller.Update(userId, 1001, request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var leave = Assert.IsType<LeaveResponseDto>(okResult.Value);
        Assert.Equal(updatedLeave.Id, leave.Id);
        Assert.Equal(userId, fakeService.LastUpdateUserId);
        Assert.Equal(1001, fakeService.LastUpdateLeaveId);
        Assert.Same(request, fakeService.LastUpdateRequest);
    }

    [Fact]
    public async Task Update_Should_Throw_When_Leave_Missing()
    {
        // Arrange
        var fakeService = new FakeLeaveService { UpdateException = new KeyNotFoundException("Leave not found.") };
        var controller = CreateController(fakeService);
        var request = new LeaveRequestDto
        {
            LeaveTypeCode = "VAC",
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-01-20T00:00:00.000-08:00",
        };

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            controller.Update(Guid.NewGuid(), 9999, request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Update_Should_Throw_When_Leave_Already_Expired()
    {
        // Arrange
        var fakeService = new FakeLeaveService
        {
            UpdateException = new InvalidOperationException("Leave 1001 is already expired and cannot be edited."),
        };
        var controller = CreateController(fakeService);
        var request = new LeaveRequestDto
        {
            LeaveTypeCode = "VAC",
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-01-20T00:00:00.000-08:00",
        };

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.Update(Guid.NewGuid(), 1001, request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Expire_Should_Return_Ok()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expiredLeave = CreateLeaveResponse(userId, "VAC") with
        {
            ExpiryAtUtc = DateTimeOffset.UtcNow,
            ExpiryReason = "ENTRYERR",
        };
        var fakeService = new FakeLeaveService { ExpireResult = expiredLeave };
        var controller = CreateController(fakeService);

        var request = new ExpireLeaveRequestDto { LeaveId = expiredLeave.Id, ExpiryReason = "ENTRYERR" };

        // Act
        var result = await controller.Expire(userId, request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var leave = Assert.IsType<LeaveResponseDto>(okResult.Value);
        Assert.Equal("ENTRYERR", leave.ExpiryReason);
        Assert.NotNull(leave.ExpiryAtUtc);
    }

    [Fact]
    public async Task Expire_Should_Throw_When_Leave_Missing()
    {
        // Arrange
        var fakeService = new FakeLeaveService { ExpireException = new KeyNotFoundException("Leave not found.") };
        var controller = CreateController(fakeService);
        var request = new ExpireLeaveRequestDto { LeaveId = 9999, ExpiryReason = "ENTRYERR" };

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            controller.Expire(Guid.NewGuid(), request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Expire_Should_Throw_Validation_Error_When_ExpiryReason_Empty()
    {
        // Arrange
        var fakeService = new FakeLeaveService();
        var controller = CreateController(fakeService);
        var request = new ExpireLeaveRequestDto { LeaveId = 1001, ExpiryReason = "" };

        // Act + Assert
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            controller.Expire(Guid.NewGuid(), request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Expire_Should_Throw_Validation_Error_When_LeaveId_Is_Zero()
    {
        // Arrange
        var fakeService = new FakeLeaveService();
        var controller = CreateController(fakeService);
        var request = new ExpireLeaveRequestDto { LeaveId = 0, ExpiryReason = "ENTRYERR" };

        // Act + Assert
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            controller.Expire(Guid.NewGuid(), request, TestContext.Current.CancellationToken)
        );
    }

    private static LeaveResponseDto CreateLeaveResponse(Guid userId, string leaveTypeCode) =>
        new()
        {
            Id = 1001,
            EventId = 1,
            UserId = userId,
            LeaveTypeId = 17,
            LeaveTypeCode = leaveTypeCode,
            LeaveTypeDescription = leaveTypeCode,
            IsPaid = true,
            StartAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            EndAtUtc = DateTimeOffset.UtcNow.AddDays(1),
            ExpiryAtUtc = null,
            ExpiryReason = null,
            Comment = null,
            Timezone = "America/Vancouver",
        };

    private sealed class FakeLeaveService : ILeaveService
    {
        public IReadOnlyCollection<LeaveResponseDto> GetByUserIdResult { get; init; } = [];
        public Exception? GetByUserIdException { get; init; }
        public Guid LastGetByUserIdUserId { get; private set; }

        public LeaveResponseDto CreateResult { get; init; } =
            new()
            {
                Id = 1001,
                EventId = 1,
                UserId = Guid.NewGuid(),
                LeaveTypeId = 17,
                LeaveTypeCode = "VAC",
                LeaveTypeDescription = "Vacation",
                IsPaid = true,
                StartAtUtc = DateTimeOffset.UtcNow,
                EndAtUtc = DateTimeOffset.UtcNow.AddDays(10),
                Timezone = "America/Vancouver",
            };
        public Exception? CreateException { get; init; }
        public Guid LastCreateUserId { get; private set; }
        public LeaveRequestDto? LastCreateRequest { get; private set; }

        public LeaveResponseDto UpdateResult { get; init; } =
            new()
            {
                Id = 1001,
                EventId = 2,
                UserId = Guid.NewGuid(),
                LeaveTypeId = 18,
                LeaveTypeCode = "SICK",
                LeaveTypeDescription = "Sick",
                IsPaid = true,
                StartAtUtc = DateTimeOffset.UtcNow,
                EndAtUtc = DateTimeOffset.UtcNow.AddDays(4),
                Timezone = "America/Vancouver",
            };
        public Exception? UpdateException { get; init; }
        public Guid LastUpdateUserId { get; private set; }
        public int LastUpdateLeaveId { get; private set; }
        public LeaveRequestDto? LastUpdateRequest { get; private set; }

        public LeaveResponseDto ExpireResult { get; init; } =
            new()
            {
                Id = 1001,
                EventId = 1,
                UserId = Guid.NewGuid(),
                LeaveTypeId = 17,
                LeaveTypeCode = "VAC",
                LeaveTypeDescription = "Vacation",
                IsPaid = true,
                StartAtUtc = DateTimeOffset.UtcNow.AddDays(-30),
                EndAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                ExpiryAtUtc = DateTimeOffset.UtcNow,
                ExpiryReason = "ENTRYERR",
                Timezone = "America/Vancouver",
            };
        public Exception? ExpireException { get; init; }

        public Task<IReadOnlyCollection<LeaveResponseDto>> GetByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default
        )
        {
            LastGetByUserIdUserId = userId;
            return GetByUserIdException is not null
                ? Task.FromException<IReadOnlyCollection<LeaveResponseDto>>(GetByUserIdException)
                : Task.FromResult(GetByUserIdResult);
        }

        public Task<LeaveResponseDto> CreateAsync(
            Guid userId,
            LeaveRequestDto request,
            CancellationToken cancellationToken = default
        )
        {
            LastCreateUserId = userId;
            LastCreateRequest = request;
            return CreateException is not null
                ? Task.FromException<LeaveResponseDto>(CreateException)
                : Task.FromResult(CreateResult);
        }

        public Task<LeaveResponseDto> UpdateAsync(
            Guid userId,
            int leaveId,
            LeaveRequestDto request,
            CancellationToken cancellationToken = default
        )
        {
            LastUpdateUserId = userId;
            LastUpdateLeaveId = leaveId;
            LastUpdateRequest = request;
            return UpdateException is not null
                ? Task.FromException<LeaveResponseDto>(UpdateException)
                : Task.FromResult(UpdateResult);
        }

        public Task<LeaveResponseDto> ExpireAsync(
            Guid userId,
            ExpireLeaveRequestDto request,
            CancellationToken cancellationToken = default
        )
        {
            return ExpireException is not null
                ? Task.FromException<LeaveResponseDto>(ExpireException)
                : Task.FromResult(ExpireResult);
        }
    }
}
