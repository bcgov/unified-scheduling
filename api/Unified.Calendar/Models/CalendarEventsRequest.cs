using System.Text.Json;

namespace Unified.Calendar.Models;

public sealed class CalendarEventsRequest
{
    public required DateTimeOffset StartDate { get; init; }

    public required DateTimeOffset EndDate { get; init; }

    public int? LocationId { get; init; }

    public Dictionary<string, JsonElement>? Filters { get; init; }
}
