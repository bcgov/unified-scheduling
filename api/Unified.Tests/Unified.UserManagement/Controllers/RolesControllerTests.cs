using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Unified.UserManagement.Controllers;
using Unified.UserManagement.Models;
using Unified.UserManagement.Services;
using Unified.UserManagement.Validators;

namespace Unified.Tests.UserManagement.Controllers;

public class RolesControllerTests
{
    private static RolesController BuildController(FakeRoleService? service = null) =>
        new(service ?? new FakeRoleService(), new RoleRequestValidator(), new UpdateRoleRequestValidator());

    // ── Get ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_Should_Return_Ok_With_Roles()
    {
        // Arrange
        var expectedRoles = new List<RoleDto>
        {
            new()
            {
                Id = 1,
                Name = "Administrator",
                Description = "Administrator role",
            },
            new()
            {
                Id = 2,
                Name = "Manager",
                Description = "Manager role",
            },
        };
        var controller = BuildController(new FakeRoleService { GetAllResult = expectedRoles });

        // Act
        var result = await controller.Get(TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var roles = Assert.IsAssignableFrom<IEnumerable<RoleDto>>(okResult.Value);
        Assert.Equal(2, roles.Count());
    }

    // ── Create ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_Should_Return_Created_With_Location_And_Body()
    {
        // Arrange
        var createdRole = new RoleDto
        {
            Id = 1,
            Name = "Viewer",
            Description = "Read-only viewer role",
        };
        var controller = BuildController(new FakeRoleService { CreateResult = createdRole });
        var request = new RoleRequestDto { Name = "Viewer", Description = "Read-only viewer role" };

        // Act
        var result = await controller.Create(request, TestContext.Current.CancellationToken);

        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result.Result);
        var role = Assert.IsType<RoleDto>(createdResult.Value);
        Assert.Equal($"/api/roles/{createdRole.Id}", createdResult.Location);
        Assert.Equal(createdRole.Id, role.Id);
        Assert.Equal("Viewer", role.Name);
    }

    [Fact]
    public async Task Create_Should_Throw_ValidationException_When_Name_Empty()
    {
        // Arrange
        var controller = BuildController();
        var request = new RoleRequestDto { Name = "", Description = "Valid description" };

        // Act & Assert
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            controller.Create(request, TestContext.Current.CancellationToken)
        );
    }

    // ── Update ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_Should_Return_Ok_With_Updated_Role()
    {
        // Arrange
        var updatedRole = new RoleDto
        {
            Id = 1,
            Name = "Super Admin",
            Description = "Updated description",
            Permissions = [new PermissionDto { Id = "ShiftsView", Description = "View shifts" }],
        };
        var controller = BuildController(new FakeRoleService { UpdateResult = updatedRole });
        var request = new UpdateRoleRequestDto
        {
            Id = 1,
            Name = "Super Admin",
            Description = "Updated description",
            PermissionIds = ["ShiftsView"],
            ConcurrencyToken = 0,
        };

        // Act
        var result = await controller.Update(1, request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var role = Assert.IsType<RoleDto>(okResult.Value);
        Assert.Equal("Super Admin", role.Name);
        Assert.Single(role.Permissions);
    }

    [Fact]
    public async Task Update_WhenRouteIdDoesNotMatchBodyId_ReturnsBadRequest()
    {
        // Arrange
        var controller = BuildController();
        var request = new UpdateRoleRequestDto
        {
            Id = 2,
            Name = "Some Role",
            Description = "Description",
            ConcurrencyToken = 0,
        };

        // Act
        var result = await controller.Update(99, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<BadRequestResult>(result.Result);
    }

    [Fact]
    public async Task Update_Should_Throw_ValidationException_When_Name_Empty()
    {
        // Arrange
        var controller = BuildController();
        var request = new UpdateRoleRequestDto
        {
            Id = 1,
            Name = "",
            Description = "Valid",
            ConcurrencyToken = 0,
        };

        // Act & Assert
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            controller.Update(1, request, TestContext.Current.CancellationToken)
        );
    }

    // ── Fake ─────────────────────────────────────────────────────────────────

    private sealed class FakeRoleService : IRoleService
    {
        public IReadOnlyCollection<RoleDto>? GetAllResult { get; set; }
        public RoleDto? CreateResult { get; set; }
        public RoleDto? UpdateResult { get; set; }

        public Task<IReadOnlyCollection<RoleDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(GetAllResult ?? (IReadOnlyCollection<RoleDto>)new List<RoleDto>());

        public Task<RoleDto> CreateAsync(RoleRequestDto request, CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateResult ?? new RoleDto());

        public Task<RoleDto> UpdateAsync(UpdateRoleRequestDto request, CancellationToken cancellationToken = default) =>
            Task.FromResult(UpdateResult ?? new RoleDto());
    }
}
