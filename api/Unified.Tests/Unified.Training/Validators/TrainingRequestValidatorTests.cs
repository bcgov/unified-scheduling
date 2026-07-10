using FluentValidation;
using Unified.Training.Models;
using Unified.Training.Validators;

namespace Unified.Tests.Training.Validators;

public class TrainingRequestValidatorTests
{
    [Fact]
    public async Task Validate_WhenRequestIsValid_DoesNotThrow()
    {
        var validator = new TrainingRequestValidator();
        var request = new TrainingRequest
        {
            Code = "FIREARMS",
            Description = "Firearms Qualification",
            TrainingCategoryId = 1,
            ValidityDays = 365,
            AdvanceNoticeDays = 30,
            Order = 1,
        };

        await validator.ValidateAndThrowAsync(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Validate_WhenCodeMissing_ThrowsValidationException()
    {
        var validator = new TrainingRequestValidator();
        var request = new TrainingRequest
        {
            Code = string.Empty,
            Description = "Firearms Qualification",
            TrainingCategoryId = 1,
        };

        await Assert.ThrowsAsync<ValidationException>(() =>
            validator.ValidateAndThrowAsync(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Validate_WhenValidityDaysIsZero_ThrowsValidationException()
    {
        var validator = new TrainingRequestValidator();
        var request = new TrainingRequest
        {
            Code = "FIREARMS",
            Description = "Firearms Qualification",
            ValidityDays = 0,
        };

        await Assert.ThrowsAsync<ValidationException>(() =>
            validator.ValidateAndThrowAsync(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Validate_WhenAdvanceNoticeDaysIsEqualToValidityDays_ThrowsValidationException()
    {
        var validator = new TrainingRequestValidator();
        var request = new TrainingRequest
        {
            Code = "FIREARMS",
            Description = "Firearms Qualification",
            ValidityDays = 30,
            AdvanceNoticeDays = 30,
        };

        await Assert.ThrowsAsync<ValidationException>(() =>
            validator.ValidateAndThrowAsync(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Validate_WhenAdvanceNoticeDaysIsGreaterThanValidityDays_ThrowsValidationException()
    {
        var validator = new TrainingRequestValidator();
        var request = new TrainingRequest
        {
            Code = "FIREARMS",
            Description = "Firearms Qualification",
            ValidityDays = 30,
            AdvanceNoticeDays = 60,
        };

        await Assert.ThrowsAsync<ValidationException>(() =>
            validator.ValidateAndThrowAsync(request, TestContext.Current.CancellationToken)
        );
    }
}
