using System.Text.Json;

namespace Unified.Calendar.Models;

public sealed class CalendarEventsRequest
{
    public required DateOnly StartDate { get; init; }

    public required DateOnly EndDate { get; init; }

    public string? TimeZoneId { get; init; }

    public int? LocationId { get; init; }

    public Dictionary<string, JsonElement>? Filters { get; init; }
}
