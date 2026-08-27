using Unified.Authorization;
using Unified.Authorization.Seeders;
using Unified.Common.Seeding;

namespace Unified.Audit;

/// <summary>
/// Permission seed data owned by the Audit module.
/// </summary>
public sealed class AuditPermissionSeedData : ISeedData<PermissionSeedDefinition>
{
    private const string PermissionGroupAudit = "Audit";

    public static ISeedData<PermissionSeedDefinition> Instance { get; } = new AuditPermissionSeedData();

    private AuditPermissionSeedData() { }

    public IReadOnlyList<PermissionSeedDefinition> Definitions { get; } =
    [
        new()
        {
            Group = PermissionGroupAudit,
            Id = nameof(Permissions.AuditRead),
            Description = "View audit history records",
        },
    ];
}
