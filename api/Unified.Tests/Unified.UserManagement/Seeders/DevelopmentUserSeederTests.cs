using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unified.Db;
using Unified.Db.Models.UserManagement;
using Unified.UserManagement.Options;
using Unified.UserManagement.Seeders;

namespace Unified.Tests.UserManagement.Seeders;

public sealed class DevelopmentUserSeederTests : IAsyncLifetime
{
    private UnifiedDbContext _dbContext = null!;

    public ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new UnifiedDbContext(options);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task SeedAsync_WhenEnvironmentIsNotDevelopment_DoesNotCreateDeveloperUser()
    {
        var seeder = CreateSeeder("Production", Guid.NewGuid());

        await seeder.SeedAsync(_dbContext, TestContext.Current.CancellationToken);

        Assert.Empty(_dbContext.Users);
        Assert.Empty(_dbContext.Roles);
    }

    [Fact]
    public async Task SeedAsync_WhenDevelopmentAndUserIdConfigured_CreatesDeveloperRoleUserAndPermissions()
    {
        var developerUserId = new Guid("11111111-1111-1111-1111-111111111111");
        _dbContext.Permissions.AddRange(
            new Permission
            {
                Id = "UsersView",
                Group = "User Management",
                Description = "View users",
            },
            new Permission
            {
                Id = "ShiftsView",
                Group = "Scheduling",
                Description = "View shifts",
            }
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var seeder = CreateSeeder(Environments.Development, developerUserId);

        await seeder.SeedAsync(_dbContext, TestContext.Current.CancellationToken);
        await seeder.SeedAsync(_dbContext, TestContext.Current.CancellationToken);

        var role = await _dbContext.Roles.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Developer", role.Name);

        var user = await _dbContext.Users.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(developerUserId, user.Id);
        Assert.Equal("DEVELOPER", user.IdirName);
        Assert.True(user.IsEnabled);

        var rolePermissionIds = await _dbContext
            .RolePermissions.Where(rolePermission => rolePermission.RoleId == role.Id)
            .Select(rolePermission => rolePermission.PermissionId)
            .OrderBy(permissionId => permissionId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["ShiftsView", "UsersView"], rolePermissionIds);

        var userRole = await _dbContext.UserRoles.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(developerUserId, userRole.UserId);
        Assert.Equal(role.Id, userRole.RoleId);
        Assert.Null(userRole.ExpiryDate);
    }

    private static DevelopmentUserSeeder CreateSeeder(string environmentName, Guid userId) =>
        new(
            NullLogger<DevelopmentUserSeeder>.Instance,
            new TestHostEnvironment { EnvironmentName = environmentName },
            Options.Create(new DevelopmentUserOptions { UserId = userId })
        );

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Unified.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
