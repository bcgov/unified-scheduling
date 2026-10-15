using FluentValidation.TestHelper;
using Unified.Scheduling.Models;
using Unified.Scheduling.Validators;

namespace Unified.Tests.Scheduling.Validators;

public sealed class ScheduleDistributionRequestValidatorTests
{
    private readonly ScheduleDistributionRequestValidator _validator = new();

    [Theory]
    [MemberData(nameof(ValidPeriodRanges))]
    public async Task ValidateAsync_WhenPeriodRangeMatchesCalendar_HasNoErrors(
        string period,
        DateOnly startDate,
        DateOnly endDate
    )
    {
        var request = CreateRequest(period, startDate, endDate);

        var result = await _validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldNotHaveAnyValidationErrors();
    }

    public static TheoryData<string, DateOnly, DateOnly> ValidPeriodRanges =>
        new()
        {
            { "day", new DateOnly(2026, 8, 31), new DateOnly(2026, 9, 1) },
            { "week", new DateOnly(2026, 8, 31), new DateOnly(2026, 9, 7) },
            { "work-week", new DateOnly(2026, 12, 28), new DateOnly(2027, 1, 2) },
        };

    [Theory]
    [MemberData(nameof(InvalidPeriodRanges))]
    public async Task ValidateAsync_WhenPeriodRangeDoesNotMatchCalendar_HasContextError(
        string period,
        DateOnly startDate,
        DateOnly endDate
    )
    {
        var result = await _validator.TestValidateAsync(
            CreateRequest(period, startDate, endDate),
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(candidate => candidate.Context);
    }

    public static TheoryData<string, DateOnly, DateOnly> InvalidPeriodRanges =>
        new()
        {
            { "day", new DateOnly(2026, 8, 24), new DateOnly(2026, 8, 26) },
            { "week", new DateOnly(2026, 8, 25), new DateOnly(2026, 9, 1) },
            { "week", new DateOnly(2026, 8, 24), new DateOnly(2026, 8, 30) },
            { "work-week", new DateOnly(2026, 12, 29), new DateOnly(2027, 1, 3) },
            { "work-week", new DateOnly(2026, 12, 28), new DateOnly(2027, 1, 3) },
        };

    [Fact]
    public async Task ValidateAsync_WhenPeriodIsMonth_HasPeriodError()
    {
        var request = CreateRequest("month", new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 1));

        var result = await _validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(candidate => candidate.Context!.Period);
    }

    [Theory]
    [InlineData("schedule", "DAY")]
    [InlineData("ASSIGNMENTS", "work-WEEK")]
    public async Task ValidateAsync_WhenViewAndPeriodUseDifferentCase_HasNoErrors(string view, string period)
    {
        var startDate = new DateOnly(2026, 8, 24);
        var endDate = startDate.AddDays(string.Equals(period, "DAY", StringComparison.OrdinalIgnoreCase) ? 1 : 5);
        var request = CreateRequest(period, startDate, endDate) with
        {
            Context = CreateRequest(period, startDate, endDate).Context! with { View = view },
        };

        var result = await _validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("month")]
    [InlineData("")]
    [InlineData("unknown")]
    public async Task ValidateAsync_WhenViewIsUnsupported_HasViewError(string view)
    {
        var request = CreateRequest("day", new DateOnly(2026, 8, 24), new DateOnly(2026, 8, 25));
        request = request with { Context = request.Context! with { View = view } };

        var result = await _validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(candidate => candidate.Context!.View);
    }

    [Fact]
    public async Task ValidateAsync_WhenContextIsMissing_HasContextError()
    {
        var result = await _validator.TestValidateAsync(
            new ScheduleDistributionRequest { RecipientUserIds = [Guid.NewGuid()] },
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(candidate => candidate.Context);
    }

    [Theory]
    [MemberData(nameof(InvalidRecipientCases))]
    public async Task ValidateAsync_WhenRecipientsAreInvalid_HasRecipientError(IReadOnlyCollection<Guid>? recipients)
    {
        var request = CreateRequest("day", new DateOnly(2026, 8, 24), new DateOnly(2026, 8, 25)) with
        {
            RecipientUserIds = recipients!,
        };

        var result = await _validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(candidate => candidate.RecipientUserIds);
    }

    public static TheoryData<IReadOnlyCollection<Guid>?> InvalidRecipientCases
    {
        get
        {
            var recipientId = Guid.NewGuid();
            return new TheoryData<IReadOnlyCollection<Guid>?>
            {
                { null },
                { [] },
                { [Guid.Empty] },
                { [recipientId, recipientId] },
                { Enumerable.Range(0, 501).Select(_ => Guid.NewGuid()).ToArray() },
            };
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ValidateAsync_WhenLocationIsNotPositive_HasLocationError(int locationId)
    {
        var request = CreateRequest("day", new DateOnly(2026, 8, 24), new DateOnly(2026, 8, 25));
        request = request with { Context = request.Context! with { LocationId = locationId } };

        var result = await _validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(candidate => candidate.Context!.LocationId);
    }

    [Fact]
    public async Task ValidateAsync_WhenViewIsNull_HasViewError()
    {
        var request = CreateRequest("day", new DateOnly(2026, 8, 24), new DateOnly(2026, 8, 25));
        request = request with { Context = request.Context! with { View = null! } };

        var result = await _validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(candidate => candidate.Context!.View);
    }

    [Fact]
    public async Task ValidateAsync_WhenPeriodIsNull_HasPeriodError()
    {
        var request = CreateRequest("day", new DateOnly(2026, 8, 24), new DateOnly(2026, 8, 25));
        request = request with { Context = request.Context! with { Period = null! } };

        var result = await _validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(candidate => candidate.Context!.Period);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ValidateAsync_WhenExclusiveRangeIsEmptyOrReversed_HasEndDateError(int endOffsetDays)
    {
        var startDate = new DateOnly(2026, 8, 24);
        var request = CreateRequest("day", startDate, startDate.AddDays(endOffsetDays));

        var result = await _validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(candidate => candidate.Context!.EndDate);
    }

    private static ScheduleDistributionRequest CreateRequest(string period, DateOnly startDate, DateOnly endDate) =>
        new()
        {
            Context = new ScheduleDistributionContext
            {
                View = "schedule",
                LocationId = 7,
                StartDate = startDate,
                EndDate = endDate,
                Period = period,
            },
            RecipientUserIds = [Guid.NewGuid()],
        };
}
