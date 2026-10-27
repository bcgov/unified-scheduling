namespace Unified.LoanOut.Models.Calendar;

public sealed record LoanOutCalendarEventQueryContext
{
    public required DateTimeOffset StartAtUtc { get; init; }

    public required DateTimeOffset EndAtUtc { get; init; }

    public int? LocationId { get; init; }

    public IReadOnlyCollection<Guid> UserIds { get; init; } = [];
}
