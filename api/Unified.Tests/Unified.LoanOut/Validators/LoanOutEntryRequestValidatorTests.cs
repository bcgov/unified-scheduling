using FluentValidation.TestHelper;
using Unified.LoanOut.Models;
using Unified.LoanOut.Validators;

namespace Unified.Tests.LoanOut.Validators;

public sealed class LoanOutEntryRequestValidatorTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 1, 16, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserA = new("11111111-1111-1111-1111-111111111111");

    private readonly LoanOutRequestValidator _validator = new();

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
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_WhenLoanOutLocationIdIsNotPositive_HasError(int locationId)
    {
        var result = await _validator.TestValidateAsync(
            CreateRequest() with
            {
                LoanOutLocationId = locationId,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(request => request.LoanOutLocationId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_WhenLoanOutSeriesIdIsNotPositive_HasError(int seriesId)
    {
        var result = await _validator.TestValidateAsync(
            CreateRequest() with
            {
                LoanOutSeriesId = seriesId,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(request => request.LoanOutSeriesId);
    }

    [Fact]
    public async Task Validate_WhenUsersAreEmptyOrDuplicated_HasUserIdsError()
    {
        var empty = await _validator.TestValidateAsync(
            CreateRequest() with
            {
                UserIds = [],
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        var duplicated = await _validator.TestValidateAsync(
            CreateRequest() with
            {
                UserIds = [UserA, UserA],
            },
            cancellationToken: TestContext.Current.CancellationToken
        );

        empty.ShouldHaveValidationErrorFor(request => request.UserIds);
        duplicated.ShouldHaveValidationErrorFor(request => request.UserIds).WithErrorMessage("Users must be unique.");
    }

    [Fact]
    public async Task Validate_WhenStartIsNotBeforeEnd_HasStartError()
    {
        var result = await _validator.TestValidateAsync(
            CreateRequest() with
            {
                EndAtUtc = Start,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(request => request.StartAtUtc);
    }

    [Fact]
    public async Task Validate_WhenTimeZoneIsInvalid_HasTimeZoneError()
    {
        var result = await _validator.TestValidateAsync(
            CreateRequest() with
            {
                TimeZoneId = "Not/AZone",
            },
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(request => request.TimeZoneId);
    }

    private static LoanOutEntryRequest CreateRequest() =>
        new()
        {
            Title = "Loan out",
            StartAtUtc = Start,
            EndAtUtc = Start.AddHours(8),
            TimeZoneId = "America/Vancouver",
            LocationId = 5,
            LoanOutLocationId = 10,
            UserIds = [UserA],
        };
}
