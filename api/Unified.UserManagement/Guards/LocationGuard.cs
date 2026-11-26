using Microsoft.EntityFrameworkCore;
using Unified.Db;

namespace Unified.UserManagement.Guards;

public static class LocationGuard
{
    /// <summary>Ensures every requested location id is positive and exists.</summary>
    public static async Task EnsureValidAsync(
        UnifiedDbContext db,
        IEnumerable<int>? locationIds,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(db);
        cancellationToken.ThrowIfCancellationRequested();

        var requestedIds = locationIds?.Distinct().ToList() ?? [];
        if (requestedIds.Count == 0)
            return;

        if (requestedIds.Any(id => id <= 0))
            throw new InvalidOperationException("A valid location is required.");

        var existingIds = await db
            .Locations.AsNoTracking()
            .Where(location => requestedIds.Contains(location.Id))
            .Select(location => location.Id)
            .ToListAsync(cancellationToken);

        var missingId = requestedIds.FirstOrDefault(id => !existingIds.Contains(id));
        if (missingId != 0)
            throw new InvalidOperationException($"Location {missingId} was not found.");
    }
}
