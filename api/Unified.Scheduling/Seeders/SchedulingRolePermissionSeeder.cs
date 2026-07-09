using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Authorization;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.UserManagement;

namespace Unified.Scheduling.Seeders;

public sealed class SchedulingRolePermissionSeeder(ILogger<SchedulingRolePermissionSeeder> logger)
    : SeederBase<UnifiedDbContext>(logger)
{
    private const string AdministratorRoleName = "Administrator";
    private const string ManagerRoleName = "Manager";
    private const string StaffRoleName = "Staff";

    private static readonly string[] AdministratorPermissions =
    [
        nameof(Permissions.ShiftsView),
        nameof(Permissions.ShiftsCreateAndAssign),
        nameof(Permissions.ShiftsEdit),
        nameof(Permissions.ShiftsExpire),
        nameof(Permissions.AssignmentsView),
        nameof(Permissions.AssignmentsCreate),
        nameof(Permissions.AssignmentsAssign),
        nameof(Permissions.AssignmentsEdit),
        nameof(Permissions.AssignmentsExpire),
        nameof(Permissions.AssignmentTypeRead),
        nameof(Permissions.AssignmentTypeWrite),
        nameof(Permissions.AssignmentTypeExpire),
    ];

    private static readonly string[] ManagerPermissions = AdministratorPermissions;

    private static readonly string[] StaffPermissions =
    [
        nameof(Permissions.ShiftsView),
        nameof(Permissions.AssignmentsView),
        nameof(Permissions.AssignmentTypeRead),
    ];

    public override int Order => 13;

    public override string Name => "SchedulingRolePermissions";

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        await SeedRolePermissionsAsync(dbContext, AdministratorRoleName, AdministratorPermissions, cancellationToken);
        await SeedRolePermissionsAsync(dbContext, ManagerRoleName, ManagerPermissions, cancellationToken);
        await SeedRolePermissionsAsync(dbContext, StaffRoleName, StaffPermissions, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedRolePermissionsAsync(
        UnifiedDbContext dbContext,
        string roleName,
        IReadOnlyCollection<string> permissionIds,
        CancellationToken cancellationToken
    )
    {
        var roleId = await dbContext
            .Roles.Where(role => role.Name == roleName && role.DeletedById == null)
            .Select(role => role.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (roleId == 0)
            return;

        var existingPermissionIds = await dbContext
            .RolePermissions.Where(rolePermission => rolePermission.RoleId == roleId)
            .Select(rolePermission => rolePermission.PermissionId)
            .ToListAsync(cancellationToken);
        var existingPermissionIdSet = existingPermissionIds.ToHashSet(StringComparer.Ordinal);

        foreach (var permissionId in permissionIds.Where(permissionId => !existingPermissionIdSet.Contains(permissionId)))
        {
            dbContext.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
        }
    }
}
