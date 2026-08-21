namespace Unified.Scheduling.Models;

public sealed record SchedulingCalendarDataResponse
{
    public string ModuleId { get; init; } = "scheduling";

    public string ContributionId { get; init; } = "scheduling.events";

    public IReadOnlyCollection<SchedulingCalendarEventResponse> Events { get; init; } = [];
}
