using FluentValidation.TestHelper;
using Unified.Scheduling.Models;
using Unified.Scheduling.Validators;

namespace Unified.Tests.Scheduling.Validators;

public sealed class SchedulePublishRequestValidatorTests
{
    private readonly SchedulePublishRequestValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_WhenInclusiveRangeIsExactlyMaximumLength_HasNoEndDateError()
    {
        var request = new SchedulePublishRequest(5, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1));

        var result = await _validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldNotHaveValidationErrorFor(candidate => candidate.EndDate);
    }

    [Fact]
    public async Task ValidateAsync_WhenInclusiveRangeExceedsMaximumLength_HasEndDateError()
    {
        var request = new SchedulePublishRequest(5, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 2));

        var result = await _validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(candidate => candidate.EndDate);
    }
}
