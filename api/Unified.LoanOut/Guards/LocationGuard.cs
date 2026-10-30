using Microsoft.EntityFrameworkCore;
using Unified.Db;

namespace Unified.LoanOut.Guards;

public static class LocationGuard
{
    /// <summary>Checks the optional source and required destination before a loan-out is saved.</summary>
    public static async Task EnsureValidAsync(
        UnifiedDbContext db,
        int? locationId,
        int loanOutLocationId,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(db);
        cancellationToken.ThrowIfCancellationRequested();
        if (locationId.HasValue && locationId.Value <= 0)
            throw new InvalidOperationException("A valid home location is required.");
        if (loanOutLocationId <= 0)
            throw new InvalidOperationException("A valid loan-out location is required.");

        var requestedIds = new[] { locationId, loanOutLocationId }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var existingIds = await db
            .Locations.AsNoTracking()
            .Where(location => requestedIds.Contains(location.Id))
            .Select(location => location.Id)
            .ToListAsync(cancellationToken);
        if (locationId.HasValue && !existingIds.Contains(locationId.Value))
            throw new InvalidOperationException($"Home location {locationId.Value} was not found.");
        if (!existingIds.Contains(loanOutLocationId))
            throw new InvalidOperationException($"Loan-out location {loanOutLocationId} was not found.");
    }
}
