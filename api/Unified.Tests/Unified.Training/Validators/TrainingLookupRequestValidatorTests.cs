using FluentValidation;
using Unified.Training.Models;
using Unified.Training.Validators;

namespace Unified.Tests.Training.Validators;

public sealed class TrainingLookupRequestValidatorTests
{
    private readonly TrainingLookupRequestValidator _validator = new();

    private static TrainingLookupRequest ValidRequest() =>
        new()
        {
            Code = "FIRE",
            Description = "Firearms Qualification",
            Mandatory = true,
            ValidityDays = 365,
            AdvanceNoticeDays = 30,
            Rotating = true,
            TrainingCategoryId = 1,
        };

    [Fact]
    public async Task Validate_WhenAdvanceNoticeDaysIsNull_PassesValidation()
    {
        var request = ValidRequest() with { AdvanceNoticeDays = null };

        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_WhenAdvanceNoticeDaysIsZero_FailsValidation()
    {
        var request = ValidRequest() with { AdvanceNoticeDays = 0 };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _validator.ValidateAndThrowAsync(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Validate_WhenAdvanceNoticeDaysIsPositive_PassesValidation()
    {
        var request = ValidRequest() with { AdvanceNoticeDays = 1 };

        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }
}
