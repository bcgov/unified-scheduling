using Unified.Calendar;
using Unified.Infrastructure.Modules;
using Unified.Scheduling;
using Unified.UserManagement;

namespace Unified.Api.Modules;

public static class UnifiedModules
{
    public static IReadOnlyCollection<UnifiedModuleDescriptor> All { get; } =
    [CalendarModule.Descriptor, SchedulingModule.Descriptor, UserManagementModule.Descriptor];
}
