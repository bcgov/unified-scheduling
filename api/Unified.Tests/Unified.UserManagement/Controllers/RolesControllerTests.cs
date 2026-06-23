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

    [Fact]
    public async Task GetAssignedUsers_Should_Return_Ok_With_Users()
    {
        // Arrange
        var expectedUsers = new List<RoleAssignedUserDto>
        {
            new()
            {
                UserId = Guid.NewGuid(),
                FirstName = "Jane",
                LastName = "Doe",
                Email = "jane.doe@example.com",
            },
            new()
            {
                UserId = Guid.NewGuid(),
                FirstName = "John",
                LastName = "Smith",
                Email = "john.smith@example.com",
            },
        };
        var controller = BuildController(new FakeRoleService { GetAssignedUsersResult = expectedUsers });

        // Act
        var result = await controller.GetAssignedUsers(7, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var users = Assert.IsAssignableFrom<IEnumerable<RoleAssignedUserDto>>(okResult.Value);
        Assert.Equal(2, users.Count());
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

    // ── ReassignAndDelete ────────────────────────────────────────────────────

    [Fact]
    public async Task ReassignAndDelete_Should_Return_Ok_With_Delete_Metadata_And_Call_ReassignAndDeleteAsync()
    {
        var service = new FakeRoleService();
        var controller = BuildController(service);
        var request = new DeleteRoleWithReassignmentRequestDto
        {
            NewRoleId = 8,
            NewRoleEffectiveDate = "2026-01-10",
            NewRoleExpiryDate = "2026-01-20",
        };

        var result = await controller.ReassignAndDelete(7, request, TestContext.Current.CancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<DeletedRoleDto>(okResult.Value);
        Assert.Equal(7, payload.Id);
        Assert.Equal(service.DeletedBy, payload.DeletedBy);
        Assert.Equal(service.DeletedOn, payload.DeletedOn);
        Assert.Equal(7, service.DeletedRoleId);
        Assert.Equal(1, service.ReassignAndDeleteCallCount);
        Assert.NotNull(service.LastReassignAndDeleteRequest);
        Assert.Equal(8, service.LastReassignAndDeleteRequest!.NewRoleId);
    }

    [Fact]
    public async Task ReassignAndDelete_Should_Return_Ok_When_No_Reassignment_Info_Provided()
    {
        // No reassignment fields needed when the role has no assigned users —
        // that validation is handled at service level, not controller level.
        var service = new FakeRoleService();
        var controller = BuildController(service);
        var request = new DeleteRoleWithReassignmentRequestDto();

        var result = await controller.ReassignAndDelete(7, request, TestContext.Current.CancellationToken);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(1, service.ReassignAndDeleteCallCount);
    }

    // ── Fake ─────────────────────────────────────────────────────────────────

    private sealed class FakeRoleService : IRoleService
    {
        public IReadOnlyCollection<RoleDto>? GetAllResult { get; set; }
        public RoleDto? CreateResult { get; set; }
        public RoleDto? UpdateResult { get; set; }
        public IReadOnlyCollection<RoleAssignedUserDto>? GetAssignedUsersResult { get; set; }
        public int? DeletedRoleId { get; private set; }
        public int ReassignAndDeleteCallCount { get; private set; }
        public DeleteRoleWithReassignmentRequestDto? LastReassignAndDeleteRequest { get; private set; }
        public Guid DeletedBy { get; } = Guid.NewGuid();
        public DateTimeOffset DeletedOn { get; } = DateTimeOffset.UtcNow;

        public Task<IReadOnlyCollection<RoleDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(GetAllResult ?? (IReadOnlyCollection<RoleDto>)new List<RoleDto>());

        public Task<RoleDto> CreateAsync(RoleRequestDto request, CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateResult ?? new RoleDto());

        public Task<IReadOnlyCollection<RoleAssignedUserDto>> GetAssignedUsersAsync(
            int roleId,
            CancellationToken cancellationToken = default
        ) =>
            Task.FromResult(
                GetAssignedUsersResult ?? (IReadOnlyCollection<RoleAssignedUserDto>)new List<RoleAssignedUserDto>()
            );

        public Task<RoleDto> UpdateAsync(UpdateRoleRequestDto request, CancellationToken cancellationToken = default) =>
            Task.FromResult(UpdateResult ?? new RoleDto());

        public Task<DeletedRoleDto> ReassignAndDeleteAsync(
            int roleIdToDelete,
            DeleteRoleWithReassignmentRequestDto request,
            CancellationToken cancellationToken = default
        )
        {
            DeletedRoleId = roleIdToDelete;
            ReassignAndDeleteCallCount++;
            LastReassignAndDeleteRequest = request;

            return Task.FromResult(
                new DeletedRoleDto
                {
                    Id = roleIdToDelete,
                    DeletedBy = DeletedBy,
                    DeletedOn = DeletedOn,
                }
            );
        }
    }
}
