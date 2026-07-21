using Microsoft.EntityFrameworkCore;
using Unified.Db.Models.UserManagement;

namespace Unified.UserManagement.Queries;

internal static class UserIdirNameLookup
{
    public static string? Normalize(string? idirName)
    {
        return User.NormalizeIdirName(idirName);
    }

    public static Task<bool> GetUsersByIdirName(
        this IQueryable<User> users,
        string normalizedIdirName,
        Guid? excludingUserId,
        CancellationToken cancellationToken
    )
    {
        return users
            .Where(user => user.IdirName == normalizedIdirName)
            .Where(user => !excludingUserId.HasValue || user.Id != excludingUserId.Value)
            .AnyAsync(cancellationToken);
    }

    public static IQueryable<User> GetPendingUsersByIdirName(this IQueryable<User> users, string normalizedIdirName)
    {
        return users.Where(user =>
            user.IsEnabled
            && user.PendingRegistration
            && user.IdirId == null
            && user.KeyCloakId == null
            && user.IdirName == normalizedIdirName
        );
    }
}
