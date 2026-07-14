using Microsoft.AspNetCore.Mvc;
using Unified.Training.Controllers;
using Unified.Training.Models;
using Unified.Training.Services.Lookup;
using Unified.Training.Validators;
using LookupCodeResponse = Unified.Core.Models.LookupCodeResponse;
using LookupCodeTypes = Unified.Core.Models.LookupCodeTypes;

namespace Unified.Tests.Training.Controllers;

public class TrainingLookupControllerTests
{
    [Fact]
    public async Task GetAll_Should_Return_Ok_When_Trainings_Are_Available()
    {
        var expected = new List<TrainingLookupResponse>
        {
            new()
            {
                Id = 10,
                Code = "FIRE",
                Description = "Firearms Qualification",
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
                Mandatory = true,
                Order = 0,
                CreatedOn = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            },
        };

        var strategy = new FakeTrainingLookupStrategy { TrainingsResult = expected };
        var controller = new TrainingLookupController(strategy, new TrainingLookupRequestValidator());

        var result = await controller.GetAll(TestContext.Current.CancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IEnumerable<TrainingLookupResponse>>(okResult.Value);
        var item = Assert.Single(payload);

        Assert.Equal("FIRE", item.Code);
    }

    [Fact]
    public async Task GetById_Should_Return_Ok_When_Training_Exists()
    {
        var training = new TrainingLookupResponse
        {
            Id = 10,
            Code = "FIRE",
            Description = "Firearms Qualification",
            EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Mandatory = true,
            Order = 0,
            CreatedOn = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

        var strategy = new FakeTrainingLookupStrategy { TrainingsResult = [training] };
        var controller = new TrainingLookupController(strategy, new TrainingLookupRequestValidator());

        var result = await controller.GetById(10, TestContext.Current.CancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(training, okResult.Value);
    }

    [Fact]
    public async Task GetById_Should_Return_NotFound_When_Training_Does_Not_Exist()
    {
        var strategy = new FakeTrainingLookupStrategy();
        var controller = new TrainingLookupController(strategy, new TrainingLookupRequestValidator());

        var result = await controller.GetById(99, TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task MoveOrder_Should_Return_BadRequest_When_NewOrder_Is_Negative()
    {
        var strategy = new FakeTrainingLookupStrategy();
        var controller = new TrainingLookupController(strategy, new TrainingLookupRequestValidator());

        var result = await controller.MoveOrder(
            1,
            new TrainingLookupMoveOrderRequest { NewOrder = -1 },
            TestContext.Current.CancellationToken
        );

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    private sealed class FakeTrainingLookupStrategy : ITrainingLookupStrategy
    {
        public IReadOnlyCollection<TrainingLookupResponse> TrainingsResult { get; init; } = [];

        public LookupCodeTypes CodeType => LookupCodeTypes.Trainings;

        public Task<IReadOnlyCollection<TrainingLookupResponse>> GetAllTrainingsAsync(
            CancellationToken cancellationToken = default
        ) => Task.FromResult(TrainingsResult);

        public Task<IReadOnlyCollection<LookupCodeResponse>> GetAllAsync(
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyCollection<LookupCodeResponse>>(TrainingsResult);

        public Task<TrainingLookupResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(TrainingsResult.SingleOrDefault(x => x.Id == id));

        public Task<TrainingLookupResponse> CreateAsync(
            TrainingLookupRequest request,
            CancellationToken cancellationToken = default
        ) =>
            Task.FromResult(
                new TrainingLookupResponse
                {
                    Id = 1,
                    Code = request.Code,
                    Description = request.Description ?? string.Empty,
                    Mandatory = request.Mandatory,
                    ValidityDays = request.ValidityDays,
                    AdvanceNoticeDays = request.AdvanceNoticeDays,
                    Rotating = request.Rotating,
                    TrainingCategoryId = request.TrainingCategoryId,
                    Order = request.Order,
                    CreatedOn = DateTimeOffset.UtcNow,
                }
            );

        public Task<TrainingLookupResponse?> UpdateAsync(
            int id,
            TrainingLookupRequest request,
            CancellationToken cancellationToken = default
        ) =>
            Task.FromResult<TrainingLookupResponse?>(
                new TrainingLookupResponse
                {
                    Id = id,
                    Code = request.Code,
                    Description = request.Description ?? string.Empty,
                    Mandatory = request.Mandatory,
                    ValidityDays = request.ValidityDays,
                    AdvanceNoticeDays = request.AdvanceNoticeDays,
                    Rotating = request.Rotating,
                    TrainingCategoryId = request.TrainingCategoryId,
                    Order = request.Order,
                    CreatedOn = DateTimeOffset.UtcNow,
                }
            );

        public Task<TrainingLookupResponse?> MoveOrderAsync(
            int id,
            int newOrder,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(TrainingsResult.SingleOrDefault(x => x.Id == id));
    }
}
