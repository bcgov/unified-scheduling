namespace Unified.Audit.Services;

/// <summary>Computes the [start, end] UTC bounds of the current ISO week (Monday–Sunday).</summary>
public static class AuditDateRangeDefaults
{
    public static (DateTimeOffset Start, DateTimeOffset End) GetCurrentWeekUtc(DateTimeOffset nowUtc)
    {
        var today = nowUtc.UtcDateTime.Date;
        var daysSinceMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var startOfWeek = today.AddDays(-daysSinceMonday);
        var endOfWeek = startOfWeek.AddDays(7).AddTicks(-1);

        return (new DateTimeOffset(startOfWeek, TimeSpan.Zero), new DateTimeOffset(endOfWeek, TimeSpan.Zero));
    }
}
