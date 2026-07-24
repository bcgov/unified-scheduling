using FluentValidation.TestHelper;
using Unified.UserManagement.Models;
using Unified.UserManagement.Validators;

namespace Unified.Tests.UserManagement.Validators;

public class ExpireAwayLocationRequestValidatorTests
{
    private readonly ExpireAwayLocationRequestValidator _validator = new();

    [Fact]
    public void Validate_When_All_Fields_Are_Valid_Should_Pass()
    {
        // Arrange
        var request = new ExpireAwayLocationRequestDto { AwayLocationId = 1001, ExpiryReason = "ENTRYERR" };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_When_AwayLocationId_Is_Zero_Should_Fail()
    {
        // Arrange
        var request = new ExpireAwayLocationRequestDto { AwayLocationId = 0, ExpiryReason = "ENTRYERR" };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.AwayLocationId);
    }

    [Fact]
    public void Validate_When_ExpiryReason_Is_Empty_Should_Fail()
    {
        // Arrange
        var request = new ExpireAwayLocationRequestDto { AwayLocationId = 1001, ExpiryReason = "" };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ExpiryReason);
    }

    [Fact]
    public void Validate_When_ExpiryReason_Exceeds_200_Characters_Should_Fail()
    {
        // Arrange
        var request = new ExpireAwayLocationRequestDto { AwayLocationId = 1001, ExpiryReason = new string('a', 201) };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ExpiryReason);
    }

    [Fact]
    public void Validate_When_ExpiryReason_Is_Exactly_200_Characters_Should_Pass()
    {
        // Arrange
        var request = new ExpireAwayLocationRequestDto { AwayLocationId = 1001, ExpiryReason = new string('a', 200) };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
