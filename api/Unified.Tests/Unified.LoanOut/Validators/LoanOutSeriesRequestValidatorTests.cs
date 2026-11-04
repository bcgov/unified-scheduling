using FluentValidation.TestHelper;
using Unified.Calendar.Services;
using Unified.LoanOut.Models;
using Unified.LoanOut.Validators;

namespace Unified.Tests.LoanOut.Validators;

public sealed class LoanOutSeriesRequestValidatorTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 1, 16, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserA = new("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Validate_WhenRequestIsValid_UsesBoundedRecurrenceOptions()
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
    public async Task Validate_WhenRecurrenceIsInvalid_AddsRecurrenceError()
    {
        var validator = CreateValidator(
            new FakeRecurrenceRuleValidator
            {
                Result = RecurrenceValidationResult.Failure("RRULE must be bounded by COUNT or UNTIL."),
            }
        );

        var result = await validator.TestValidateAsync(
            CreateRequest(),
            cancellationToken: TestContext.Current.CancellationToken
        );

        result
            .ShouldHaveValidationErrorFor(request => request.RecurrenceRule)
            .WithErrorMessage("RRULE must be bounded by COUNT or UNTIL.");
    }

    [Fact]
    public async Task Validate_WhenRecurrenceValidatorThrows_AddsInvalidRuleError()
    {
        var validator = CreateValidator(
            new FakeRecurrenceRuleValidator { Exception = new ArgumentException("bad rule") }
        );

        var result = await validator.TestValidateAsync(
            CreateRequest(),
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Contains(
            result.Errors,
            error =>
                error.PropertyName == nameof(LoanOutSeriesRequest.RecurrenceRule)
                && error.ErrorMessage.StartsWith("The recurrence rule is invalid:")
        );
    }

    [Fact]
    public async Task Validate_WhenLocationIsNotPositive_HasLocationError()
    {
        var result = await CreateValidator()
            .TestValidateAsync(
                CreateRequest() with
                {
                    LoanOutLocationId = 0,
                },
                cancellationToken: TestContext.Current.CancellationToken
            );

        result.ShouldHaveValidationErrorFor(request => request.LoanOutLocationId);
    }

    [Fact]
    public async Task Validate_WhenUsersAreEmptyOrDuplicated_HasUserIdsError()
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

        empty.ShouldHaveValidationErrorFor(request => request.UserIds);
        duplicated.ShouldHaveValidationErrorFor(request => request.UserIds).WithErrorMessage("Users must be unique.");
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

        result.ShouldHaveValidationErrorFor(request => request.EndAtUtc);
    }

    private static LoanOutSeriesRequestValidator CreateValidator(FakeRecurrenceRuleValidator? recurrence = null) =>
        new(recurrence ?? new FakeRecurrenceRuleValidator());

    private static LoanOutSeriesRequest CreateRequest() =>
        new()
        {
            Title = "Loan out series",
            RecurrenceRule = "FREQ=DAILY;COUNT=3",
            StartAtUtc = Start,
            EndAtUtc = Start.AddHours(8),
            TimeZoneId = "America/Vancouver",
            LocationId = 5,
            LoanOutLocationId = 10,
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
