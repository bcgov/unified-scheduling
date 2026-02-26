using Microsoft.EntityFrameworkCore;
using Unified.Auth.Data;
using Unified.Auth.Data.Entities;
using Unified.Auth.Models;

namespace Unified.Auth.Services;

public sealed class UserService(AuthDbContext authDbContext) : IUserService
{
    public async Task<IReadOnlyCollection<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await authDbContext
            .Users.AsNoTracking()
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .Select(x => new User(
                x.Id,
                x.IdirName,
                x.IdirId,
                x.KeyCloakId,
                x.IsEnabled,
                x.FirstName,
                x.LastName,
                x.Email,
                x.HomeLocationId,
                x.LastLogin
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await authDbContext
            .Users.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new User(
                x.Id,
                x.IdirName,
                x.IdirId,
                x.KeyCloakId,
                x.IsEnabled,
                x.FirstName,
                x.LastName,
                x.Email,
                x.HomeLocationId,
                x.LastLogin
            ))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<User> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var userEntity = new UserEntity
        {
            Id = Guid.NewGuid(),
            IdirName = request.IdirName.Trim(),
            IdirId = request.IdirId,
            KeyCloakId = request.KeyCloakId,
            IsEnabled = request.IsEnabled,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email.Trim(),
            HomeLocationId = request.HomeLocationId,
            LastLogin = request.LastLogin,
        };

        authDbContext.Users.Add(userEntity);
        await authDbContext.SaveChangesAsync(cancellationToken);

        return new User(
            userEntity.Id,
            userEntity.IdirName,
            userEntity.IdirId,
            userEntity.KeyCloakId,
            userEntity.IsEnabled,
            userEntity.FirstName,
            userEntity.LastName,
            userEntity.Email,
            userEntity.HomeLocationId,
            userEntity.LastLogin
        );
    }

    public async Task<User?> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var userEntity = await authDbContext.Users.SingleOrDefaultAsync(
            x => x.Id == id,
            cancellationToken
        );

        if (userEntity is null)
        {
            return null;
        }

        userEntity.IdirName = request.IdirName.Trim();
        userEntity.IdirId = request.IdirId;
        userEntity.KeyCloakId = request.KeyCloakId;
        userEntity.IsEnabled = request.IsEnabled;
        userEntity.FirstName = request.FirstName.Trim();
        userEntity.LastName = request.LastName.Trim();
        userEntity.Email = request.Email.Trim();
        userEntity.HomeLocationId = request.HomeLocationId;
        userEntity.LastLogin = request.LastLogin;

        await authDbContext.SaveChangesAsync(cancellationToken);

        return new User(
            userEntity.Id,
            userEntity.IdirName,
            userEntity.IdirId,
            userEntity.KeyCloakId,
            userEntity.IsEnabled,
            userEntity.FirstName,
            userEntity.LastName,
            userEntity.Email,
            userEntity.HomeLocationId,
            userEntity.LastLogin
        );
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userEntity = await authDbContext.Users.SingleOrDefaultAsync(
            x => x.Id == id,
            cancellationToken
        );

        if (userEntity is null)
        {
            return false;
        }

        authDbContext.Users.Remove(userEntity);
        await authDbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
