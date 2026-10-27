namespace Unified.LoanOut.Models.Calendar;

public sealed record LoanOutUserSearchRequest
{
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }

    public int? LocationId { get; init; }

    public string? TimeZoneId { get; init; }
}
