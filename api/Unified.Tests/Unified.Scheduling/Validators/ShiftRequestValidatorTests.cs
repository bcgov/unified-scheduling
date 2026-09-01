using FluentValidation.TestHelper;
using Unified.Db.Models.Calendar;
using Unified.Scheduling.Models;
using Unified.Scheduling.Validators;

namespace Unified.Tests.Scheduling.Validators;

public sealed class ShiftRequestValidatorTests
{
    private static readonly Guid UserA = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserB = new("22222222-2222-2222-2222-222222222222");

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

    [Fact]
    public async Task ShiftSeriesRequestValidator_WhenAssignedUsersContainUserNotOnShift_Fails()
    {
        var validator = new ShiftSeriesRequestValidator();
        var request = CreateShiftSeriesRequest(assignmentSeriesIds: [1], assignedUserIds: [UserB]);

        var result = await validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(x => x.AssignedUserIds);
    }

    [Fact]
    public async Task ShiftSeriesRequestValidator_WhenAssignmentSeriesIdsProvidedWithoutAssignedUserIds_Fails()
    {
        var validator = new ShiftSeriesRequestValidator();
        var request = CreateShiftSeriesRequest(assignmentSeriesIds: [1]);

        var result = await validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(x => x.AssignedUserIds);
    }

    [Fact]
    public async Task ShiftSeriesRequestValidator_WhenAssignedUserIdsProvidedWithoutAssignmentSeriesIds_Fails()
    {
        var validator = new ShiftSeriesRequestValidator();
        var request = CreateShiftSeriesRequest(assignmentSeriesIds: null, assignedUserIds: [UserA]);

        var result = await validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(x => x.AssignedUserIds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ShiftSeriesRequestValidator_WhenLocationIdIsMissingOrInvalid_HasLocationError(int? locationId)
    {
        var validator = new ShiftSeriesRequestValidator();
        var request = CreateShiftSeriesRequest(locationId: locationId);

        var result = await validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(x => x.LocationId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ShiftEntryRequestValidator_WhenLocationIdIsMissingOrInvalid_HasLocationError(int? locationId)
    {
        var validator = new ShiftEntryRequestValidator();
        var request = CreateShiftEntryRequest(locationId: locationId);

        var result = await validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(x => x.LocationId);
    }

    private static ShiftSeriesRequest CreateShiftSeriesRequest(
        string? statusTypeCode = null,
        IReadOnlyCollection<int>? assignmentSeriesIds = null,
        IReadOnlyCollection<Guid>? assignedUserIds = null,
        int? locationId = 5
    ) =>
        new()
        {
            Title = "Series",
            RecurrenceRule = "FREQ=DAILY;COUNT=1",
            StartAtUtc = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            StatusTypeCode = statusTypeCode,
            LocationId = locationId,
            UserIds = [UserA],
            AssignmentSeriesIds = assignmentSeriesIds,
            AssignedUserIds = assignedUserIds,
        };

    private static ShiftEntryRequest CreateShiftEntryRequest(string? statusTypeCode = null, int? locationId = 5) =>
        new()
        {
            Title = "Entry",
            StartAtUtc = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            StatusTypeCode = statusTypeCode,
            LocationId = locationId,
            UserIds = [UserA],
        };
}
