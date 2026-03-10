using Microsoft.EntityFrameworkCore;
using Unified.Auth.Models;
using Unified.Db.Models;

namespace Unified.Auth.Services;

public sealed class UserService(UnifiedDbContext authDbContext) : IUserService
{
    public async Task<IReadOnlyCollection<UserResponse>> GetAllAsync(
        UserQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    )
    {
        var query = authDbContext.Users.AsNoTracking();

        query = queryParams switch
        {
            { FirstName.Length: > 0, LastName.Length: > 0 } => query.Where(x =>
                x.FirstName.Contains(queryParams.FirstName) || x.LastName.Contains(queryParams.LastName)
            ),
            { FirstName.Length: > 0 } => query.Where(x => x.FirstName.Contains(queryParams.FirstName)),
            { LastName.Length: > 0 } => query.Where(x => x.LastName.Contains(queryParams.LastName)),
            _ => query,
        };

        return await query
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .Select(x => new UserResponse(
                x.Id,
                x.IdirName,
                x.IdirId,
                x.IsEnabled,
                x.FirstName,
                x.LastName,
                x.Email,
                x.HomeLocationId,
                x.LastLogin
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await authDbContext
            .Users.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new UserResponse(
                x.Id,
                x.IdirName,
                x.IdirId,
                x.IsEnabled,
                x.FirstName,
                x.LastName,
                x.Email,
                x.HomeLocationId,
                x.LastLogin
            ))
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
            HomeLocationId = request.HomeLocationId,
            LastLogin = DateTimeOffset.Now,
        };

        authDbContext.Users.Add(userEntity);
        await authDbContext.SaveChangesAsync(cancellationToken);

        return new UserResponse(
            userEntity.Id,
            userEntity.IdirName,
            userEntity.IdirId,
            userEntity.IsEnabled,
            userEntity.FirstName,
            userEntity.LastName,
            userEntity.Email,
            userEntity.HomeLocationId,
            userEntity.LastLogin
        );
    }

    public async Task<UserResponse?> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var userEntity = await authDbContext.Users.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

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

        await authDbContext.SaveChangesAsync(cancellationToken);

        return new UserResponse(
            userEntity.Id,
            userEntity.IdirName,
            userEntity.IdirId,
            userEntity.IsEnabled,
            userEntity.FirstName,
            userEntity.LastName,
            userEntity.Email,
            userEntity.HomeLocationId,
            userEntity.LastLogin
        );
    }
}
