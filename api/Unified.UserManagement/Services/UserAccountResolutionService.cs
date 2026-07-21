using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.Common.Logging;
using Unified.Db;
using Unified.Db.Models.UserManagement;
using Unified.UserManagement.Queries;

namespace Unified.UserManagement.Services;

public sealed class UserAccountResolutionService(UnifiedDbContext db, ILogger<UserAccountResolutionService> logger)
    : IUserAccountResolutionService
{
    public async Task UpdateCurrentUserLastLoginAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default
    )
    {
        var userIdValue = principal.FindFirstValue(UnifiedClaimTypes.UserId);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        await db
            .Users.Where(user => user.Id == userId && user.IsEnabled)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.LastLogin, now), cancellationToken);
    }

    public async Task<User?> ResolveCurrentUserAsync(
        ClaimsPrincipal principal,
        bool recordLogin = false,
        CancellationToken cancellationToken = default
    )
    {
        var authenticatedUser = AuthenticatedUserClaims.FromPrincipal(principal);

        logger.LogDebug(
            "Resolving authenticated user with keycloak id present {HasKeycloakId}, idir id present {HasIdirId}, idir username present {HasIdirUsername}",
            authenticatedUser.KeyCloakId.HasValue,
            authenticatedUser.IdirId.HasValue,
            LogSanitizer.HasValue(authenticatedUser.NormalizedIdirUsername)
        );

        if (!authenticatedUser.HasStableIdentity && string.IsNullOrWhiteSpace(authenticatedUser.NormalizedIdirUsername))
        {
            return null;
        }

        var resolvedUser = await ResolveLinkedUserAsync(authenticatedUser, cancellationToken);
        if (resolvedUser is not null)
        {
            if (recordLogin)
            {
                await UpdateLastLoginAsync(resolvedUser, cancellationToken);
            }

            return resolvedUser;
        }

        if (!authenticatedUser.HasStableIdentity || string.IsNullOrWhiteSpace(authenticatedUser.NormalizedIdirUsername))
        {
            return null;
        }

        return await LinkPendingRegistrationAsync(authenticatedUser, cancellationToken);
    }

    private async Task<User?> ResolveLinkedUserAsync(
        AuthenticatedUserClaims authenticatedUser,
        CancellationToken cancellationToken
    )
    {
        User? userByKeyCloakId = null;
        User? userByIdirId = null;

        if (authenticatedUser.KeyCloakId.HasValue)
        {
            userByKeyCloakId = await CreateEnabledUserQuery()
                .SingleOrDefaultAsync(user => user.KeyCloakId == authenticatedUser.KeyCloakId.Value, cancellationToken);
        }

        if (authenticatedUser.IdirId.HasValue)
        {
            userByIdirId = await CreateEnabledUserQuery()
                .SingleOrDefaultAsync(user => user.IdirId == authenticatedUser.IdirId.Value, cancellationToken);
        }

        if (userByKeyCloakId is not null && userByIdirId is not null && userByKeyCloakId.Id != userByIdirId.Id)
        {
            logger.LogWarning(
                "Authenticated principal resolved to multiple local users for keycloak id {KeyCloakId} and idir id {IdirId}",
                authenticatedUser.KeyCloakId,
                authenticatedUser.IdirId
            );

            throw new InvalidOperationException(
                "The authenticated identity is already linked to multiple local users."
            );
        }

        return userByIdirId;
    }

    private async Task UpdateLastLoginAsync(User user, CancellationToken cancellationToken)
    {
        user.LastLogin = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<User?> LinkPendingRegistrationAsync(
        AuthenticatedUserClaims authenticatedUser,
        CancellationToken cancellationToken
    )
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var linkedUser = await LinkPendingRegistrationAtomicallyAsync(authenticatedUser, cancellationToken);

        if (linkedUser is null)
        {
            return null;
        }

        await transaction.CommitAsync(cancellationToken);

        return linkedUser;
    }

    private async Task<User?> LinkPendingRegistrationAtomicallyAsync(
        AuthenticatedUserClaims authenticatedUser,
        CancellationToken cancellationToken
    )
    {
        var pendingUser = await db
            .Users.AsNoTracking()
            .GetPendingUsersByIdirName(authenticatedUser.NormalizedIdirUsername!)
            .SingleOrDefaultAsync(cancellationToken);

        if (pendingUser is null)
        {
            return null;
        }

        await EnsureStableIdentityIsUniqueAsync(
            pendingUser.Id,
            authenticatedUser.IdirId,
            authenticatedUser.KeyCloakId,
            cancellationToken
        );

        var now = DateTimeOffset.UtcNow;

        var updatedRows = await db
            .Users.GetPendingUsersByIdirName(authenticatedUser.NormalizedIdirUsername!)
            .Where(user => user.Id == pendingUser.Id)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(user => user.IdirId, authenticatedUser.IdirId)
                        .SetProperty(user => user.KeyCloakId, authenticatedUser.KeyCloakId)
                        .SetProperty(user => user.PendingRegistration, false)
                        .SetProperty(user => user.LastLogin, now),
                cancellationToken
            );

        if (updatedRows != 1)
        {
            logger.LogWarning(
                "Rejected pending registration link for user {UserId} because the account was already linked or changed",
                pendingUser.Id
            );

            return null;
        }

        var linkedUser = await CreateEnabledUserQuery(asNoTracking: true)
            .SingleAsync(user => user.Id == pendingUser.Id, cancellationToken);

        logger.LogInformation("Linked pending registration for user {UserId}", linkedUser.Id);

        return linkedUser;
    }

    private async Task EnsureStableIdentityIsUniqueAsync(
        Guid userId,
        Guid? idirId,
        Guid? keyCloakId,
        CancellationToken cancellationToken
    )
    {
        var hasConflict = await db.Users.AnyAsync(
            user =>
                user.Id != userId
                && (
                    (idirId.HasValue && user.IdirId == idirId.Value)
                    || (keyCloakId.HasValue && user.KeyCloakId == keyCloakId.Value)
                ),
            cancellationToken
        );

        if (!hasConflict)
        {
            return;
        }

        logger.LogWarning(
            "Rejected identity link for user {UserId} because the authenticated stable identity is already linked elsewhere",
            userId
        );

        throw new InvalidOperationException("The authenticated stable identity is already linked to another user.");
    }

    private IQueryable<User> CreateEnabledUserQuery(bool asNoTracking = false)
    {
        var query = asNoTracking ? db.Users.AsNoTracking() : db.Users;

        return query
            .Include(user => user.HomeLocation)
            .Include(user => user.UserRoles)
                .ThenInclude(userRole => userRole.Role)
                    .ThenInclude(role => role.RolePermissions)
            .Where(user => user.IsEnabled);
    }

    private sealed record AuthenticatedUserClaims(Guid? IdirId, Guid? KeyCloakId, string? NormalizedIdirUsername)
    {
        public bool HasStableIdentity => IdirId.HasValue || KeyCloakId.HasValue;

        public static AuthenticatedUserClaims FromPrincipal(ClaimsPrincipal principal)
        {
            return new AuthenticatedUserClaims(
                GetIdirId(principal),
                GetKeyCloakId(principal),
                GetNormalizedIdirUsername(principal)
            );
        }

        private static Guid? GetIdirId(ClaimsPrincipal principal)
        {
            var claimValues = new[]
            {
                principal.FindFirstValue(UnifiedClaimTypes.IdirId),
                principal.FindFirstValue("idir_user_guid"),
            };

            foreach (var claimValue in claimValues)
            {
                if (TryParseGuidClaim(claimValue, out var idirId))
                {
                    return idirId;
                }
            }

            return null;
        }

        private static Guid? GetKeyCloakId(ClaimsPrincipal principal)
        {
            return TryParseGuidClaim(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var keyCloakId)
                ? keyCloakId
                : null;
        }

        private static string? GetNormalizedIdirUsername(ClaimsPrincipal principal)
        {
            var idirUsername = principal.FindFirstValue("idir_username");
            return UserIdirNameLookup.Normalize(idirUsername);
        }

        private static bool TryParseGuidClaim(string? claimValue, out Guid parsedValue)
        {
            parsedValue = Guid.Empty;

            if (string.IsNullOrWhiteSpace(claimValue))
            {
                return false;
            }

            var normalizedValue = claimValue.Replace("@idir", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            return Guid.TryParse(normalizedValue, out parsedValue);
        }
    }
}
