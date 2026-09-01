using System.Runtime.CompilerServices;

namespace Unified.Tests;

/// <summary>
/// UnifiedDbContext inherits Audit.NET's AuditDbContext (see Unified.Audit's README), so any test
/// that constructs one directly and calls SaveChangesAsync would otherwise drive the process-wide
/// Audit.Core.Configuration pipeline (writing stray files via the default FileDataProvider, or
/// racing with whatever DataProvider another test class configured). Disabled globally by default;
/// AuditPipelineTests explicitly re-enables it around its own test bodies.
/// </summary>
internal static class ModuleInitialization
{
    [ModuleInitializer]
    public static void Initialize()
    {
        global::Audit.Core.Configuration.AuditDisabled = true;
    }
}
