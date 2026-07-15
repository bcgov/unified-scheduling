using FluentValidation.TestHelper;
using Unified.Scheduling.Models;
using Unified.Scheduling.Validators;

namespace Unified.Tests.Scheduling.Validators;

public sealed class AssignmentRequestValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task AssignmentSeriesRequestValidator_WhenLocationIdIsMissingOrInvalid_HasLocationError(int? locationId)
    {
        var validator = new AssignmentSeriesRequestValidator();
        var request = CreateAssignmentSeriesRequest(locationId);

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
    public async Task AssignmentEntryRequestValidator_WhenLocationIdIsMissingOrInvalid_HasLocationError(int? locationId)
    {
        var validator = new AssignmentEntryRequestValidator();
        var request = CreateAssignmentEntryRequest(locationId);

        var result = await validator.TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(x => x.LocationId);
    }

    private static AssignmentSeriesRequest CreateAssignmentSeriesRequest(int? locationId) =>
        new()
        {
            AssignmentDefinitionId = 1,
            Title = "Series",
            RecurrenceRule = "FREQ=DAILY;COUNT=1",
            StartAtUtc = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            LocationId = locationId,
            Capacity = 1,
        };

    private static AssignmentEntryRequest CreateAssignmentEntryRequest(int? locationId) =>
        new()
        {
            AssignmentDefinitionId = 1,
            Title = "Entry",
            StartAtUtc = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            LocationId = locationId,
            Capacity = 1,
        };
}
