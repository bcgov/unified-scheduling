using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Authorization;
using Unified.Db;
using Unified.Db.Models.UserManagement;
using Unified.Scheduling.Seeders;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.Scheduling.Seeders;

public sealed class SchedulingSeedersTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private UnifiedDbContext _dbContext = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.CreateFunction("now", () => DateTimeOffset.UtcNow.ToString("O"));
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        var options = new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(_connection).Options;
        _dbContext = new SqliteTestUnifiedDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task SchedulingRolePermissionSeeder_SeedAsync_UsesRoleNamesAndIsIdempotent()
    {
        _dbContext.Roles.AddRange(
            new Role { Id = 101, Name = "Administrator", Description = "Administrator" },
            new Role { Id = 202, Name = "Manager", Description = "Manager" },
            new Role { Id = 303, Name = "Staff", Description = "Staff" }
        );
        _dbContext.Permissions.AddRange(
            CreatePermission(nameof(Permissions.AssignmentsView)),
            CreatePermission(nameof(Permissions.AssignmentsCreate)),
            CreatePermission(nameof(Permissions.AssignmentsAssign)),
            CreatePermission(nameof(Permissions.AssignmentsEdit)),
            CreatePermission(nameof(Permissions.AssignmentsExpire)),
            CreatePermission(nameof(Permissions.AssignmentTypeRead)),
            CreatePermission(nameof(Permissions.AssignmentTypeWrite)),
            CreatePermission(nameof(Permissions.AssignmentTypeExpire)),
            CreatePermission(nameof(Permissions.ShiftsView)),
            CreatePermission(nameof(Permissions.ShiftsCreateAndAssign)),
            CreatePermission(nameof(Permissions.ShiftsEdit)),
            CreatePermission(nameof(Permissions.ShiftsExpire))
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var seeder = new SchedulingRolePermissionSeeder(new NullLogger<SchedulingRolePermissionSeeder>());

        await seeder.SeedAsync(_dbContext, TestContext.Current.CancellationToken);
        await seeder.SeedAsync(_dbContext, TestContext.Current.CancellationToken);

        var adminPermissionIds = await _dbContext
            .RolePermissions.Where(rolePermission => rolePermission.RoleId == 101)
            .Select(rolePermission => rolePermission.PermissionId)
            .OrderBy(permissionId => permissionId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains(nameof(Permissions.AssignmentsCreate), adminPermissionIds);
        Assert.Contains(nameof(Permissions.AssignmentsAssign), adminPermissionIds);
        Assert.Equal(adminPermissionIds.Count, adminPermissionIds.Distinct().Count());

        var managerPermissionIds = await _dbContext
            .RolePermissions.Where(rolePermission => rolePermission.RoleId == 202)
            .Select(rolePermission => rolePermission.PermissionId)
            .OrderBy(permissionId => permissionId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(adminPermissionIds, managerPermissionIds);

        var staffPermissionIds = await _dbContext
            .RolePermissions.Where(rolePermission => rolePermission.RoleId == 303)
            .Select(rolePermission => rolePermission.PermissionId)
            .OrderBy(permissionId => permissionId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            [
                nameof(Permissions.AssignmentTypeRead),
                nameof(Permissions.AssignmentsView),
                nameof(Permissions.ShiftsView),
            ],
            staffPermissionIds
        );

        Assert.Empty(
            await _dbContext
                .RolePermissions.Where(rolePermission => rolePermission.RoleId == 1)
                .ToListAsync(TestContext.Current.CancellationToken)
        );
        Assert.Empty(
            await _dbContext
                .RolePermissions.Where(rolePermission => rolePermission.RoleId == 2)
                .ToListAsync(TestContext.Current.CancellationToken)
        );
        Assert.Empty(
            await _dbContext
                .RolePermissions.Where(rolePermission => rolePermission.RoleId == 3)
                .ToListAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task SchedulingRolePermissionSeeder_SeedAsync_WhenRoleMissing_SkipsMissingRole()
    {
        _dbContext.Roles.Add(new Role { Id = 303, Name = "Staff", Description = "Staff" });
        _dbContext.Permissions.AddRange(
            CreatePermission(nameof(Permissions.ShiftsView)),
            CreatePermission(nameof(Permissions.AssignmentsView)),
            CreatePermission(nameof(Permissions.AssignmentTypeRead))
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var seeder = new SchedulingRolePermissionSeeder(new NullLogger<SchedulingRolePermissionSeeder>());

        await seeder.SeedAsync(_dbContext, TestContext.Current.CancellationToken);

        var rolePermissions = await _dbContext
            .RolePermissions.OrderBy(rolePermission => rolePermission.PermissionId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, rolePermissions.Count);
        Assert.All(rolePermissions, rolePermission => Assert.Equal(303, rolePermission.RoleId));
    }

    private static Permission CreatePermission(string id) =>
        new()
        {
            Id = id,
            Group = "Scheduling",
            Description = id,
        };
}
