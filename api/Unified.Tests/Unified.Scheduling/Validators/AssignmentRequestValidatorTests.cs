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
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AssignmentSeriesRequest.Capacity));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AssignmentSeriesRequest.Color));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AssignmentSeriesRequest.EndAtUtc));
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
}
