using System.Data.Common;
using System.Security.Claims;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.UserManagement;
using Unified.Tests.TestHelpers;
using Unified.UserManagement.Services;

namespace Unified.Tests.UserManagement.Services;

public class UserAccountResolutionServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private UnifiedDbContext _dbContext = null!;
    private UserAccountResolutionService _service = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        var options = new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(_connection).Options;

        _dbContext = new SqliteTestUnifiedDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        _service = new UserAccountResolutionService(_dbContext, NullLogger<UserAccountResolutionService>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task ResolveCurrentUserAsync_Should_Link_Pending_User_Using_Normalized_IdirUsername()
    {
        var idirId = Guid.NewGuid();
        var keyCloakId = Guid.NewGuid();
        var user = await SeedUserAsync(
            idirName: "pending.user",
            idirId: null,
            keyCloakId: null,
            pendingRegistration: true,
            lastLogin: null
        );

        var result = await _service.ResolveCurrentUserAsync(
            CreatePrincipal(
                new Claim(UnifiedClaimTypes.IdirId, idirId.ToString()),
                new Claim("idir_username", "  PENDING.USER  "),
                new Claim(ClaimTypes.NameIdentifier, keyCloakId.ToString())
            ),
            recordLogin: true,
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);

        var userInDb = await _dbContext
            .Users.AsNoTracking()
            .SingleAsync(u => u.Id == user.Id, TestContext.Current.CancellationToken);
        Assert.Equal("pending.user", userInDb.IdirName);
        Assert.Equal(idirId, userInDb.IdirId);
        Assert.Equal(keyCloakId, userInDb.KeyCloakId);
        Assert.False(userInDb.PendingRegistration);
        Assert.NotNull(userInDb.LastLogin);
    }

    [Fact]
    public async Task UpdateCurrentUserLastLoginAsync_Should_Update_LastLogin_When_UserId_Claim_Is_Present()
    {
        var originalLastLogin = DateTimeOffset.UtcNow.AddDays(-2);
        var user = await SeedUserAsync(
            idirName: "linked.lastlogin.user",
            idirId: Guid.NewGuid(),
            keyCloakId: Guid.NewGuid(),
            pendingRegistration: false,
            lastLogin: originalLastLogin
        );

        await _service.UpdateCurrentUserLastLoginAsync(
            CreatePrincipal(new Claim(UnifiedClaimTypes.UserId, user.Id.ToString())),
            TestContext.Current.CancellationToken
        );

        var userInDb = await _dbContext
            .Users.AsNoTracking()
            .SingleAsync(u => u.Id == user.Id, TestContext.Current.CancellationToken);
        Assert.True(userInDb.LastLogin > originalLastLogin);
    }

    [Fact]
    public async Task UpdateCurrentUserLastLoginAsync_Should_Do_Nothing_When_UserId_Claim_Is_Missing()
    {
        var originalLastLogin = DateTimeOffset.UtcNow.AddDays(-2);
        var user = await SeedUserAsync(
            idirName: "missing.claim.user",
            idirId: Guid.NewGuid(),
            keyCloakId: Guid.NewGuid(),
            pendingRegistration: false,
            lastLogin: originalLastLogin
        );

        await _service.UpdateCurrentUserLastLoginAsync(
            CreatePrincipal(new Claim(ClaimTypes.Name, "missing-claim-user")),
            TestContext.Current.CancellationToken
        );

        var userInDb = await _dbContext.Users.SingleAsync(u => u.Id == user.Id, TestContext.Current.CancellationToken);
        Assert.Equal(originalLastLogin, userInDb.LastLogin);
    }

    [Fact]
    public async Task ResolveCurrentUserAsync_Should_Throw_When_StableIdentity_Maps_To_Multiple_Users()
    {
        var keyCloakId = Guid.NewGuid();
        var idirId = Guid.NewGuid();

        await SeedUserAsync(
            idirName: "keycloak.user",
            idirId: null,
            keyCloakId: keyCloakId,
            pendingRegistration: false,
            lastLogin: null
        );
        await SeedUserAsync(
            idirName: "idir.user",
            idirId: idirId,
            keyCloakId: null,
            pendingRegistration: false,
            lastLogin: null
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ResolveCurrentUserAsync(
                CreatePrincipal(
                    new Claim(ClaimTypes.NameIdentifier, keyCloakId.ToString()),
                    new Claim(UnifiedClaimTypes.IdirId, idirId.ToString())
                ),
                false,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task ResolveCurrentUserAsync_Should_Return_Null_For_Disabled_User()
    {
        var keyCloakId = Guid.NewGuid();
        var disabledUser = await SeedUserAsync(
            idirName: "disabled.user",
            idirId: Guid.NewGuid(),
            keyCloakId: keyCloakId,
            pendingRegistration: false,
            lastLogin: null
        );
        disabledUser.IsEnabled = false;
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.ResolveCurrentUserAsync(
            CreatePrincipal(new Claim(ClaimTypes.NameIdentifier, keyCloakId.ToString())),
            recordLogin: false,
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveCurrentUserAsync_Should_Link_Pending_User_With_Relational_Conditional_Update_And_Reload_Graph()
    {
        var idirId = Guid.NewGuid();
        var keyCloakId = Guid.NewGuid();
        var (connection, dbContext) = await CreateSqliteDbContextAsync();

        var user = await SeedUserAsync(
            dbContext,
            idirName: "pending.relational",
            idirId: null,
            keyCloakId: null,
            pendingRegistration: true,
            lastLogin: null
        );

        var service = new UserAccountResolutionService(dbContext, NullLogger<UserAccountResolutionService>.Instance);

        var result = await service.ResolveCurrentUserAsync(
            CreatePrincipal(
                new Claim(UnifiedClaimTypes.IdirId, idirId.ToString()),
                new Claim("idir_username", "pending.relational"),
                new Claim(ClaimTypes.NameIdentifier, keyCloakId.ToString())
            ),
            recordLogin: false,
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal(idirId, result.IdirId);
        Assert.Equal(keyCloakId, result.KeyCloakId);
        Assert.False(result.PendingRegistration);
        Assert.NotNull(result.LastLogin);
        Assert.NotNull(result.HomeLocation);
        Assert.Contains(
            result.ActiveUserRoles,
            ur => ur.Role.RolePermissions.Any(rp => rp.PermissionId == Permissions.ShiftsEdit.ToString())
        );

        await connection.DisposeAsync();
    }

    [Fact]
    public async Task ResolveCurrentUserAsync_Should_Return_Null_When_Conditional_Link_Updates_Zero_Rows()
    {
        var (connection, dbContext) = await CreateSqliteDbContextAsync(
            new PendingRegistrationChangedCommandInterceptor()
        );
        var user = await SeedUserAsync(
            dbContext,
            idirName: "pending.changed",
            idirId: null,
            keyCloakId: null,
            pendingRegistration: true,
            lastLogin: null
        );

        var service = new UserAccountResolutionService(dbContext, NullLogger<UserAccountResolutionService>.Instance);

        var result = await service.ResolveCurrentUserAsync(
            CreatePrincipal(
                new Claim(UnifiedClaimTypes.IdirId, Guid.NewGuid().ToString()),
                new Claim("idir_username", "pending.changed"),
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
            ),
            recordLogin: false,
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Null(result);

        var userInDb = await dbContext
            .Users.AsNoTracking()
            .SingleAsync(u => u.Id == user.Id, TestContext.Current.CancellationToken);
        Assert.Null(userInDb.IdirId);
        Assert.Null(userInDb.KeyCloakId);
        Assert.True(userInDb.PendingRegistration);
        Assert.Null(userInDb.LastLogin);

        await connection.DisposeAsync();
    }

    [Fact]
    public async Task ResolveCurrentUserAsync_Should_Not_Overwrite_User_Linked_By_First_Request()
    {
        var (connection, dbContext) = await CreateSqliteDbContextAsync();
        var user = await SeedUserAsync(
            dbContext,
            idirName: "pending.race",
            idirId: null,
            keyCloakId: null,
            pendingRegistration: true,
            lastLogin: null
        );

        var service = new UserAccountResolutionService(dbContext, NullLogger<UserAccountResolutionService>.Instance);
        var winningIdirId = Guid.NewGuid();
        var winningKeyCloakId = Guid.NewGuid();

        var winner = await service.ResolveCurrentUserAsync(
            CreatePrincipal(
                new Claim(UnifiedClaimTypes.IdirId, winningIdirId.ToString()),
                new Claim("idir_username", "pending.race"),
                new Claim(ClaimTypes.NameIdentifier, winningKeyCloakId.ToString())
            ),
            recordLogin: false,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var loser = await service.ResolveCurrentUserAsync(
            CreatePrincipal(
                new Claim(UnifiedClaimTypes.IdirId, Guid.NewGuid().ToString()),
                new Claim("idir_username", "pending.race"),
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
            ),
            recordLogin: false,
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.NotNull(winner);
        Assert.Null(loser);

        var userInDb = await dbContext
            .Users.AsNoTracking()
            .SingleAsync(u => u.Id == user.Id, TestContext.Current.CancellationToken);
        Assert.Equal(winningIdirId, userInDb.IdirId);
        Assert.Equal(winningKeyCloakId, userInDb.KeyCloakId);
        Assert.False(userInDb.PendingRegistration);

        await connection.DisposeAsync();
    }

    [Fact]
    public async Task ResolveCurrentUserAsync_Should_Throw_When_Pending_Link_StableIdentity_Already_Exists()
    {
        var keyCloakId = Guid.NewGuid();
        var existingUser = await SeedUserAsync(
            idirName: "existing.identity",
            idirId: Guid.NewGuid(),
            keyCloakId: keyCloakId,
            pendingRegistration: false,
            lastLogin: null
        );
        existingUser.IsEnabled = false;
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await SeedUserAsync(
            idirName: "pending.identity",
            idirId: null,
            keyCloakId: null,
            pendingRegistration: true,
            lastLogin: null
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ResolveCurrentUserAsync(
                CreatePrincipal(
                    new Claim(UnifiedClaimTypes.IdirId, Guid.NewGuid().ToString()),
                    new Claim("idir_username", "pending.identity"),
                    new Claim(ClaimTypes.NameIdentifier, keyCloakId.ToString())
                ),
                recordLogin: false,
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
    }

    private async Task<User> SeedUserAsync(
        string idirName,
        Guid? idirId,
        Guid? keyCloakId,
        bool pendingRegistration,
        DateTimeOffset? lastLogin
    )
    {
        return await SeedUserAsync(_dbContext, idirName, idirId, keyCloakId, pendingRegistration, lastLogin);
    }

    private static async Task<User> SeedUserAsync(
        UnifiedDbContext dbContext,
        string idirName,
        Guid? idirId,
        Guid? keyCloakId,
        bool pendingRegistration,
        DateTimeOffset? lastLogin
    )
    {
        var location =
            await dbContext.Locations.FindAsync([1], TestContext.Current.CancellationToken)
            ?? new Location
            {
                Id = 1,
                AgencyId = "LOC-001",
                Name = "Vancouver Court",
                Timezone = "America/Vancouver",
                CreatedOn = DateTimeOffset.UtcNow,
            };
        var permission =
            await dbContext.Permissions.FindAsync(
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
            await dbContext.Roles.FindAsync([1], TestContext.Current.CancellationToken)
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
            IdirName = idirName,
            IdirId = idirId,
            KeyCloakId = keyCloakId,
            IsEnabled = true,
            FirstName = "Test",
            LastName = "User",
            Email = $"{idirName}@example.com",
            Gender = Gender.Other,
            HomeLocationId = location.Id,
            PendingRegistration = pendingRegistration,
            LastLogin = lastLogin,
            CreatedOn = DateTimeOffset.UtcNow,
        };
        var rolePermission = await dbContext.RolePermissions.FindAsync([1], TestContext.Current.CancellationToken);
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
            Id = await dbContext.UserRoles.CountAsync(TestContext.Current.CancellationToken) + 1,
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

        if (dbContext.Entry(location).State == EntityState.Detached)
        {
            dbContext.Locations.Add(location);
        }

        if (dbContext.Entry(permission).State == EntityState.Detached)
        {
            dbContext.Permissions.Add(permission);
        }

        if (dbContext.Entry(role).State == EntityState.Detached)
        {
            dbContext.Roles.Add(role);
        }

        if (dbContext.Entry(rolePermission).State == EntityState.Detached)
        {
            dbContext.RolePermissions.Add(rolePermission);
        }

        dbContext.Users.Add(user);
        dbContext.UserRoles.Add(userRole);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return user;
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private static async Task<(SqliteConnection Connection, UnifiedDbContext DbContext)> CreateSqliteDbContextAsync(
        params IInterceptor[] interceptors
    )
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var optionsBuilder = new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(connection);

        if (interceptors.Length > 0)
        {
            optionsBuilder.AddInterceptors(interceptors);
        }

        var dbContext = new SqliteTestUnifiedDbContext(optionsBuilder.Options);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        return (connection, dbContext);
    }

    private sealed class PendingRegistrationChangedCommandInterceptor : DbCommandInterceptor
    {
        private bool _hasChangedPendingRegistration;

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            if (
                !_hasChangedPendingRegistration
                && command.CommandText.TrimStart().StartsWith("UPDATE \"Users\"", StringComparison.Ordinal)
            )
            {
                _hasChangedPendingRegistration = true;
                await using var conflictCommand = command.Connection!.CreateCommand();
                conflictCommand.Transaction = command.Transaction;
                conflictCommand.CommandText =
                    "UPDATE \"Users\" SET \"PendingRegistration\" = 0 WHERE \"PendingRegistration\" = 1";
                await conflictCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
