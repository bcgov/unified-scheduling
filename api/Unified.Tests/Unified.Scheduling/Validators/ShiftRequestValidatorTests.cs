using FluentValidation.TestHelper;
using Unified.Db.Models.Calendar;
using Unified.Scheduling.Models;
using Unified.Scheduling.Validators;

namespace Unified.Tests.Scheduling.Validators;

public sealed class ShiftRequestValidatorTests
{
    private static readonly Guid UserA = new("11111111-1111-1111-1111-111111111111");

    [Theory]
    [InlineData(CalendarEventStatusTypeCodes.Active)]
    [InlineData(CalendarEventStatusTypeCodes.Cancelled)]
    public async Task ShiftSeriesRequestValidator_WhenStatusIsNotDraft_HasStatusError(string statusTypeCode)
    {
        // Arrange
        var validator = new ShiftSeriesRequestValidator();
        var request = CreateShiftSeriesRequest(statusTypeCode);

        // Act
        var result = await validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.StatusTypeCode);
    }

    [Theory]
    [InlineData(CalendarEventStatusTypeCodes.Active)]
    [InlineData(CalendarEventStatusTypeCodes.Cancelled)]
    public async Task ShiftEntryRequestValidator_WhenStatusIsNotDraft_HasStatusError(string statusTypeCode)
    {
        // Arrange
        var validator = new ShiftEntryRequestValidator();
        var request = CreateShiftEntryRequest(statusTypeCode);

        // Act
        var result = await validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.StatusTypeCode);
    }

    private static ShiftSeriesRequest CreateShiftSeriesRequest(string statusTypeCode) =>
        new()
        {
            Title = "Series",
            RecurrenceRule = "FREQ=DAILY;COUNT=1",
            StartAtUtc = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            StatusTypeCode = statusTypeCode,
            UserIds = [UserA],
        };

    private static ShiftEntryRequest CreateShiftEntryRequest(string statusTypeCode) =>
        new()
        {
            Title = "Entry",
            StartAtUtc = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            StatusTypeCode = statusTypeCode,
            UserIds = [UserA],
        };
}
