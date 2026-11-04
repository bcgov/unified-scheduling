using Unified.Common.Scheduling.Models;

namespace Unified.LoanOut.Models.Calendar;

public sealed record LoanOutCalendarEvent : SchedulingCalendarEvent
{
    public int LoanOutEntryId { get; init; }
    public int? LoanOutSeriesId { get; init; }
    public int LoanOutLocationId { get; init; }
    public string? LoanOutLocationName { get; init; }
}
