using Unified.Calendar;
using Unified.Infrastructure.Modules;
using Unified.Scheduling;

namespace Unified.Api.Modules;

public static class UnifiedModules
{
    public static IReadOnlyCollection<UnifiedModuleDescriptor> All { get; } =
    [CalendarModule.Descriptor, SchedulingModule.Descriptor];
}
