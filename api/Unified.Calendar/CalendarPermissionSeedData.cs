using Unified.Authorization;
using Unified.Authorization.Seeders;

namespace Unified.Calendar;

public static class CalendarPermissionSeedData
{
    public static PermissionSeedConfiguration Configuration { get; } =
        new()
        {
            Source = "CalendarModule",
            Definitions =
            [
                new()
                {
                    Group = "Calendar",
                    Id = nameof(Permissions.CalendarConflictsOverride),
                    Description = "Override calendar conflicts",
                },
            ],
        };
}
