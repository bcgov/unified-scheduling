using FluentValidation.TestHelper;
using Unified.Calendar.Models;
using Unified.Calendar.Validators;

namespace Unified.Tests.Calendar.Validators;

public class CalendarDataRequestValidatorTests
{
    private readonly CalendarDataRequestValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_WhenRequestIsValid_HasNoErrors()
    {
        // Arrange
        var request = new CalendarDataRequest
        {
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 8),
            TimeZoneId = "America/Vancouver",
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
        var request = new CalendarDataRequest
        {
            StartDate = new DateOnly(1900, 1, 1),
            EndDate = new DateOnly(1900, 1, 2),
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
        var request = new CalendarDataRequest
        {
            StartDate = new DateOnly(1900, 1, 2),
            EndDate = new DateOnly(1900, 1, 1),
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
        var request = new CalendarDataRequest
        {
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 1),
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
        var request = new CalendarDataRequest
        {
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2027, 1, 3),
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
        var request = new CalendarDataRequest
        {
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 8),
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

    [Fact]
    public async Task ValidateAsync_WhenTimeZoneIdIsInvalid_HasError()
    {
        // Arrange
        var request = new CalendarDataRequest
        {
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 8),
            TimeZoneId = "Not/AZone",
        };

        // Act
        var result = await _validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TimeZoneId);
    }
}
