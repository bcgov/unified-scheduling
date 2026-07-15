using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.UserManagement;
using Unified.UserManagement.Options;

namespace Unified.UserManagement.Seeders;

public sealed class DevelopmentUserSeeder(
    ILogger<DevelopmentUserSeeder> logger,
    IHostEnvironment environment,
    IOptions<DevelopmentUserOptions> options
) : SeederBase<UnifiedDbContext>(logger)
{
    private const string DeveloperRoleName = "Developer";
    private static readonly DateTimeOffset SeedEffectiveDate = new(2020, 6, 10, 0, 0, 0, TimeSpan.Zero);

    public override int Order => 100;

    public override string Name => "DevelopmentUser";

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            Logger.LogDebug("Skipping development user seed outside Development environment.");
            return;
        }

        var developerUserId = options.Value.UserId;
        if (developerUserId == Guid.Empty)
        {
            Logger.LogWarning(
                "Skipping development user seed because {Section}:{Property} is not configured.",
                DevelopmentUserOptions.SectionName,
                nameof(DevelopmentUserOptions.UserId)
            );
            return;
        }

        var role = await SeedDeveloperRoleAsync(dbContext, cancellationToken);
        await SeedDeveloperUserAsync(dbContext, developerUserId, cancellationToken);
        await SyncDeveloperRolePermissionsAsync(dbContext, role.Id, cancellationToken);
        await SeedDeveloperUserRoleAsync(dbContext, developerUserId, role.Id, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Role> SeedDeveloperRoleAsync(
        UnifiedDbContext dbContext,
        CancellationToken cancellationToken
    )
    {
        var role = await dbContext
            .Roles.FirstOrDefaultAsync(
                candidate => candidate.Name == DeveloperRoleName && candidate.DeletedById == null,
                cancellationToken
            );

        if (role is null)
        {
            role = new Role
            {
                Name = DeveloperRoleName,
                Description = "Developer",
            };
            await dbContext.Roles.AddAsync(role, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return role;
        }

        role.Description = "Developer";
        role.DeletedById = null;
        role.DeletedOn = null;
        return role;
    }

    private static async Task SeedDeveloperUserAsync(
        UnifiedDbContext dbContext,
        Guid developerUserId,
        CancellationToken cancellationToken
    )
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(candidate => candidate.Id == developerUserId, cancellationToken);

        if (user is null)
        {
            user = new User
            {
                Id = developerUserId,
                IdirId = developerUserId,
                KeyCloakId = developerUserId,
                IdirName = "DEVELOPER",
                IsEnabled = true,
                FirstName = "Developer",
                LastName = "User",
                Email = "developer@example.local",
                Gender = Gender.Other,
            };
            await dbContext.Users.AddAsync(user, cancellationToken);
            return;
        }

        user.IdirId = developerUserId;
        user.KeyCloakId = developerUserId;
        user.IdirName = "DEVELOPER";
        user.IsEnabled = true;
        user.FirstName = "Developer";
        user.LastName = "User";
        user.Email = "developer@example.local";
        user.Gender = Gender.Other;
    }

    private static async Task SyncDeveloperRolePermissionsAsync(
        UnifiedDbContext dbContext,
        int roleId,
        CancellationToken cancellationToken
    )
    {
        var permissionIds = await dbContext.Permissions.Select(permission => permission.Id).ToListAsync(cancellationToken);
        var permissionIdSet = permissionIds.ToHashSet(StringComparer.Ordinal);
        var existingRolePermissions = await dbContext
            .RolePermissions.Where(rolePermission => rolePermission.RoleId == roleId)
            .ToListAsync(cancellationToken);
        var existingPermissionIdSet = existingRolePermissions
            .Select(rolePermission => rolePermission.PermissionId)
            .ToHashSet(StringComparer.Ordinal);

        dbContext.RolePermissions.RemoveRange(
            existingRolePermissions.Where(rolePermission => !permissionIdSet.Contains(rolePermission.PermissionId))
        );

        foreach (var permissionId in permissionIds.Where(permissionId => !existingPermissionIdSet.Contains(permissionId)))
        {
            await dbContext.RolePermissions.AddAsync(
                new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = permissionId,
                },
                cancellationToken
            );
        }
    }

    private static async Task SeedDeveloperUserRoleAsync(
        UnifiedDbContext dbContext,
        Guid developerUserId,
        int roleId,
        CancellationToken cancellationToken
    )
    {
        var userRole = await dbContext
            .UserRoles.FirstOrDefaultAsync(
                candidate => candidate.UserId == developerUserId && candidate.RoleId == roleId,
                cancellationToken
            );

        if (userRole is null)
        {
            await dbContext.UserRoles.AddAsync(
                new UserRole
                {
                    UserId = developerUserId,
                    RoleId = roleId,
                    EffectiveDate = SeedEffectiveDate,
                },
                cancellationToken
            );
            return;
        }

        userRole.EffectiveDate = SeedEffectiveDate;
        userRole.ExpiryDate = null;
        userRole.ExpiryReason = null;
    }
}
