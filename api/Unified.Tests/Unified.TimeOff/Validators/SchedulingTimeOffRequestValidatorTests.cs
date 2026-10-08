using FluentValidation.TestHelper;
using Unified.TimeOff.Models.Scheduling;
using Unified.TimeOff.Validators;

namespace Unified.Tests.TimeOff.Validators;

public sealed class SchedulingTimeOffRequestValidatorTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 1, 16, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserA = new("11111111-1111-1111-1111-111111111111");

    private readonly SchedulingTimeOffRequestValidator _validator = new();

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
    public async Task Validate_WhenLeaveTypeIdIsNotPositive_HasLeaveTypeIdError(int leaveTypeId)
    {
        var result = await _validator.TestValidateAsync(
            CreateRequest() with
            {
                LeaveTypeId = leaveTypeId,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(x => x.LeaveTypeId);
    }

    [Fact]
    public async Task Validate_WhenTitleIsEmpty_HasTitleError()
    {
        var result = await _validator.TestValidateAsync(
            CreateRequest() with
            {
                Title = "",
            },
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(x => x.Title);
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

        result.ShouldHaveValidationErrorFor(x => x.TimeZoneId);
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

        result.ShouldHaveValidationErrorFor(x => x.StartAtUtc);
    }

    [Fact]
    public async Task Validate_WhenSeriesStartIsNotBeforeSeriesEnd_HasSeriesStartError()
    {
        var result = await _validator.TestValidateAsync(
            CreateRequest() with
            {
                SeriesStartAtUtc = Start,
                SeriesEndAtUtc = Start,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(x => x.SeriesStartAtUtc);
    }

    [Fact]
    public async Task Validate_WhenLocationIdIsNotPositive_HasLocationError()
    {
        var result = await _validator.TestValidateAsync(
            CreateRequest() with
            {
                LocationId = 0,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(x => x.LocationId);
    }

    [Fact]
    public async Task Validate_WhenUsersEmpty_HasUserIdsError()
    {
        var result = await _validator.TestValidateAsync(
            CreateRequest() with
            {
                UserIds = [],
            },
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(x => x.UserIds);
    }

    [Fact]
    public async Task Validate_WhenUsersDuplicated_HasUniqueUsersError()
    {
        var result = await _validator.TestValidateAsync(
            CreateRequest() with
            {
                UserIds = [UserA, UserA],
            },
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(x => x.UserIds).WithErrorMessage("Users must be unique.");
    }

    [Fact]
    public async Task Validate_WhenUserIdIsEmptyGuid_HasUserIdError()
    {
        var result = await _validator.TestValidateAsync(
            CreateRequest() with
            {
                UserIds = [Guid.Empty],
            },
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Contains(result.Errors, e => e.PropertyName.StartsWith("UserIds"));
    }

    [Fact]
    public async Task Validate_WhenTextFieldsExceedMaxLength_HasErrors()
    {
        var result = await _validator.TestValidateAsync(
            CreateRequest() with
            {
                Title = new string('a', 201),
                Description = new string('b', 2001),
                Notes = new string('c', 4001),
                Color = new string('d', 101),
            },
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(x => x.Title);
        result.ShouldHaveValidationErrorFor(x => x.Description);
        result.ShouldHaveValidationErrorFor(x => x.Notes);
        result.ShouldHaveValidationErrorFor(x => x.Color);
    }

    private static SchedulingTimeOffEntryRequest CreateRequest() =>
        new()
        {
            Title = "Leave",
            StartAtUtc = Start,
            EndAtUtc = Start.AddHours(8),
            TimeZoneId = "America/Vancouver",
            LocationId = 5,
            LeaveTypeId = 1,
            UserIds = [UserA],
        };
}
