using FluentValidation.TestHelper;
using Unified.Scheduling.Models;
using Unified.Scheduling.Validators;

namespace Unified.Tests.Scheduling.Validators;

public sealed class ShiftRequestValidatorTests
{
    private static readonly Guid UserA = new("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task ShiftSeriesRequestValidator_WhenTimeZoneIdIsInvalid_HasTimeZoneError()
    {
        // Arrange
        var validator = new ShiftSeriesRequestValidator();
        var request = CreateShiftSeriesRequest("Not/AZone");

        // Act
        var result = await validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TimeZoneId);
    }

    [Fact]
    public async Task ShiftEntryRequestValidator_WhenTimeZoneIdIsInvalid_HasTimeZoneError()
    {
        // Arrange
        var validator = new ShiftEntryRequestValidator();
        var request = CreateShiftEntryRequest("Not/AZone");

        // Act
        var result = await validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TimeZoneId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ShiftSeriesRequestValidator_WhenRecurrenceRuleIsMissingOrBlank_HasError(string? recurrenceRule)
    {
        var validator = new ShiftSeriesRequestValidator();
        var request = CreateShiftSeriesRequest() with { RecurrenceRule = recurrenceRule };

        var result = await validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(x => x.RecurrenceRule);
    }

    [Fact]
    public async Task ShiftSeriesRequestValidator_WhenEndAtUtcIsMissing_HasError()
    {
        var validator = new ShiftSeriesRequestValidator();
        var request = CreateShiftSeriesRequest() with { EndAtUtc = null };

        var result = await validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(x => x.EndAtUtc);
    }

    [Fact]
    public async Task ShiftSeriesRequestValidator_WhenEndAtUtcIsNotAfterStartAtUtc_HasError()
    {
        var validator = new ShiftSeriesRequestValidator();
        var request = CreateShiftSeriesRequest() with
        {
            EndAtUtc = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
        };

        var result = await validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(x => x.StartAtUtc);
    }

    private static ShiftSeriesRequest CreateShiftSeriesRequest(string? timeZoneId = null) =>
        new()
        {
            Title = "Series",
            RecurrenceRule = "FREQ=DAILY;COUNT=1",
            StartAtUtc = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            TimeZoneId = timeZoneId,
            UserIds = [UserA],
        };

    private static ShiftEntryRequest CreateShiftEntryRequest(string? timeZoneId = null) =>
        new()
        {
            Title = "Entry",
            StartAtUtc = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            TimeZoneId = timeZoneId,
            UserIds = [UserA],
        };
}
