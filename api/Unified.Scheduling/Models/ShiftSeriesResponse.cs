using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.Scheduling.Models;

public sealed record ShiftSeriesResponse : CalendarEventSeriesResponse
{
    public uint ConcurrencyToken { get; init; }

    public IReadOnlyCollection<int> ShiftEntryIds { get; init; } = [];
}
