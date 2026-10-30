using Microsoft.EntityFrameworkCore;
using Unified.Db;

namespace Unified.LoanOut.Guards;

public static class UserGuard
{
    /// <summary>Requires distinct, nonempty user IDs that all exist before a loan-out is saved.</summary>
    public static async Task EnsureValidAsync(
        UnifiedDbContext db,
        IReadOnlyCollection<Guid>? userIds,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(db);
        cancellationToken.ThrowIfCancellationRequested();
        if (userIds is null || userIds.Count == 0)
            throw InvalidUsers("At least one user is required.");
        if (userIds.Contains(Guid.Empty))
            throw InvalidUsers("Selected users must have valid user IDs.");

        var distinctIds = userIds.Distinct().ToList();
        if (distinctIds.Count != userIds.Count)
            throw InvalidUsers("Selected users must be unique.");

        var existingCount = await db
            .Users.AsNoTracking()
            .CountAsync(user => distinctIds.Contains(user.Id), cancellationToken);
        if (existingCount != distinctIds.Count)
            throw InvalidUsers("One or more selected users were not found.");
    }

    private static InvalidOperationException InvalidUsers(string message) => new(message);
}
