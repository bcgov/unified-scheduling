using FluentValidation.TestHelper;
using Unified.Calendar.Models;
using Unified.Calendar.Validators;

namespace Unified.Tests.Calendar.Validators;

public class CalendarEventsRequestValidatorTests
{
    private readonly CalendarEventsRequestValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_WhenRequestIsValid_HasNoErrors()
    {
        // Arrange
        var request = new CalendarEventsRequest
        {
            StartDate = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 6, 8, 0, 0, 0, TimeSpan.Zero),
            LocationId = 0,
        };

        // Act
        var result = await _validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task ValidateAsync_WhenStartDateIsTooEarly_HasError()
    {
        // Arrange
        var request = new CalendarEventsRequest
        {
            StartDate = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(1900, 1, 2, 0, 0, 0, TimeSpan.Zero),
        };

        // Act
        var result = await _validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.StartDate);
    }

    [Fact]
    public async Task ValidateAsync_WhenEndDateIsTooEarly_HasError()
    {
        // Arrange
        var request = new CalendarEventsRequest
        {
            StartDate = new DateTimeOffset(1900, 1, 2, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

        // Act
        var result = await _validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.EndDate);
    }

    [Fact]
    public async Task ValidateAsync_WhenStartDateIsNotBeforeEndDate_HasError()
    {
        // Arrange
        var request = new CalendarEventsRequest
        {
            StartDate = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
        };

        // Act
        var result = await _validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.StartDate);
    }

    [Fact]
    public async Task ValidateAsync_WhenRangeExceedsMaximumLength_HasError()
    {
        // Arrange
        var request = new CalendarEventsRequest
        {
            StartDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2027, 1, 3, 0, 0, 0, TimeSpan.Zero),
        };

        // Act
        var result = await _validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.EndDate);
    }

    [Fact]
    public async Task ValidateAsync_WhenLocationIdIsNegative_HasError()
    {
        // Arrange
        var request = new CalendarEventsRequest
        {
            StartDate = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 6, 8, 0, 0, 0, TimeSpan.Zero),
            LocationId = -1,
        };

        // Act
        var result = await _validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LocationId);
    }
}
