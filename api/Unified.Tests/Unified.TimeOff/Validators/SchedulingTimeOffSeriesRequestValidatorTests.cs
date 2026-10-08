using FluentValidation.TestHelper;
using Unified.Calendar.Services;
using Unified.TimeOff.Models.Scheduling;
using Unified.TimeOff.Validators;

namespace Unified.Tests.TimeOff.Validators;

public sealed class SchedulingTimeOffSeriesRequestValidatorTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 1, 16, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserA = new("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Validate_WhenRequestIsValid_HasNoErrorsAndUsesBoundedOptions()
    {
        var recurrence = new FakeRecurrenceRuleValidator();

        var result = await CreateValidator(recurrence)
            .TestValidateAsync(CreateRequest(), cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldNotHaveAnyValidationErrors();
        Assert.Equal(1, recurrence.CallCount);
        Assert.True(recurrence.LastOptions!.RequireBoundedRule);
        Assert.Equal(400, recurrence.LastOptions.MaximumOccurrences);
        Assert.Equal(TimeSpan.FromDays(365), recurrence.LastOptions.MaximumDuration);
    }

    [Fact]
    public async Task Validate_WhenRecurrenceRuleIsEmpty_HasErrorAndSkipsRuleValidator()
    {
        var recurrence = new FakeRecurrenceRuleValidator();

        var result = await CreateValidator(recurrence)
            .TestValidateAsync(
                CreateRequest() with
                {
                    RecurrenceRule = "",
                },
                cancellationToken: TestContext.Current.CancellationToken
            );

        result.ShouldHaveValidationErrorFor(x => x.RecurrenceRule);
        Assert.Equal(0, recurrence.CallCount);
    }

    [Fact]
    public async Task Validate_WhenRuleValidatorReturnsErrors_AddsThemToRecurrenceRule()
    {
        var recurrence = new FakeRecurrenceRuleValidator { Result = RecurrenceValidationResult.Failure("too many") };

        var result = await CreateValidator(recurrence)
            .TestValidateAsync(CreateRequest(), cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldHaveValidationErrorFor(x => x.RecurrenceRule).WithErrorMessage("too many");
    }

    [Fact]
    public async Task Validate_WhenRuleValidatorThrowsArgumentException_AddsInvalidRuleError()
    {
        var recurrence = new FakeRecurrenceRuleValidator { Exception = new ArgumentException("bad rule") };

        var result = await CreateValidator(recurrence)
            .TestValidateAsync(CreateRequest(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains(
            result.Errors,
            e => e.PropertyName == "RecurrenceRule" && e.ErrorMessage.StartsWith("The recurrence rule is invalid:")
        );
    }

    [Fact]
    public async Task Validate_WhenEndIsMissing_HasEndAtUtcError()
    {
        var result = await CreateValidator()
            .TestValidateAsync(
                CreateRequest() with
                {
                    EndAtUtc = null,
                },
                cancellationToken: TestContext.Current.CancellationToken
            );

        result.ShouldHaveValidationErrorFor(x => x.EndAtUtc);
    }

    [Fact]
    public async Task Validate_WhenStartIsNotBeforeEnd_HasStartError()
    {
        var result = await CreateValidator()
            .TestValidateAsync(
                CreateRequest() with
                {
                    EndAtUtc = Start,
                },
                cancellationToken: TestContext.Current.CancellationToken
            );

        result.ShouldHaveValidationErrorFor(x => x.StartAtUtc);
    }

    [Fact]
    public async Task Validate_WhenLeaveTypeIdIsNotPositive_HasLeaveTypeIdError()
    {
        var result = await CreateValidator()
            .TestValidateAsync(
                CreateRequest() with
                {
                    LeaveTypeId = 0,
                },
                cancellationToken: TestContext.Current.CancellationToken
            );

        result.ShouldHaveValidationErrorFor(x => x.LeaveTypeId);
    }

    [Fact]
    public async Task Validate_WhenTimeZoneIsInvalid_HasTimeZoneError()
    {
        var result = await CreateValidator()
            .TestValidateAsync(
                CreateRequest() with
                {
                    TimeZoneId = "Not/AZone",
                },
                cancellationToken: TestContext.Current.CancellationToken
            );

        result.ShouldHaveValidationErrorFor(x => x.TimeZoneId);
    }

    [Fact]
    public async Task Validate_WhenUsersEmptyOrDuplicated_HasUserIdsError()
    {
        var validator = CreateValidator();

        var empty = await validator.TestValidateAsync(
            CreateRequest() with
            {
                UserIds = [],
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        var duplicated = await validator.TestValidateAsync(
            CreateRequest() with
            {
                UserIds = [UserA, UserA],
            },
            cancellationToken: TestContext.Current.CancellationToken
        );

        empty.ShouldHaveValidationErrorFor(x => x.UserIds);
        duplicated.ShouldHaveValidationErrorFor(x => x.UserIds).WithErrorMessage("Users must be unique.");
    }

    [Fact]
    public async Task Validate_WhenTitleTooLongOrLocationInvalid_HasErrors()
    {
        var result = await CreateValidator()
            .TestValidateAsync(
                CreateRequest() with
                {
                    Title = new string('a', 201),
                    LocationId = 0,
                },
                cancellationToken: TestContext.Current.CancellationToken
            );

        result.ShouldHaveValidationErrorFor(x => x.Title);
        result.ShouldHaveValidationErrorFor(x => x.LocationId);
    }

    private static SchedulingTimeOffSeriesRequestValidator CreateValidator(
        FakeRecurrenceRuleValidator? recurrence = null
    ) => new(recurrence ?? new FakeRecurrenceRuleValidator());

    private static SchedulingTimeOffSeriesRequest CreateRequest() =>
        new()
        {
            Title = "Series",
            RecurrenceRule = "FREQ=DAILY;COUNT=3",
            StartAtUtc = Start,
            EndAtUtc = Start.AddHours(8),
            TimeZoneId = "America/Vancouver",
            LocationId = 5,
            LeaveTypeId = 1,
            UserIds = [UserA],
        };

    private sealed class FakeRecurrenceRuleValidator : IRecurrenceRuleValidator
    {
        public RecurrenceValidationResult Result { get; init; } = RecurrenceValidationResult.Success;

        public Exception? Exception { get; init; }

        public int CallCount { get; private set; }

        public RecurrenceValidationOptions? LastOptions { get; private set; }

        public RecurrenceValidationResult Validate(
            string recurrenceRule,
            DateTimeOffset seriesStartAtUtc,
            DateTimeOffset? seriesEndAtUtc,
            string? timeZoneId,
            RecurrenceValidationOptions options
        )
        {
            CallCount++;
            LastOptions = options;
            if (Exception is not null)
                throw Exception;
            return Result;
        }
    }
}
