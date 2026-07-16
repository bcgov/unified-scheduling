using System.Security.Claims;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.UserManagement;
using Unified.Tests.TestHelpers;
using Unified.UserManagement.Services;

namespace Unified.Tests.Authorization.Claims;

public class UnifiedClaimsTransformerTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private UnifiedDbContext _dbContext = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
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
    public async Task TransformAsync_Should_Add_AppClaims_When_User_Matches_By_IdirGuid()
    {
        var idirId = Guid.NewGuid();
        var user = await SeedUserAsync(
            idirId: idirId,
            idirName: "guid.user",
            email: "guid.user@example.com",
            pendingRegistration: false
        );
        var transformer = CreateTransformer();
        var principal = CreatePrincipal(
            new Claim(UnifiedClaimTypes.IdirId, idirId.ToString()),
            new Claim(ClaimTypes.Name, "Guid User")
        );

        var result = await transformer.TransformAsync(principal);

        Assert.True(result.HasClaim(UnifiedClaimTypes.UserId, user.Id.ToString()));
        Assert.True(result.HasClaim(UnifiedClaimTypes.FirstName, user.FirstName));
        Assert.True(result.HasClaim(UnifiedClaimTypes.LastName, user.LastName));
        Assert.True(result.HasClaim(UnifiedClaimTypes.HomeLocationId, user.HomeLocationId!.Value.ToString()));
        Assert.True(result.HasClaim(UnifiedClaimTypes.HomeLocationTimezone, "America/Vancouver"));
        Assert.Contains(result.Claims, c => c.Type == ClaimTypes.Role && c.Value == Roles.Scheduler);
        Assert.Contains(
            result.Claims,
            c => c.Type == UnifiedClaimTypes.Permission && c.Value == Permissions.ShiftsEdit.ToString()
        );
        Assert.Single(result.Claims, c => c.Type == UnifiedClaimTypes.IdirId && c.Value == idirId.ToString());
        Assert.Contains(
            result.Identities,
            identity => identity.AuthenticationType == UnifiedClaimsTransformer.ApplicationAuthenticationType
        );
    }

    [Fact]
    public async Task TransformAsync_Should_Not_Update_LastLogin_When_User_Is_Already_Linked()
    {
        var idirId = Guid.NewGuid();
        var originalLastLogin = DateTimeOffset.UtcNow.AddDays(-7);
        var user = await SeedUserAsync(
            idirId: idirId,
            idirName: "readonly.user",
            email: "readonly.user@example.com",
            pendingRegistration: false,
            lastLogin: originalLastLogin
        );
        var transformer = CreateTransformer();
        var principal = CreatePrincipal(new Claim(UnifiedClaimTypes.IdirId, idirId.ToString()));

        var result = await transformer.TransformAsync(principal);

        Assert.True(result.HasClaim(UnifiedClaimTypes.UserId, user.Id.ToString()));

        var userInDb = await _dbContext.Users.SingleAsync(u => u.Id == user.Id, TestContext.Current.CancellationToken);
        Assert.Equal(originalLastLogin, userInDb.LastLogin);
    }

    [Fact]
    public async Task TransformAsync_Should_Link_IdirGuid_When_User_Matches_By_IdirUsername()
    {
        var idirId = Guid.NewGuid();
        var keyCloakId = Guid.NewGuid();
        var user = await SeedUserAsync(
            idirId: null,
            idirName: "link.user",
            email: "link.user@example.com",
            pendingRegistration: true
        );
        var transformer = CreateTransformer();
        var principal = CreatePrincipal(
            new Claim(UnifiedClaimTypes.IdirId, idirId.ToString()),
            new Claim("idir_username", "LINK.USER"),
            new Claim(ClaimTypes.NameIdentifier, keyCloakId.ToString()),
            new Claim(ClaimTypes.Name, "Link User")
        );

        var result = await transformer.TransformAsync(principal);

        Assert.True(result.HasClaim(UnifiedClaimTypes.UserId, user.Id.ToString()));

        var userInDb = await _dbContext
            .Users.AsNoTracking()
            .SingleAsync(u => u.Id == user.Id, TestContext.Current.CancellationToken);
        Assert.Equal(idirId, userInDb.IdirId);
        Assert.Equal(keyCloakId, userInDb.KeyCloakId);
        Assert.False(userInDb.PendingRegistration);
    }

    [Fact]
    public async Task TransformAsync_Should_Return_Original_Principal_When_No_User_Is_Resolved()
    {
        var transformer = CreateTransformer();
        var principal = CreatePrincipal(
            new Claim(UnifiedClaimTypes.IdirId, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, "Unknown User")
        );

        var result = await transformer.TransformAsync(principal);

        Assert.Same(principal, result);
        Assert.False(result.HasClaim(c => c.Type == UnifiedClaimTypes.UserId));
        Assert.DoesNotContain(result.Claims, c => c.Type == UnifiedClaimTypes.Permission);
    }

    [Fact]
    public async Task TransformAsync_Should_Not_Duplicate_Claims_When_Run_Multiple_Times()
    {
        var idirId = Guid.NewGuid();
        await SeedUserAsync(
            idirId: idirId,
            idirName: "repeat.user",
            email: "repeat.user@example.com",
            pendingRegistration: false
        );
        var transformer = CreateTransformer();
        var principal = CreatePrincipal(
            new Claim(UnifiedClaimTypes.IdirId, idirId.ToString()),
            new Claim(ClaimTypes.Name, "Repeat User")
        );

        var firstResult = await transformer.TransformAsync(principal);
        var secondResult = await transformer.TransformAsync(firstResult);

        Assert.Same(firstResult, secondResult);
        Assert.Single(secondResult.Claims, c => c.Type == UnifiedClaimTypes.UserId);
        Assert.Single(secondResult.Claims, c => c.Type == UnifiedClaimTypes.Permission);
        Assert.Single(secondResult.Claims, c => c.Type == ClaimTypes.Role);
    }

    [Fact]
    public async Task TransformAsync_Should_Not_Link_When_Matching_User_Is_Not_PendingRegistration()
    {
        await SeedUserAsync(
            idirId: null,
            idirName: "registered.user",
            email: "registered.user@example.com",
            pendingRegistration: false
        );
        var transformer = CreateTransformer();
        var principal = CreatePrincipal(
            new Claim(UnifiedClaimTypes.IdirId, Guid.NewGuid().ToString()),
            new Claim("idir_username", "registered.user"),
            new Claim(ClaimTypes.Name, "Registered User")
        );

        var result = await transformer.TransformAsync(principal);

        Assert.False(result.HasClaim(c => c.Type == UnifiedClaimTypes.UserId));
    }

    [Fact]
    public async Task TransformAsync_Should_Throw_When_PendingRegistration_Link_Would_Duplicate_IdirId()
    {
        var idirId = Guid.NewGuid();
        var existingUser = await SeedUserAsync(
            idirId: idirId,
            idirName: "existing.user",
            email: "existing.user@example.com",
            pendingRegistration: false
        );
        existingUser.IsEnabled = false;
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        await SeedUserAsync(
            idirId: null,
            idirName: "pending.user",
            email: "pending.user@example.com",
            pendingRegistration: true
        );

        var transformer = CreateTransformer();
        var principal = CreatePrincipal(
            new Claim(UnifiedClaimTypes.IdirId, idirId.ToString()),
            new Claim("idir_username", "pending.user"),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() => transformer.TransformAsync(principal));
    }

    private async Task<User> SeedUserAsync(
        Guid? idirId,
        string idirName,
        string email,
        bool pendingRegistration,
        DateTimeOffset? lastLogin = null,
        Guid? keyCloakId = null
    )
    {
        var location =
            await _dbContext.Locations.FindAsync([1], TestContext.Current.CancellationToken)
            ?? new Location
            {
                Id = 1,
                AgencyId = "LOC-001",
                Name = "Vancouver Court",
                Timezone = "America/Vancouver",
                CreatedOn = DateTimeOffset.UtcNow,
            };
        var permission =
            await _dbContext.Permissions.FindAsync(
                [Permissions.ShiftsEdit.ToString()],
                TestContext.Current.CancellationToken
            )
            ?? new Permission
            {
                Id = Permissions.ShiftsEdit.ToString(),
                Group = "Scheduling",
                Description = "Edit shifts",
                CreatedOn = DateTimeOffset.UtcNow,
            };
        var role =
            await _dbContext.Roles.FindAsync([1], TestContext.Current.CancellationToken)
            ?? new Role
            {
                Id = 1,
                Name = Roles.Scheduler,
                Description = "Scheduler",
                CreatedOn = DateTimeOffset.UtcNow,
            };
        var user = new User
        {
            Id = Guid.NewGuid(),
            IdirId = idirId,
            KeyCloakId = keyCloakId,
            IdirName = idirName,
            IsEnabled = true,
            FirstName = "Test",
            LastName = "User",
            Email = email,
            Gender = Gender.Other,
            HomeLocationId = location.Id,
            PendingRegistration = pendingRegistration,
            LastLogin = lastLogin,
            CreatedOn = DateTimeOffset.UtcNow,
        };
        var rolePermission = await _dbContext.RolePermissions.FindAsync([1], TestContext.Current.CancellationToken);
        if (rolePermission is null)
        {
            rolePermission = new RolePermission
            {
                Id = 1,
                RoleId = role.Id,
                Role = role,
                PermissionId = permission.Id,
                Permission = permission,
                CreatedOn = DateTimeOffset.UtcNow,
            };
        }
        var userRole = new UserRole
        {
            Id = await _dbContext.UserRoles.CountAsync(TestContext.Current.CancellationToken) + 1,
            UserId = user.Id,
            User = user,
            RoleId = role.Id,
            Role = role,
            EffectiveDate = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedOn = DateTimeOffset.UtcNow,
        };

        if (!role.RolePermissions.Any(rp => rp.Id == rolePermission.Id))
        {
            role.RolePermissions.Add(rolePermission);
        }
        role.UserRoles.Add(userRole);
        user.UserRoles.Add(userRole);

        if (_dbContext.Entry(location).State == EntityState.Detached)
        {
            _dbContext.Locations.Add(location);
        }

        if (_dbContext.Entry(permission).State == EntityState.Detached)
        {
            _dbContext.Permissions.Add(permission);
        }

        if (_dbContext.Entry(role).State == EntityState.Detached)
        {
            _dbContext.Roles.Add(role);
        }

        if (_dbContext.Entry(rolePermission).State == EntityState.Detached)
        {
            _dbContext.RolePermissions.Add(rolePermission);
        }

        _dbContext.Users.Add(user);
        _dbContext.UserRoles.Add(userRole);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return user;
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private UnifiedClaimsTransformer CreateTransformer()
    {
        return new UnifiedClaimsTransformer(
            new UserAccountResolutionService(_dbContext, NullLogger<UserAccountResolutionService>.Instance)
        );
    }
}
