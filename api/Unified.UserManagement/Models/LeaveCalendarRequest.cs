namespace Unified.UserManagement.Models;

public sealed record LeaveCalendarRequest
{
    // Inclusive start date.
    public required DateOnly StartDate { get; init; }

    // Inclusive end date.
    public required DateOnly EndDate { get; init; }

    public string? TimeZoneId { get; init; }

    public IReadOnlyCollection<Guid>? UserIds { get; init; }
}
