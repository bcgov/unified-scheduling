using Unified.Db.Models.Calendar;

namespace Unified.Leave.Services;

internal static class LeaveEditPolicy
{
    public static bool CanEditStatus(string statusTypeCode) =>
        statusTypeCode is CalendarEventStatusTypeCodes.Draft or CalendarEventStatusTypeCodes.Active;
}
