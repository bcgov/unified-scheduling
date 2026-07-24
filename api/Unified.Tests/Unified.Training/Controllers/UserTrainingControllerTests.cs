using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Unified.Tests.TestHelpers;
using Unified.Training.Controllers;
using Unified.Training.Models;
using Unified.Training.Services;
using Unified.Training.Validators;

namespace Unified.Tests.Training.Controllers;

public class UserTrainingControllerTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static readonly Guid OtherUserId = Guid.NewGuid();

    private static UserTrainingRequest ValidRequest() =>
        new()
        {
            UserId = UserId,
            TrainingId = 1,
            AwardedOn = DateTimeOffset.UtcNow,
            EndingOn = DateTimeOffset.UtcNow.AddDays(1),
        };

    private static UserTrainingResponse SampleResponse(int id = 1) =>
        new()
        {
            Id = id,
            UserId = UserId,
            TrainingId = 1,
            TrainingCode = "FA",
            TrainingCategoryName = "Safety",
            AwardedOn = DateTimeOffset.UtcNow,
            NoticeState = "None",
        };

    [Fact]
    public async Task GetAll_WhenValid_Returns200Ok()
    {
        var response = new[] { SampleResponse(), SampleResponse(2) };
        var service = new FakeUserTrainingService { GetAllResult = response };
        var controller = BuildController(service, callerId: UserId);

        var result = await controller.GetAll(OtherUserId, TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(response, ok.Value);
    }

    [Fact]
    public async Task GetAll_WhenUserIdClaimMissing_Returns401()
    {
        var controller = BuildController(new FakeUserTrainingService(), callerId: null);

        var result = await controller.GetAll(OtherUserId, TestContext.Current.CancellationToken);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task GetAllByUser_WhenValid_Returns200Ok()
    {
        var response = new[] { SampleResponse(), SampleResponse(2) };
        var service = new FakeUserTrainingService { GetAllResult = response };
        var controller = BuildController(service, callerId: UserId);

        var result = await controller.GetAllByUser(OtherUserId, TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(response, ok.Value);
    }

    [Fact]
    public async Task GetByTrainingAndUser_WhenFound_Returns200Ok()
    {
        var response = SampleResponse();
        var service = new FakeUserTrainingService { GetByTrainingAndUserResult = response };
        var controller = BuildController(service, callerId: UserId);

        var result = await controller.GetByTrainingAndUser(1, OtherUserId, TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(response, ok.Value);
    }

    [Fact]
    public async Task GetByTrainingAndUser_WhenNotFound_Returns404()
    {
        var service = new FakeUserTrainingService { GetByTrainingAndUserResult = null };
        var controller = BuildController(service, callerId: UserId);

        var result = await controller.GetByTrainingAndUser(1, OtherUserId, TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_WhenValid_Returns201Created()
    {
        var response = SampleResponse();
        var service = new FakeUserTrainingService { CreateResult = response };
        var controller = BuildController(service, callerId: UserId);

        var result = await controller.Create(ValidRequest(), TestContext.Current.CancellationToken);

        var created = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal("/api/training/user-trainings/1", created.Location);
        Assert.Equal(response, created.Value);
    }

    [Fact]
    public async Task Create_WhenUserIdClaimMissing_Returns401()
    {
        var controller = BuildController(new FakeUserTrainingService(), callerId: null);

        var result = await controller.Create(ValidRequest(), TestContext.Current.CancellationToken);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task Create_WhenRequestIsInvalid_ThrowsValidationException()
    {
        var controller = BuildController(new FakeUserTrainingService(), callerId: UserId);
        var badRequest = new UserTrainingRequest
        {
            UserId = Guid.Empty, // invalid
            TrainingId = 1,
            AwardedOn = DateTimeOffset.UtcNow,
            EndingOn = DateTimeOffset.UtcNow.AddDays(1),
        };

        await Assert.ThrowsAsync<ValidationException>(() =>
            controller.Create(badRequest, TestContext.Current.CancellationToken)
        );
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_WhenFound_Returns200Ok()
    {
        var response = SampleResponse(42);
        var service = new FakeUserTrainingService { UpdateResult = response };
        var controller = BuildController(service, callerId: UserId);

        var result = await controller.Update(42, ValidRequest(), TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(response, ok.Value);
    }

    [Fact]
    public async Task Update_WhenNotFound_Returns404()
    {
        var service = new FakeUserTrainingService { UpdateResult = null };
        var controller = BuildController(service, callerId: UserId);

        var result = await controller.Update(99, ValidRequest(), TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Update_WhenUserIdClaimMissing_Returns401()
    {
        var controller = BuildController(new FakeUserTrainingService(), callerId: null);

        var result = await controller.Update(1, ValidRequest(), TestContext.Current.CancellationToken);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_WhenFound_Returns204NoContent()
    {
        var service = new FakeUserTrainingService { DeleteResult = true };
        var controller = BuildController(service, callerId: UserId);

        var result = await controller.Delete(1, TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_WhenNotFound_Returns404()
    {
        var service = new FakeUserTrainingService { DeleteResult = false };
        var controller = BuildController(service, callerId: UserId);

        var result = await controller.Delete(99, TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_WhenUserIdClaimMissing_Returns401()
    {
        var controller = BuildController(new FakeUserTrainingService(), callerId: null);

        var result = await controller.Delete(1, TestContext.Current.CancellationToken);

        Assert.IsType<UnauthorizedResult>(result);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static UserTrainingController BuildController(IUserTrainingService service, Guid? callerId)
    {
        var controller = new UserTrainingController(service, new UserTrainingRequestValidator());
        controller.ControllerContext = ControllerContextFactory.CreateWithUserId(callerId);
        return controller;
    }

    private sealed class FakeUserTrainingService : IUserTrainingService
    {
        public IReadOnlyCollection<UserTrainingResponse> GetAllResult { get; init; } = [];
        public UserTrainingResponse? GetByTrainingAndUserResult { get; init; }
        public UserTrainingResponse? CreateResult { get; init; }
        public UserTrainingResponse? UpdateResult { get; init; }
        public bool DeleteResult { get; init; }

        public Task<IReadOnlyCollection<UserTrainingResponse>> GetAllAsync(
            Guid userId,
            Guid callerUserId,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(GetAllResult);

        public Task<UserTrainingResponse?> GetByTrainingAndUserAsync(
            int trainingId,
            Guid userId,
            Guid callerUserId,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(GetByTrainingAndUserResult);

        public Task<UserTrainingResponse> CreateAsync(
            UserTrainingRequest request,
            Guid callerUserId,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(CreateResult!);

        public Task<UserTrainingResponse?> UpdateAsync(
            int id,
            UserTrainingRequest request,
            Guid callerUserId,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(UpdateResult);

        public Task<bool> DeleteAsync(int id, Guid callerUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(DeleteResult);
    }
}
