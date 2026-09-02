using FluentValidation.TestHelper;
using Unified.Audit.Models;
using Unified.Audit.Validators;

namespace Unified.Tests.Unified.Audit.Validators;

public class AuditHistoryQueryParamsValidatorTests
{
    private readonly AuditHistoryQueryParamsValidator _validator = new();
    private static readonly DateTimeOffset TodayStart = new(DateTime.UtcNow.Date, TimeSpan.Zero);
    private static readonly DateTimeOffset TodayEnd = TodayStart.AddDays(1).AddTicks(-1);

    private static AuditHistoryQueryParams BuildParams(
        string? entityType = "User",
        string? entityPK = null,
        string? action = null,
        string? sortDirection = null,
        int? page = null,
        int? pageSize = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null
    ) =>
        new()
        {
            EntityType = entityType!,
            EntityPK = entityPK,
            Action = action,
            SortDirection = sortDirection,
            Page = page,
            PageSize = pageSize,
            From = from ?? TodayStart,
            To = to ?? TodayEnd,
        };

    [Fact]
    public void Validate_When_Only_EntityType_Provided_Should_Pass()
    {
        var result = _validator.TestValidate(BuildParams());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_When_EntityType_Missing_Should_Fail()
    {
        var result = _validator.TestValidate(BuildParams(entityType: null));

        result.ShouldHaveValidationErrorFor(x => x.EntityType);
    }

    [Fact]
    public void Validate_When_Page_Is_Zero_Should_Fail()
    {
        var result = _validator.TestValidate(BuildParams(page: 0));

        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Fact]
    public void Validate_When_PageSize_Is_Zero_Should_Fail()
    {
        var result = _validator.TestValidate(BuildParams(pageSize: 0));

        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Validate_When_EntityPK_Provided_With_EntityType_Should_Pass()
    {
        var result = _validator.TestValidate(BuildParams(entityPK: "1"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_When_PageSize_Exceeds_100_Should_Fail()
    {
        var result = _validator.TestValidate(BuildParams(pageSize: 101));

        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Validate_When_PageSize_Is_100_Should_Pass()
    {
        var result = _validator.TestValidate(BuildParams(pageSize: 100));

        result.ShouldNotHaveValidationErrorFor(x => x.PageSize);
    }

    [Theory]
    [InlineData("Added")]
    [InlineData("modified")]
    [InlineData("DELETED")]
    public void Validate_When_Action_Is_Known_Value_Should_Pass(string action)
    {
        var result = _validator.TestValidate(BuildParams(action: action));

        result.ShouldNotHaveValidationErrorFor(x => x.Action);
    }

    [Fact]
    public void Validate_When_Action_Is_Unknown_Should_Fail()
    {
        var result = _validator.TestValidate(BuildParams(action: "Renamed"));

        result.ShouldHaveValidationErrorFor(x => x.Action);
    }

    [Theory]
    [InlineData("asc")]
    [InlineData("DESC")]
    public void Validate_When_SortDirection_Is_Known_Value_Should_Pass(string direction)
    {
        var result = _validator.TestValidate(BuildParams(sortDirection: direction));

        result.ShouldNotHaveValidationErrorFor(x => x.SortDirection);
    }

    [Fact]
    public void Validate_When_SortDirection_Is_Unknown_Should_Fail()
    {
        var result = _validator.TestValidate(BuildParams(sortDirection: "sideways"));

        result.ShouldHaveValidationErrorFor(x => x.SortDirection);
    }

    [Fact]
    public void Validate_When_From_After_To_Should_Fail()
    {
        var request = BuildParams(from: DateTimeOffset.UtcNow, to: DateTimeOffset.UtcNow.AddDays(-1));

        var result = _validator.TestValidate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_When_From_Before_To_Should_Pass()
    {
        var request = BuildParams(from: DateTimeOffset.UtcNow.AddDays(-1), to: DateTimeOffset.UtcNow);

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_When_From_Missing_Should_Fail()
    {
        var result = _validator.TestValidate(
            new AuditHistoryQueryParams
            {
                EntityType = "User",
                From = default,
                To = TodayEnd,
            }
        );

        result.ShouldHaveValidationErrorFor(x => x.From);
    }

    [Fact]
    public void Validate_When_To_Missing_Should_Fail()
    {
        var result = _validator.TestValidate(
            new AuditHistoryQueryParams
            {
                EntityType = "User",
                From = TodayStart,
                To = default,
            }
        );

        result.ShouldHaveValidationErrorFor(x => x.To);
    }
}
