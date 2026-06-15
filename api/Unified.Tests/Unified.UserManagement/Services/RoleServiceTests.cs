using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Db.Models.UserManagement;
using Unified.UserManagement.Models;
using Unified.UserManagement.Services;

namespace Unified.Tests.UserManagement.Services;

public class RoleServiceTests : IAsyncLifetime
{
    private UnifiedDbContext _dbContext = null!;
    private RoleService _roleService = null!;

    public ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new UnifiedDbContext(options);
        _roleService = new RoleService(_dbContext);

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    private async Task SeedRoles()
    {
        var roles = new[]
        {
            new Role
            {
                Id = 1,
                Name = "Administrator",
                Description = "System administrator",
            },
            new Role
            {
                Id = 2,
                Name = "Manager",
                Description = "Role manager",
            },
            new Role
            {
                Id = 3,
                Name = "Staff",
                Description = "Regular staff member",
            },
        };

        _dbContext.Roles.AddRange(roles);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedPermissions()
    {
        _dbContext.Permissions.AddRange(
            new Permission { Id = "ShiftsView", Description = "View shifts" },
            new Permission { Id = "ShiftsEdit", Description = "Edit shifts" },
            new Permission { Id = "UsersView", Description = "View users" }
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // ── GetAllAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_Should_Return_All_Roles_Ordered_By_Name()
    {
        // Arrange
        await SeedRoles();

        // Act
        var result = await _roleService.GetAllAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        var roleList = result.ToList();
        Assert.Equal("Administrator", roleList[0].Name);
        Assert.Equal("Manager", roleList[1].Name);
        Assert.Equal("Staff", roleList[2].Name);
    }

    [Fact]
    public async Task GetAllAsync_Should_Return_Empty_When_No_Roles()
    {
        // Act
        var result = await _roleService.GetAllAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_Should_Include_Permissions_For_Each_Role()
    {
        // Arrange
        await SeedPermissions();
        var role = new Role
        {
            Id = 1,
            Name = "Admin",
            Description = "Admin role",
        };
        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        _dbContext.RolePermissions.Add(new RolePermission { RoleId = 1, PermissionId = "ShiftsView" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _roleService.GetAllAsync(TestContext.Current.CancellationToken);

        // Assert
        var dto = Assert.Single(result);
        Assert.Single(dto.Permissions);
        Assert.Equal("ShiftsView", dto.Permissions[0].Id);
    }

    [Fact]
    public async Task GetAssignedUsersAsync_Should_Return_Only_Active_Users_For_Role()
    {
        // Arrange
        var role = new Role { Id = 10, Name = "Manager", Description = "Manager role" };
        var activeUser = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Alice",
            LastName = "Brown",
            Email = "alice.brown@example.com",
            IdirName = "ABROWN",
            IsEnabled = true,
        };
        var expiredUser = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Bob",
            LastName = "Clark",
            Email = "bob.clark@example.com",
            IdirName = "BCLARK",
            IsEnabled = true,
        };
        var now = DateTimeOffset.UtcNow;

        _dbContext.Roles.Add(role);
        _dbContext.Users.AddRange(activeUser, expiredUser);
        _dbContext.UserRoles.AddRange(
            new UserRole
            {
                UserId = activeUser.Id,
                RoleId = role.Id,
                EffectiveDate = now.AddDays(-2),
                ExpiryDate = null,
            },
            new UserRole
            {
                UserId = expiredUser.Id,
                RoleId = role.Id,
                EffectiveDate = now.AddDays(-5),
                ExpiryDate = now.AddDays(-1),
            }
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _roleService.GetAssignedUsersAsync(role.Id, TestContext.Current.CancellationToken);

        // Assert
        var users = result.ToList();
        Assert.Single(users);
        Assert.Equal(activeUser.Id, users[0].UserId);
        Assert.Equal("Alice", users[0].FirstName);
        Assert.Equal("Brown", users[0].LastName);
    }

    [Fact]
    public async Task GetAssignedUsersAsync_When_Role_Not_Found_Throws_KeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _roleService.GetAssignedUsersAsync(9999, TestContext.Current.CancellationToken)
        );
    }

    // ── CreateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_Should_Create_And_Return_Role()
    {
        // Arrange
        var request = new RoleRequestDto { Name = "Viewer", Description = "Read-only viewer" };

        // Act
        var result = await _roleService.CreateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Viewer", result.Name);
        Assert.Equal("Read-only viewer", result.Description);
        Assert.True(result.Id > 0);

        var storedRole = await _dbContext.Roles.FirstOrDefaultAsync(
            r => r.Name == "Viewer",
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(storedRole);
    }

    [Fact]
    public async Task CreateAsync_Should_Assign_Permissions_When_PermissionIds_Provided()
    {
        // Arrange
        await SeedPermissions();
        var request = new RoleRequestDto
        {
            Name = "Scheduler",
            Description = "Scheduling role",
            PermissionIds = ["ShiftsView", "ShiftsEdit"],
        };

        // Act
        var result = await _roleService.CreateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Permissions.Count);
        Assert.Contains(result.Permissions, p => p.Id == "ShiftsView");
        Assert.Contains(result.Permissions, p => p.Id == "ShiftsEdit");

        var stored = await _dbContext
            .RolePermissions.Where(rp => rp.RoleId == result.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, stored.Count);
    }

    [Fact]
    public async Task CreateAsync_WhenNameAlreadyExists_ThrowsInvalidOperationException()
    {
        // Arrange
        await SeedRoles();
        var request = new RoleRequestDto { Name = "Administrator", Description = "Duplicate" };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _roleService.CreateAsync(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task CreateAsync_WhenPermissionIdDoesNotExist_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new RoleRequestDto
        {
            Name = "NewRole",
            Description = "Role",
            PermissionIds = ["NonExistentPermission"],
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _roleService.CreateAsync(request, TestContext.Current.CancellationToken)
        );
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_Should_Update_Name_And_Description()
    {
        // Arrange
        await SeedRoles();
        var request = new UpdateRoleRequestDto
        {
            Id = 1,
            Name = "Super Admin",
            Description = "Updated description",
            ConcurrencyToken = 0, // in-memory DB always returns 0
        };

        // Act
        var result = await _roleService.UpdateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Super Admin", result.Name);
        Assert.Equal("Updated description", result.Description);

        var stored = await _dbContext.Roles.FindAsync([1], TestContext.Current.CancellationToken);
        Assert.Equal("Super Admin", stored!.Name);
    }

    [Fact]
    public async Task UpdateAsync_Should_Add_New_Permissions()
    {
        // Arrange
        await SeedPermissions();
        _dbContext.Roles.Add(
            new Role
            {
                Id = 10,
                Name = "Tester",
                Description = "Test role",
            }
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new UpdateRoleRequestDto
        {
            Id = 10,
            Name = "Tester",
            Description = "Test role",
            PermissionIds = ["ShiftsView", "UsersView"],
            ConcurrencyToken = 0,
        };

        // Act
        var result = await _roleService.UpdateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Permissions.Count);
        Assert.Contains(result.Permissions, p => p.Id == "ShiftsView");
        Assert.Contains(result.Permissions, p => p.Id == "UsersView");
    }

    [Fact]
    public async Task UpdateAsync_Should_Remove_Omitted_Permissions()
    {
        // Arrange
        await SeedPermissions();
        var role = new Role
        {
            Id = 20,
            Name = "Ops",
            Description = "Ops role",
        };
        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        _dbContext.RolePermissions.AddRange(
            new RolePermission { RoleId = 20, PermissionId = "ShiftsView" },
            new RolePermission { RoleId = 20, PermissionId = "ShiftsEdit" }
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new UpdateRoleRequestDto
        {
            Id = 20,
            Name = "Ops",
            Description = "Ops role",
            PermissionIds = ["ShiftsView"], // ShiftsEdit should be removed
            ConcurrencyToken = 0,
        };

        // Act
        var result = await _roleService.UpdateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result.Permissions);
        Assert.Equal("ShiftsView", result.Permissions[0].Id);

        var stored = await _dbContext
            .RolePermissions.Where(rp => rp.RoleId == 20)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(stored);
    }

    [Fact]
    public async Task UpdateAsync_WhenRoleNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var request = new UpdateRoleRequestDto
        {
            Id = 9999,
            Name = "Ghost",
            Description = "Doesn't exist",
            ConcurrencyToken = 0,
        };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _roleService.UpdateAsync(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task UpdateAsync_WhenConcurrencyTokenMismatch_ThrowsInvalidOperationException()
    {
        // Arrange
        await SeedRoles();
        var request = new UpdateRoleRequestDto
        {
            Id = 1,
            Name = "Administrator",
            Description = "Administrator",
            ConcurrencyToken = 999, // in-memory DB stores 0; this will mismatch
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _roleService.UpdateAsync(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task UpdateAsync_WhenNewNameAlreadyUsedByAnotherRole_ThrowsInvalidOperationException()
    {
        // Arrange
        await SeedRoles();
        var request = new UpdateRoleRequestDto
        {
            Id = 1,
            Name = "Manager", // already used by role Id 2
            Description = "Updated",
            ConcurrencyToken = 0,
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _roleService.UpdateAsync(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task UpdateAsync_WhenPermissionIdDoesNotExist_ThrowsInvalidOperationException()
    {
        // Arrange
        await SeedRoles();
        var request = new UpdateRoleRequestDto
        {
            Id = 1,
            Name = "Administrator",
            Description = "Admin",
            PermissionIds = ["GhostPermission"],
            ConcurrencyToken = 0,
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _roleService.UpdateAsync(request, TestContext.Current.CancellationToken)
        );
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_Should_Delete_Role()
    {
        // Arrange
        await SeedRoles();

        // Act
        await _roleService.DeleteAsync(1, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(await _dbContext.Roles.AnyAsync(r => r.Id == 1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_WhenRoleNotFound_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _roleService.DeleteAsync(9999, TestContext.Current.CancellationToken)
        );
    }
}
