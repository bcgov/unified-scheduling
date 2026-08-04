using FluentValidation.TestHelper;
using Unified.UserManagement.Models;
using Unified.UserManagement.Validators;

namespace Unified.Tests.UserManagement.Validators;

public class AwayLocationRequestValidatorTests
{
    private readonly AwayLocationRequestValidator _validator = new();

    [Fact]
    public void Validate_When_All_Fields_Are_Valid_Should_Pass()
    {
        // Arrange
        var request = new AwayLocationRequestDto
        {
            LocationId = 1,
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-06-30T00:00:00.000-07:00",
            Timezone = "America/Vancouver",
            Comment = "Training visit",
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_When_Timezone_And_Comment_Are_Null_Should_Pass()
    {
        // Arrange
        var request = new AwayLocationRequestDto
        {
            LocationId = 1,
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-06-30T00:00:00.000-07:00",
            Timezone = null,
            Comment = null,
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_When_LocationId_Is_Zero_Should_Fail()
    {
        // Arrange
        var request = new AwayLocationRequestDto
        {
            LocationId = 0,
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-06-30T00:00:00.000-07:00",
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LocationId);
    }

    [Fact]
    public void Validate_When_StartDateTime_Is_Empty_Should_Fail()
    {
        // Arrange
        var request = new AwayLocationRequestDto
        {
            LocationId = 1,
            StartDateTime = "",
            EndDateTime = "2026-06-30T00:00:00.000-07:00",
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.StartDateTime);
    }

    [Fact]
    public void Validate_When_StartDateTime_Is_Invalid_Format_Should_Fail()
    {
        // Arrange
        var request = new AwayLocationRequestDto
        {
            LocationId = 1,
            StartDateTime = "2026-01-10", // date only — missing time and offset
            EndDateTime = "2026-06-30T00:00:00.000-07:00",
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.StartDateTime);
    }

    [Fact]
    public void Validate_When_EndDateTime_Is_Empty_Should_Fail()
    {
        // Arrange
        var request = new AwayLocationRequestDto
        {
            LocationId = 1,
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "",
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.EndDateTime);
    }

    [Fact]
    public void Validate_When_EndDateTime_Is_Invalid_Format_Should_Fail()
    {
        // Arrange
        var request = new AwayLocationRequestDto
        {
            LocationId = 1,
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-06-30", // date only — missing time and offset
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.EndDateTime);
    }

    [Fact]
    public void Validate_When_EndDateTime_Is_Before_StartDateTime_Should_Fail()
    {
        // Arrange
        var request = new AwayLocationRequestDto
        {
            LocationId = 1,
            StartDateTime = "2026-06-01T00:00:00.000-07:00",
            EndDateTime = "2026-01-01T00:00:00.000-08:00",
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x);
    }

    [Fact]
    public void Validate_When_EndDateTime_Equals_StartDateTime_Should_Fail()
    {
        // Arrange
        var request = new AwayLocationRequestDto
        {
            LocationId = 1,
            StartDateTime = "2026-01-10T08:30:00.000-08:00",
            EndDateTime = "2026-01-10T08:30:00.000-08:00",
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x);
    }

    [Fact]
    public void Validate_When_Comment_Exceeds_500_Characters_Should_Fail()
    {
        // Arrange
        var request = new AwayLocationRequestDto
        {
            LocationId = 1,
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-06-30T00:00:00.000-07:00",
            Comment = new string('a', 501),
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Comment);
    }

    [Fact]
    public void Validate_When_Comment_Is_Exactly_500_Characters_Should_Pass()
    {
        // Arrange
        var request = new AwayLocationRequestDto
        {
            LocationId = 1,
            StartDateTime = "2026-01-10T00:00:00.000-08:00",
            EndDateTime = "2026-06-30T00:00:00.000-07:00",
            Comment = new string('a', 500),
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
