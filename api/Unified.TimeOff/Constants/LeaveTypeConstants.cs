using System.Collections.ObjectModel;

public static class LeaveTypeConstants
{
    public static readonly IReadOnlyDictionary<string, (string Description, bool IsPaid)> LeaveTypes =
        new ReadOnlyDictionary<string, (string Description, bool IsPaid)>(
            new Dictionary<string, (string Description, bool IsPaid)>
            {
                ["STIIP"] = ("Short Term Illness and Injury Plan", true),
                ["Vacation/CTO"] = ("Annual Vacation or Cumulative Time Off (CTO) or Earned Time Off (ETO)", true),
                ["Special"] = ("Special Leave", true),
                ["LWOP"] = ("Leave Without Pay", false),
                ["Med/Dent"] = ("Medical/Dental Leave", true),
            }
        );
}
