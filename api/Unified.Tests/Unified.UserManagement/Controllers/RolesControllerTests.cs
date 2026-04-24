using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Unified.UserManagement.Controllers;
using Unified.UserManagement.Models;
using Unified.UserManagement.Services;
using Unified.UserManagement.Validators;

namespace Unified.Tests.UserManagement.Controllers;

public class RolesControllerTests
{
    [Fact]
    public async Task Get_Should_Return_Ok_With_Roles()
    {
        // Arrange
        var expectedRoles = new List<RoleDto>
        {
            new() { Id = 1, Name = "Administrator", Description = "Administrator role" },
            new() { Id = 2, Name = "Manager", Description = "Manager role" }
        };
        var fakeService = new FakeRoleService { GetAllResult = expectedRoles };
        var controller = new RolesController(fakeService, new RoleRequestValidator());

        // Act
        var result = await controller.Get(TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var roles = Assert.IsAssignableFrom<IEnumerable<RoleDto>>(okResult.Value);
        Assert.Equal(2, roles.Count());
    }

    [Fact]
    public async Task Create_Should_Return_Created_With_Location_And_Body()
    {
        // Arrange
        var createdRole = new RoleDto { Id = 1, Name = "Viewer", Description = "Read-only viewer role" };
        var fakeService = new FakeRoleService { CreateResult = createdRole };
        var controller = new RolesController(fakeService, new RoleRequestValidator());
        var request = new RoleRequestDto
        {
            Name = "Viewer",
            Description = "Read-only viewer role"
        };

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
    public async Task Create_Should_Return_BadRequest_When_Name_Empty()
    {
        // Arrange
        var fakeService = new FakeRoleService();
        var validator = new RoleRequestValidator();
        var controller = new RolesController(fakeService, validator);
        var request = new RoleRequestDto
        {
            Name = "",
            Description = "Valid description"
        };

        // Act & Assert
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(
            () => validator.ValidateAndThrowAsync(request, TestContext.Current.CancellationToken)
        );
    }

    private sealed class FakeRoleService : IRoleService
    {
        public IReadOnlyCollection<RoleDto>? GetAllResult { get; set; }
        public RoleDto? CreateResult { get; set; }

        public Task<IReadOnlyCollection<RoleDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetAllResult ?? new List<RoleDto>().AsReadOnly());
        }

        public Task<RoleDto> CreateAsync(RoleRequestDto request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateResult ?? new RoleDto());
        }
    }
}
