using Unified.Scheduling.Models;
using Unified.Scheduling.Validators;

namespace Unified.Tests.Scheduling.Validators;

public sealed class AssignmentRequestValidatorTests
{
    [Fact]
    public async Task Series_requires_explicit_snapshot_values()
    {
        var result = await new AssignmentSeriesRequestValidator().ValidateAsync(
            new AssignmentSeriesRequest(),
            TestContext.Current.CancellationToken
        );
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AssignmentSeriesRequest.LocationId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AssignmentSeriesRequest.CategoryId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AssignmentSeriesRequest.SubCategoryId));
        Assert.DoesNotContain(result.Errors, error => error.PropertyName == nameof(AssignmentSeriesRequest.Capacity));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AssignmentSeriesRequest.Color));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AssignmentSeriesRequest.EndAtUtc));
    }

    [Fact]
    public async Task Assignment_validators_accept_zero_capacity()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var results = await Task.WhenAll(
            new AssignmentSeriesRequestValidator().ValidateAsync(CreateSeriesRequest(0), cancellationToken),
            new AssignmentEntryRequestValidator().ValidateAsync(CreateEntryRequest(0), cancellationToken),
            new AssignmentEntryUpdateRequestValidator()
                .ValidateAsync(CreateEntryUpdateRequest(0), cancellationToken)
        );

        Assert.All(results, result => Assert.True(result.IsValid));
    }

    [Fact]
    public async Task Assignment_validators_reject_negative_capacity()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var results = await Task.WhenAll(
            new AssignmentSeriesRequestValidator().ValidateAsync(CreateSeriesRequest(-1), cancellationToken),
            new AssignmentEntryRequestValidator().ValidateAsync(CreateEntryRequest(-1), cancellationToken),
            new AssignmentEntryUpdateRequestValidator()
                .ValidateAsync(CreateEntryUpdateRequest(-1), cancellationToken)
        );

        Assert.All(
            results,
            result =>
                Assert.Contains(
                    result.Errors,
                    error => error.PropertyName == nameof(AssignmentEntryRequest.Capacity)
                )
        );
    }

    [Fact]
    public async Task Entry_accepts_complete_explicit_values()
    {
        var request = new AssignmentEntryRequest
        {
            AssignmentDefinitionId = 1,
            Title = "Assignment",
            Color = "#123456",
            StartAtUtc = DateTimeOffset.Parse("2026-08-20T16:00:00Z"),
            EndAtUtc = DateTimeOffset.Parse("2026-08-20T17:00:00Z"),
            LocationId = 2,
            CategoryId = 3,
            SubCategoryId = 4,
            Capacity = 1,
        };
        var result = await new AssignmentEntryRequestValidator().ValidateAsync(
            request,
            TestContext.Current.CancellationToken
        );
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Entry_rejects_reversed_series_slot_times()
    {
        var request = new AssignmentEntryRequest
        {
            AssignmentDefinitionId = 1,
            Title = "Assignment",
            Color = "#123456",
            StartAtUtc = DateTimeOffset.Parse("2026-08-20T16:00:00Z"),
            EndAtUtc = DateTimeOffset.Parse("2026-08-20T17:00:00Z"),
            SeriesStartAtUtc = DateTimeOffset.Parse("2026-08-20T18:00:00Z"),
            SeriesEndAtUtc = DateTimeOffset.Parse("2026-08-20T17:00:00Z"),
            LocationId = 2,
            CategoryId = 3,
            SubCategoryId = 4,
            Capacity = 1,
        };

        var result = await new AssignmentEntryRequestValidator().ValidateAsync(
            request,
            TestContext.Current.CancellationToken
        );

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AssignmentEntryRequest.SeriesStartAtUtc));
    }

    private static AssignmentSeriesRequest CreateSeriesRequest(int capacity) =>
        new()
        {
            AssignmentDefinitionId = 1,
            Title = "Assignment",
            Color = "#123456",
            RecurrenceRule = "RRULE:FREQ=DAILY;COUNT=2",
            StartAtUtc = DateTimeOffset.Parse("2026-08-20T16:00:00Z"),
            EndAtUtc = DateTimeOffset.Parse("2026-08-20T17:00:00Z"),
            LocationId = 2,
            CategoryId = 3,
            SubCategoryId = 4,
            Capacity = capacity,
        };

    private static AssignmentEntryRequest CreateEntryRequest(int capacity) =>
        new()
        {
            AssignmentDefinitionId = 1,
            Title = "Assignment",
            Color = "#123456",
            StartAtUtc = DateTimeOffset.Parse("2026-08-20T16:00:00Z"),
            EndAtUtc = DateTimeOffset.Parse("2026-08-20T17:00:00Z"),
            LocationId = 2,
            CategoryId = 3,
            SubCategoryId = 4,
            Capacity = capacity,
        };

    private static AssignmentEntryUpdateRequest CreateEntryUpdateRequest(int capacity) =>
        new()
        {
            AssignmentDefinitionId = 1,
            Title = "Assignment",
            Color = "#123456",
            StartAtUtc = DateTimeOffset.Parse("2026-08-20T16:00:00Z"),
            EndAtUtc = DateTimeOffset.Parse("2026-08-20T17:00:00Z"),
            LocationId = 2,
            CategoryId = 3,
            SubCategoryId = 4,
            Capacity = capacity,
        };
}
