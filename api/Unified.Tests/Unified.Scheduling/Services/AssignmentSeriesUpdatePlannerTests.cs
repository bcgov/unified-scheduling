using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;

namespace Unified.Tests.Scheduling.Services;

public sealed class AssignmentSeriesUpdatePlannerTests
{
    [Fact]
    public void CreatePlan_WhenRecurrenceValuesAreEquivalent_PropagatesWithoutRegeneration()
    {
        var assignmentSeries = CreateSeries();
        var request = CreateRequest();

        var plan = AssignmentSeriesUpdatePlanner.CreatePlan(assignmentSeries, request);

        Assert.False(plan.RecurrenceChanged);
        Assert.False(plan.RegenerateEntries);
        Assert.True(plan.PropagateSeriesChanges);
        Assert.Equal(7, plan.PreviousValues.AssignmentDefinitionId);
        Assert.Equal(4, plan.PreviousValues.Capacity);
    }

    [Fact]
    public void CreatePlan_WhenStartChanges_RegeneratesWithoutPropagation()
    {
        var assignmentSeries = CreateSeries();
        var request = CreateRequest() with { StartAtUtc = new DateTimeOffset(2026, 8, 22, 16, 0, 0, TimeSpan.Zero) };

        var plan = AssignmentSeriesUpdatePlanner.CreatePlan(assignmentSeries, request);

        Assert.True(plan.RecurrenceChanged);
        Assert.True(plan.RegenerateEntries);
        Assert.False(plan.PropagateSeriesChanges);
    }

    private static AssignmentSeries CreateSeries() =>
        new()
        {
            AssignmentDefinitionId = 7,
            Capacity = 4,
            CategoryId = 10,
            SubCategoryId = 20,
            EventSeries = new EventSeries
            {
                Title = "Court",
                Description = "Description",
                Notes = "Notes",
                Color = "#123456",
                RecurrenceRule = " FREQ=DAILY ",
                TimeZoneId = " America/Vancouver ",
                StartAtUtc = new DateTimeOffset(2026, 8, 21, 16, 0, 0, TimeSpan.Zero),
                EndAtUtc = new DateTimeOffset(2026, 8, 21, 17, 0, 0, TimeSpan.Zero),
                LocationId = 3,
            },
        };

    private static AssignmentSeriesRequest CreateRequest() =>
        new()
        {
            AssignmentDefinitionId = 8,
            Title = "Updated court",
            Color = "#654321",
            RecurrenceRule = "FREQ=DAILY",
            TimeZoneId = "America/Vancouver",
            StartAtUtc = new DateTimeOffset(2026, 8, 21, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 8, 21, 17, 0, 0, TimeSpan.Zero),
            LocationId = 4,
            CategoryId = 11,
            SubCategoryId = 21,
            Capacity = 5,
        };
}
