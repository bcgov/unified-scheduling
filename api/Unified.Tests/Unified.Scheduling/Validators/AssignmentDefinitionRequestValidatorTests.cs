using Unified.Scheduling.Models;
using Unified.Scheduling.Validators;

namespace Unified.Tests.Scheduling.Validators;

public sealed class AssignmentDefinitionRequestValidatorTests
{
    [Fact]
    public async Task Category_and_subcategory_are_required()
    {
        var result = await new AssignmentDefinitionRequestValidator().ValidateAsync(
            new AssignmentDefinitionRequest
            {
                LocationId = 1,
                Name = "Template",
                DefaultCapacity = 1,
                EffectiveDateUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            },
            TestContext.Current.CancellationToken
        );
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AssignmentDefinitionRequest.CategoryId));
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(AssignmentDefinitionRequest.SubCategoryId)
        );
    }

    [Fact]
    public async Task Expiry_on_same_UTC_business_date_is_rejected()
    {
        var request = new AssignmentDefinitionRequest
        {
            LocationId = 1,
            Name = "Template",
            CategoryId = 2,
            SubCategoryId = 3,
            DefaultCapacity = 1,
            EffectiveDateUtc = DateTimeOffset.Parse("2026-08-21T00:00:00Z"),
            ExpiryDateUtc = DateTimeOffset.Parse("2026-08-21T23:00:00Z"),
        };

        var result = await new AssignmentDefinitionRequestValidator().ValidateAsync(
            request,
            TestContext.Current.CancellationToken
        );

        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(AssignmentDefinitionRequest.ExpiryDateUtc)
        );
    }
}
