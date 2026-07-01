using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Helpers.Extensions;
using Unified.Common.Logging;
using Unified.Db;
using Unified.Db.Models.UserManagement;
using Unified.FeatureFlags;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Services;

public sealed class UserService(UnifiedDbContext DB, IFeatureFlags featureFlags, ILogger<UserService> logger)
    : IUserService
{
    public async Task<IReadOnlyCollection<UserResponse>> GetAllAsync(
        UserQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug(
            "Retrieving users with search provided {HasSearch}, search length {SearchLength}, location {LocationId}, enabled {IsEnabled}",
            LogSanitizer.HasValue(queryParams?.Search),
            LogSanitizer.Length(queryParams?.Search),
            queryParams?.LocationId,
            queryParams?.IsEnabled
        );

        var query = DB.Users.AsNoTracking();

        if (queryParams?.Search?.Trim() is { Length: > 0 } searchText)
        {
            query = featureFlags.Current switch
            {
                { UserBadgeNumber: true } => query.Where(x =>
                    x.FirstName.Contains(searchText)
                    || x.LastName.Contains(searchText)
                    || (x.BadgeNumber ?? string.Empty).Contains(searchText)
                ),
                _ => query.Where(x => x.FirstName.Contains(searchText) || x.LastName.Contains(searchText)),
            };
        }

        if (queryParams?.LocationId is int locationId)
        {
            query = query.Where(x => x.HomeLocationId == locationId);
        }

        if (queryParams?.IsEnabled is bool isEnabled)
        {
            query = query.Where(x => x.IsEnabled == isEnabled);
        }

        return await query
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ProjectToType<UserResponse>()
            .ToListAsync(cancellationToken);
    }

    public async Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DB
            .Users.AsNoTracking()
            .Where(x => x.Id == id)
            .ProjectToType<UserResponse>()
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<UserResponse> CreateAsync(UserRequestDto request, CancellationToken cancellationToken = default)
    {
        var userEntity = new User
        {
            Id = Guid.NewGuid(),
            IdirName = request.IdirName!.Trim(),
            IsEnabled = request.IsEnabled,
            FirstName = request.FirstName!.Trim(),
            LastName = request.LastName!.Trim(),
            Email = request.Email!.Trim(),
            Gender = request.Gender,
            Rank = request.Rank?.Trim(),
            BadgeNumber = request.BadgeNumber?.Trim(),
            HomeLocationId = request.HomeLocationId,
            LastLogin = DateTimeOffset.Now,
        };

        DB.Users.Add(userEntity);
        await DB.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created user {UserId}", userEntity.Id);

        return userEntity.Adapt<UserResponse>();
    }

    public async Task<UserResponse?> UpdateAsync(
        Guid id,
        UserRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        var userEntity = await DB.Users.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (userEntity is null)
        {
            logger.LogDebug("User {UserId} was not found for update", id);
            return null;
        }

        userEntity.IsEnabled = request.IsEnabled;
        userEntity.FirstName = request.FirstName!.Trim();
        userEntity.LastName = request.LastName!.Trim();
        userEntity.Email = request.Email!.Trim();
        userEntity.IdirName = request.IdirName!.Trim();
        userEntity.Gender = request.Gender;
        userEntity.Rank = request.Rank?.Trim();
        userEntity.BadgeNumber = request.BadgeNumber?.Trim();
        userEntity.HomeLocationId = request.HomeLocationId;
        userEntity.LastLogin = DateTimeOffset.Now;

        await DB.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated user {UserId}", userEntity.Id);

        return userEntity.Adapt<UserResponse>();
    }

    public async Task<IReadOnlyCollection<UserRoleResponseDto>> GetRolesAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var user = await DB
            .Users.AsNoTracking()
            .Include(x => x.HomeLocation)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException($"User {id} not found.");
        }

        var timezoneId = user.HomeLocation?.Timezone;

        var userRoles = await DB
            .UserRoles.AsNoTracking()
            .Where(x => x.UserId == id)
            .OrderByDescending(x => x.EffectiveDate)
            .ThenBy(x => x.RoleId)
            .ToListAsync(cancellationToken);

        return userRoles.Select(x => MapUserRoleResponse(x, timezoneId)).ToList();
    }

    public async Task<UserRoleResponseDto> AssignRoleAsync(
        Guid id,
        AssignUserRoleRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation(
            "Assigning role {RoleId} to user {UserId} with effective date {EffectiveDate} and expiry date {ExpiryDate}",
            request.RoleId,
            id,
            request.EffectiveDate,
            request.ExpiryDate
        );

        var user = await DB
            .Users.AsNoTracking()
            .Include(x => x.HomeLocation)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException($"User {id} not found.");
        }

        var effectiveDateUtc = DateTimeOffsetExtensions.FromDateStringToStartOfDayInTimeZone(
            request.EffectiveDate,
            user.HomeLocation?.Timezone
        );
        DateTimeOffset? expiryDateUtc = !string.IsNullOrEmpty(request.ExpiryDate)
            ? DateTimeOffsetExtensions.FromDateStringToEndOfDayInTimeZone(
                request.ExpiryDate,
                user.HomeLocation?.Timezone
            )
            : null;

        var roleExists = await DB.Roles.AnyAsync(r => r.Id == request.RoleId, cancellationToken);
        if (!roleExists)
        {
            throw new KeyNotFoundException($"Role {request.RoleId} not found.");
        }

        var userRole = await DB.UserRoles.SingleOrDefaultAsync(
            ur => ur.UserId == id && ur.RoleId == request.RoleId,
            cancellationToken
        );

        UserRole assignedUserRole;

        if (userRole is null)
        {
            logger.LogDebug("Creating new user role for user {UserId} and role {RoleId}", id, request.RoleId);
            assignedUserRole = new UserRole
            {
                UserId = id,
                RoleId = request.RoleId,
                EffectiveDate = effectiveDateUtc,
                ExpiryDate = expiryDateUtc,
                ExpiryReason = null,
            };

            DB.UserRoles.Add(assignedUserRole);
        }
        else
        {
            logger.LogDebug(
                "Updating existing user role (ID {UserRoleId}) for user {UserId} and role {RoleId}",
                userRole.Id,
                id,
                request.RoleId
            );
            userRole.EffectiveDate = effectiveDateUtc;
            userRole.ExpiryDate = expiryDateUtc;
            userRole.ExpiryReason = null;

            assignedUserRole = userRole;
        }

        await DB.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Assigned role {RoleId} to user {UserId}", request.RoleId, id);

        var timezoneId = user.HomeLocation?.Timezone;

        return MapUserRoleResponse(assignedUserRole, timezoneId);
    }

    public async Task<UserRoleResponseDto> ExpireRoleAsync(
        Guid id,
        ExpireUserRoleRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        var user = await DB
            .Users.AsNoTracking()
            .Include(x => x.HomeLocation)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            throw new KeyNotFoundException($"User {id} not found.");
        }

        var userRole = await DB.UserRoles.SingleOrDefaultAsync(
            ur => ur.UserId == id && ur.RoleId == request.RoleId,
            cancellationToken
        );

        if (userRole is null)
        {
            throw new KeyNotFoundException($"Role assignment for user {id} and role {request.RoleId} not found.");
        }

        userRole.ExpiryDate = DateTimeOffset.UtcNow;
        userRole.ExpiryReason = request.ExpiryReason;

        await DB.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Expired role {RoleId} for user {UserId} with reason provided {HasExpiryReason}",
            request.RoleId,
            id,
            LogSanitizer.HasValue(request.ExpiryReason)
        );

        return MapUserRoleResponse(userRole, user.HomeLocation?.Timezone);
    }

    private static UserRoleResponseDto MapUserRoleResponse(UserRole userRole, string? timezoneId)
    {
        return new UserRoleResponseDto
        {
            Id = userRole.Id,
            UserId = userRole.UserId,
            RoleId = userRole.RoleId,
            EffectiveDate = userRole.EffectiveDate.ToTimeZone(timezoneId),
            ExpiryDate = userRole.ExpiryDate?.ToTimeZone(timezoneId),
            ExpiryReason = userRole.ExpiryReason,
        };
    }
}
