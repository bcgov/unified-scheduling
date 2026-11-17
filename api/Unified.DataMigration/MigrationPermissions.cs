using Unified.Authorization.Seeders;

namespace Unified.DataMigration;

public static class MigrationPermissions
{
    public const string Operate = "DataMigrationOperate";
    public const string Rollback = "DataMigrationRollback";
    public const string OperationsPolicy = "Permission:" + Operate;
    public const string RollbackPolicy = "Permission:" + Rollback;

    // Permission registration uses the existing module seed/claims framework.
    // No application role, account or automatic grant is created here.
    public static readonly PermissionSeedConfiguration Configuration = new()
    {
        Source = "DataMigration",
        Definitions =
        [
            new()
            {
                Id = Operate,
                Group = "Data migration",
                Description = "Operate and read migration jobs and redacted findings.",
            },
            new()
            {
                Id = Rollback,
                Group = "Data migration",
                Description = "Preview, download restricted impact and confirm migration rollback.",
            },
        ],
    };
}
