using FluentValidation.TestHelper;
using Unified.Scheduling.Models;
using Unified.Scheduling.Validators;

namespace Unified.Tests.Scheduling.Validators;

public sealed class ProposedShiftAssignmentOptionsRequestValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Validate_WhenSeriesScopeRecurrenceRuleIsMissing_HasError(string? recurrenceRule)
    {
        var request = CreateRequest() with { IsSeriesScope = true, RecurrenceRule = recurrenceRule };

        var result = await new ProposedShiftAssignmentOptionsRequestValidator().TestValidateAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldHaveValidationErrorFor(item => item.RecurrenceRule);
    }

    [Fact]
    public async Task Validate_WhenEntryScopeRecurrenceRuleIsMissing_HasNoRecurrenceRuleError()
    {
        var result = await new ProposedShiftAssignmentOptionsRequestValidator().TestValidateAsync(
            CreateRequest(),
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.ShouldNotHaveValidationErrorFor(item => item.RecurrenceRule);
    }

    private static ProposedShiftAssignmentOptionsRequest CreateRequest() =>
        new()
        {
            LocationId = 5,
            StartAtUtc = new DateTimeOffset(2026, 9, 2, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 9, 2, 17, 0, 0, TimeSpan.Zero),
            TimeZoneId = "UTC",
        };
}
