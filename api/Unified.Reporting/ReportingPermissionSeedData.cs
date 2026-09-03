using Unified.Authorization;
using Unified.Authorization.Seeders;
using Unified.Common.Seeding;

namespace Unified.Reporting;

/// <summary>
/// Permission seed data owned by the Reporting module.
/// </summary>
public sealed class ReportingPermissionSeedData : ISeedData<PermissionSeedDefinition>
{
    private const string PermissionGroupReports = "Reports";

    public static ISeedData<PermissionSeedDefinition> Instance { get; } = new ReportingPermissionSeedData();

    private ReportingPermissionSeedData() { }

    public IReadOnlyList<PermissionSeedDefinition> Definitions { get; } =
    [
        new()
        {
            Group = PermissionGroupReports,
            Id = nameof(Permissions.ReportsGenerate),
            Description = "Generate reports",
        },
    ];
}
