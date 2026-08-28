using FluentValidation.TestHelper;
using Unified.Audit.Models;
using Unified.Audit.Validators;

namespace Unified.Tests.Unified.Audit.Validators;

public class AuditHistoryQueryParamsValidatorTests
{
    private readonly AuditHistoryQueryParamsValidator _validator = new();

    [Fact]
    public void Validate_When_All_Fields_Empty_Should_Pass()
    {
        var result = _validator.TestValidate(new AuditHistoryQueryParams());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_When_Page_Is_Zero_Should_Fail()
    {
        var result = _validator.TestValidate(new AuditHistoryQueryParams { Page = 0 });

        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Fact]
    public void Validate_When_PageSize_Is_Zero_Should_Fail()
    {
        var result = _validator.TestValidate(new AuditHistoryQueryParams { PageSize = 0 });

        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Validate_When_PageSize_Exceeds_100_Should_Fail()
    {
        var result = _validator.TestValidate(new AuditHistoryQueryParams { PageSize = 101 });

        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Validate_When_PageSize_Is_100_Should_Pass()
    {
        var result = _validator.TestValidate(new AuditHistoryQueryParams { PageSize = 100 });

        result.ShouldNotHaveValidationErrorFor(x => x.PageSize);
    }

    [Theory]
    [InlineData("Added")]
    [InlineData("modified")]
    [InlineData("DELETED")]
    public void Validate_When_Action_Is_Known_Value_Should_Pass(string action)
    {
        var result = _validator.TestValidate(new AuditHistoryQueryParams { Action = action });

        result.ShouldNotHaveValidationErrorFor(x => x.Action);
    }

    [Fact]
    public void Validate_When_Action_Is_Unknown_Should_Fail()
    {
        var result = _validator.TestValidate(new AuditHistoryQueryParams { Action = "Renamed" });

        result.ShouldHaveValidationErrorFor(x => x.Action);
    }

    [Theory]
    [InlineData("asc")]
    [InlineData("DESC")]
    public void Validate_When_SortDirection_Is_Known_Value_Should_Pass(string direction)
    {
        var result = _validator.TestValidate(new AuditHistoryQueryParams { SortDirection = direction });

        result.ShouldNotHaveValidationErrorFor(x => x.SortDirection);
    }

    [Fact]
    public void Validate_When_SortDirection_Is_Unknown_Should_Fail()
    {
        var result = _validator.TestValidate(new AuditHistoryQueryParams { SortDirection = "sideways" });

        result.ShouldHaveValidationErrorFor(x => x.SortDirection);
    }

    [Fact]
    public void Validate_When_From_After_To_Should_Fail()
    {
        var request = new AuditHistoryQueryParams
        {
            From = DateTimeOffset.UtcNow,
            To = DateTimeOffset.UtcNow.AddDays(-1),
        };

        var result = _validator.TestValidate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_When_From_Before_To_Should_Pass()
    {
        var request = new AuditHistoryQueryParams
        {
            From = DateTimeOffset.UtcNow.AddDays(-1),
            To = DateTimeOffset.UtcNow,
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
