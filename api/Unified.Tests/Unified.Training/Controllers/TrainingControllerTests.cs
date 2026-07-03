using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Unified.Training.Controllers;
using Unified.Training.Models;
using Unified.Training.Services;
using Unified.Training.Validators;

namespace Unified.Tests.Training.Controllers;

public class TrainingsControllerTests
{
    [Fact]
    public async Task Create_WhenValid_ReturnsCreated()
    {
        var request = new TrainingRequest
        {
            Code = "FIREARMS",
            Description = "Firearms Qualification",
            TrainingCategoryId = 1,
        };

        var response = new TrainingResponse
        {
            Id = 10,
            Code = "FIREARMS",
            Description = "Firearms Qualification",
            TrainingCategoryId = 1,
            TrainingCategoryName = "Mandatory",
        };

        var service = new FakeTrainingService { CreateResult = response };
        var controller = new TrainingsController(service, new TrainingRequestValidator());

        var result = await controller.Create(request, TestContext.Current.CancellationToken);

        var created = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal("/api/trainings/10", created.Location);
        Assert.Equal(response, created.Value);
    }

    [Fact]
    public async Task Update_WhenMissing_ReturnsNotFound()
    {
        var request = new TrainingRequest
        {
            Code = "FIREARMS",
            Description = "Firearms Qualification",
            TrainingCategoryId = 1,
        };

        var service = new FakeTrainingService { UpdateResult = null };
        var controller = new TrainingsController(service, new TrainingRequestValidator());

        var result = await controller.Update(99, request, TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Create_WhenInvalid_ThrowsValidationException()
    {
        var request = new TrainingRequest
        {
            Code = string.Empty,
            Description = "Firearms Qualification",
            TrainingCategoryId = 1,
        };

        var service = new FakeTrainingService();
        var controller = new TrainingsController(service, new TrainingRequestValidator());

        await Assert.ThrowsAsync<ValidationException>(() =>
            controller.Create(request, TestContext.Current.CancellationToken)
        );
    }

    private sealed class FakeTrainingService : ITrainingService
    {
        public IReadOnlyCollection<TrainingResponse> GetAllResult { get; init; } = [];
        public TrainingResponse? GetByIdResult { get; init; }
        public TrainingResponse? CreateResult { get; init; }
        public TrainingResponse? UpdateResult { get; init; }
        public bool DeleteResult { get; init; }

        public Task<IReadOnlyCollection<TrainingResponse>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(GetAllResult);

        public Task<TrainingResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(GetByIdResult);

        public Task<TrainingResponse> CreateAsync(
            TrainingRequest request,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(CreateResult!);

        public Task<TrainingResponse?> UpdateAsync(
            int id,
            TrainingRequest request,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(UpdateResult);

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(DeleteResult);
    }
}
