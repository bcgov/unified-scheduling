using System.Collections.ObjectModel;

public static class LeaveTypeConstants
{
    // ExpiryDate: set a value to end-date a leave type; null keeps it active.
    public static readonly IReadOnlyDictionary<
        string,
        (string Description, bool IsPaid, DateTimeOffset? ExpiryDate)
    > LeaveTypes = new ReadOnlyDictionary<string, (string Description, bool IsPaid, DateTimeOffset? ExpiryDate)>(
        new Dictionary<string, (string Description, bool IsPaid, DateTimeOffset? ExpiryDate)>
        {
            ["STIIP"] = ("Short Term Illness and Injury Plan", true, null),
            ["Vacation/CTO"] = (
                "Annual Vacation or Cumulative Leave (CTO) or Earned Leave (ETO)",
                true,
                new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero)
            ),
            ["Special"] = ("Special Leave", true, null),
            ["LWOP"] = ("Leave Without Pay", false, null),
            ["Med/Dent"] = ("Medical/Dental Leave", true, null),
            ["Vacation"] = ("Annual Vacation", true, null),
            ["CTO"] = ("Cumulative Leave (CTO) or Earned Leave (ETO)", true, null),
        }
    );
}
