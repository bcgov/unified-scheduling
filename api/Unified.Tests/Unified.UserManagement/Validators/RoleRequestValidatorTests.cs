using FluentValidation;
using FluentValidation.TestHelper;
using Unified.UserManagement.Models;
using Unified.UserManagement.Validators;

namespace Unified.Tests.UserManagement.Validators;

public class RoleRequestValidatorTests
{
    private readonly RoleRequestValidator _validator = new();

    [Fact]
    public void Validate_When_All_Required_Fields_Present_Should_Pass()
    {
        // Arrange
        var request = new RoleRequestDto { Name = "Administrator", Description = "System administrator role" };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_When_Name_Empty_Should_Fail()
    {
        // Arrange
        var request = new RoleRequestDto { Name = "", Description = "Valid description" };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_When_Description_Empty_Should_Fail()
    {
        // Arrange
        var request = new RoleRequestDto { Name = "Valid Name", Description = "" };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_When_Name_Exceeds_Max_Length_Should_Fail()
    {
        // Arrange
        var request = new RoleRequestDto { Name = new string('a', 101), Description = "Valid description" };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_When_Description_Exceeds_Max_Length_Should_Fail()
    {
        // Arrange
        var request = new RoleRequestDto { Name = "Valid Name", Description = new string('a', 501) };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_When_Name_At_Max_Length_Should_Pass()
    {
        // Arrange
        var request = new RoleRequestDto { Name = new string('a', 100), Description = "Valid description" };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_When_Description_At_Max_Length_Should_Pass()
    {
        // Arrange
        var request = new RoleRequestDto { Name = "Valid Name", Description = new string('a', 500) };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }
}
