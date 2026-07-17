using FluentValidation.TestHelper;
using Unified.Scheduling.Models;
using Unified.Scheduling.Validators;

namespace Unified.Tests.Scheduling.Validators;

public sealed class AssignmentDefinitionRequestValidatorTests
{
    private readonly AssignmentDefinitionRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenDescriptionIsNull_ShouldPass()
    {
        var request = CreateValidRequest() with { Description = null };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_WhenDescriptionIsEmpty_ShouldPass()
    {
        var request = CreateValidRequest() with { Description = string.Empty };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_WhenDescriptionExceedsMaxLength_ShouldFail()
    {
        var request = CreateValidRequest() with { Description = new string('a', 201) };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    private static AssignmentDefinitionRequest CreateValidRequest() =>
        new()
        {
            LocationId = 1,
            Name = "Assignment type",
            Description = "Description",
            AssignmentCategoryTypeId = 1,
            AssignmentSubCategoryTypeId = 1,
            DefaultCapacity = 1,
            EffectiveDateUtc = DateTimeOffset.UtcNow,
        };
}
