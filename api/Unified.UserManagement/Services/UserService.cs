using Mapster;
using Microsoft.EntityFrameworkCore;
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

    public async Task<UserResponse> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var userEntity = new User
        {
            Id = Guid.NewGuid(),
            IdirName = request.IdirName.Trim(),
            IdirId = request.IdirId,
            IsEnabled = request.IsEnabled,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email.Trim(),
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
        UpdateUserRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var userEntity = await DB.Users.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (userEntity is null)
        {
            return null;
        }

        userEntity.IsEnabled = request.IsEnabled;
        userEntity.FirstName = request.FirstName.Trim();
        userEntity.LastName = request.LastName.Trim();
        userEntity.Email = request.Email.Trim();
        userEntity.HomeLocationId = request.HomeLocationId;
        userEntity.LastLogin = DateTimeOffset.Now;

        await DB.SaveChangesAsync(cancellationToken);

        return userEntity.Adapt<UserResponse>();
    }
}
