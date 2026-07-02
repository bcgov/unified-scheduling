namespace Unified.Calendar.Models;

public sealed record CalendarDataResponse
{
    public string ModuleId { get; init; } = "calendar";

    public string ContributionId { get; init; } = "calendar.events";

    public IReadOnlyCollection<CalendarEventResponse> Events { get; init; } = [];
}
