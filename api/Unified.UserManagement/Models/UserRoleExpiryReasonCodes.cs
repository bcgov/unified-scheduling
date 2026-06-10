namespace Unified.UserManagement.Models;

public static class UserRoleExpiryReasonCodes
{
    public const string OperationalDemand = "OPERDEMAND";
    public const string PersonalDecision = "PERSONAL";
    public const string EntryError = "ENTRYERR";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        OperationalDemand,
        PersonalDecision,
        EntryError,
    };
}