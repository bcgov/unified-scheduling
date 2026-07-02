using FluentValidation.TestHelper;
using Unified.Scheduling.Models;
using Unified.Scheduling.Validators;

namespace Unified.Tests.Scheduling.Validators;

public class SchedulingCalendarRequestValidatorTests
{
    private readonly SchedulingCalendarRequestValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_WhenInclusiveRangeIsExactlyMaximumLength_HasNoEndDateError()
    {
        // Arrange
        var request = new SchedulingCalendarRequest
        {
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2027, 1, 1),
        };

        // Act
        var result = await _validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.EndDate);
    }

    [Fact]
    public async Task ValidateAsync_WhenInclusiveRangeExceedsMaximumLength_HasEndDateError()
    {
        // Arrange
        var request = new SchedulingCalendarRequest
        {
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2027, 1, 2),
        };

        // Act
        var result = await _validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.EndDate);
    }
}
