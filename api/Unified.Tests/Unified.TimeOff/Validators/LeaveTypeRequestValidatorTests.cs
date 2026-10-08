using FluentValidation.TestHelper;
using Unified.TimeOff.Models;
using Unified.TimeOff.Validators;

namespace Unified.Tests.TimeOff.Validators;

public sealed class LeaveTypeRequestValidatorTests
{
    private static readonly DateTimeOffset Effective = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly LeaveTypeRequestValidator _validator = new();

    [Fact]
    public async Task Validate_WhenRequestIsValid_HasNoErrors()
    {
        var result = await _validator.TestValidateAsync(
            CreateRequest(),
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_WhenNameIsEmpty_HasNameError(string name)
    {
        var result = await _validator.TestValidateAsync(
            CreateRequest() with
            {
                Name = name,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public async Task Validate_WhenNameExceedsMaxLength_HasNameError()
    {
        var result = await _validator.TestValidateAsync(
            CreateRequest() with
            {
                Name = new string('a', 51),
            },
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public async Task Validate_WhenNameAndDescriptionAtMaxLength_HasNoErrors()
    {
        var result = await _validator.TestValidateAsync(
            CreateRequest() with
            {
                Name = new string('a', 50),
                Description = new string('b', 100),
            },
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_WhenDescriptionIsEmpty_HasDescriptionError()
    {
        var result = await _validator.TestValidateAsync(
            CreateRequest() with
            {
                Description = "",
            },
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public async Task Validate_WhenDescriptionExceedsMaxLength_HasDescriptionError()
    {
        var result = await _validator.TestValidateAsync(
            CreateRequest() with
            {
                Description = new string('b', 101),
            },
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_WhenEffectiveDateIsNotBeforeExpiry_HasEffectiveDateError(int expiryOffsetDays)
    {
        var result = await _validator.TestValidateAsync(
            CreateRequest() with
            {
                EffectiveDateUtc = Effective,
                ExpiryDateUtc = Effective.AddDays(expiryOffsetDays),
            },
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(x => x.EffectiveDateUtc);
    }

    [Fact]
    public async Task Validate_WhenEffectiveDateIsBeforeExpiry_HasNoDateError()
    {
        var result = await _validator.TestValidateAsync(
            CreateRequest() with
            {
                EffectiveDateUtc = Effective,
                ExpiryDateUtc = Effective.AddDays(1),
            },
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldNotHaveValidationErrorFor(x => x.EffectiveDateUtc);
    }

    private static LeaveTypeRequest CreateRequest() =>
        new()
        {
            Name = "Vacation",
            Description = "Paid vacation",
            IsPaid = true,
        };
}
