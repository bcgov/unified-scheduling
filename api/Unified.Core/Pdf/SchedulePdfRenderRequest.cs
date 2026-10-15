namespace Unified.Core.Pdf;

public enum SchedulePdfView
{
    Schedule,
    Assignments,
}

public enum SchedulePdfPeriod
{
    Day,
    Week,
    WorkWeek,
}

public sealed record SchedulePdfRenderRequest
{
    public required SchedulePdfView View { get; init; }

    public required int LocationId { get; init; }

    public required DateOnly StartDate { get; init; }

    /// <summary>Exclusive end date.</summary>
    public required DateOnly EndDate { get; init; }

    public required SchedulePdfPeriod Period { get; init; }
}
