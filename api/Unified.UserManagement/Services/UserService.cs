using Mapster;
using Microsoft.EntityFrameworkCore;
using Unified.Common.Helpers.Extensions;
using Unified.Db;
using Unified.Db.Models.UserManagement;
using Unified.FeatureFlags;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Services;

public sealed class UserService(UnifiedDbContext DB, IFeatureFlags featureFlags) : IUserService
{
    public async Task<IReadOnlyCollection<UserResponse>> GetAllAsync(
        UserQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    )
    {
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

        return userEntity.Adapt<UserResponse>();
    }

    public async Task<IReadOnlyCollection<UserRoleResponseDto>> GetRolesAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var userExists = await DB.Users.AnyAsync(x => x.Id == id, cancellationToken);
        if (!userExists)
        {
            throw new KeyNotFoundException($"User {id} not found.");
        }

        return await DB
            .UserRoles.AsNoTracking()
            .Where(x => x.UserId == id)
            .OrderByDescending(x => x.EffectiveDate)
            .ThenBy(x => x.RoleId)
            .ProjectToType<UserRoleResponseDto>()
            .ToListAsync(cancellationToken);
    }

    public async Task<UserRoleResponseDto> AssignRoleAsync(
        Guid id,
        AssignUserRoleRequestDto request,
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

        var effectiveDateUtc = request.EffectiveDate.ToStartOfDayUtcInTimeZone(user.HomeLocation?.Timezone);
        DateTimeOffset? expiryDateUtc = request.ExpiryDate.HasValue
            ? request.ExpiryDate.Value.ToEndOfDayUtcInTimeZone(user.HomeLocation?.Timezone)
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
            userRole.EffectiveDate = effectiveDateUtc;
            userRole.ExpiryDate = expiryDateUtc;
            userRole.ExpiryReason = null;

            assignedUserRole = userRole;
        }

        await DB.SaveChangesAsync(cancellationToken);

        return assignedUserRole.Adapt<UserRoleResponseDto>();
    }

    public async Task<UserRoleResponseDto> ExpireRoleAsync(
        Guid id,
        ExpireUserRoleRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        var userExists = await DB.Users.AnyAsync(x => x.Id == id, cancellationToken);
        if (!userExists)
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

        return userRole.Adapt<UserRoleResponseDto>();
    }
}
