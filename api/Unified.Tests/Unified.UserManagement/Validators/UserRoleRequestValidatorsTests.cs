using FluentValidation.TestHelper;
using Unified.UserManagement.Models;
using Unified.UserManagement.Validators;

namespace Unified.Tests.UserManagement.Validators;

public class UserRoleRequestValidatorsTests
{
    private readonly AssignUserRoleRequestValidator _assignValidator = new();
    private readonly ExpireUserRoleRequestValidator _expireValidator = new();

    [Fact]
    public void AssignValidator_When_Request_Is_Valid_Should_Pass()
    {
        // Arrange
        var request = new AssignUserRoleRequestDto
        {
            RoleId = 10,
            EffectiveDate = "2026-01-10",
            ExpiryDate = "2026-01-20",
        };

        // Act
        var result = _assignValidator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AssignValidator_When_RoleId_Is_Invalid_Should_Fail()
    {
        // Arrange
        var request = new AssignUserRoleRequestDto { RoleId = 0, EffectiveDate = "2026-01-10" };

        // Act
        var result = _assignValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.RoleId);
    }

    [Fact]
    public void AssignValidator_When_ExpiryDate_Before_EffectiveDate_Should_Fail()
    {
        // Arrange
        var request = new AssignUserRoleRequestDto
        {
            RoleId = 10,
            EffectiveDate = "2026-01-10",
            ExpiryDate = "2026-01-09",
        };

        // Act
        var result = _assignValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ExpiryDate);
    }

    [Fact]
    public void ExpireValidator_When_ExpiryReason_Is_Empty_Should_Fail()
    {
        // Arrange
        var request = new ExpireUserRoleRequestDto { RoleId = 10, ExpiryReason = string.Empty };

        // Act
        var result = _expireValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ExpiryReason);
    }


    [Fact]
    public void ExpireValidator_When_Request_Is_Valid_Should_Pass()
    {
        // Arrange
        var request = new ExpireUserRoleRequestDto
        {
            RoleId = 10,
            ExpiryReason = "OPERDEMAND",
        };

        // Act
        var result = _expireValidator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
