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

    private async Task SeedTestData()
    {
        var roles = new[]
        {
            new Role { Id = 1, Name = "Administrator", Description = "System administrator" },
            new Role { Id = 2, Name = "Manager", Description = "Role manager" },
            new Role { Id = 3, Name = "Staff", Description = "Regular staff member" }
        };

        _dbContext.Roles.AddRange(roles);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetAllAsync_Should_Return_All_Roles_Ordered_By_Name()
    {
        // Arrange
        await SeedTestData();

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
    public async Task CreateAsync_Should_Create_And_Return_Role()
    {
        // Arrange
        var request = new RoleRequestDto
        {
            Name = "Viewer",
            Description = "Read-only viewer"
        };

        // Act
        var result = await _roleService.CreateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Viewer", result.Name);
        Assert.Equal("Read-only viewer", result.Description);
        Assert.True(result.Id > 0);

        // Verify persistence
        var storedRole = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.Name == "Viewer", TestContext.Current.CancellationToken);
        Assert.NotNull(storedRole);
        Assert.Equal("Viewer", storedRole.Name);
    }

    [Fact]
    public async Task CreateAsync_Should_Persist_Role_To_Database()
    {
        // Arrange
        var request = new RoleRequestDto { Name = "Auditor", Description = "Audit role" };

        // Act
        var createdRole = await _roleService.CreateAsync(request, TestContext.Current.CancellationToken);

        // Assert - Verify by querying DB
        var queryResult = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.Id == createdRole.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(queryResult);
        Assert.Equal("Auditor", queryResult.Name);
        Assert.Equal("Audit role", queryResult.Description);
    }
}
