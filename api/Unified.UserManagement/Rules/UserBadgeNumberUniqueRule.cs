using Microsoft.EntityFrameworkCore;
using Unified.Common.Interceptors;
using Unified.Db;
using Unified.Db.Models.UserManagement;
using Microsoft.Extensions.Options;
using Unified.UserManagement.FeatureFlags;

namespace Unified.UserManagement.Rules;

/// <summary>
/// Validates that badge numbers are required and unique when feature flags are enabled.
/// Runs inside a transaction - any exception causes rollback.
/// </summary>
public sealed class UserBadgeNumberUniqueRule(
    IDbContextFactory<UnifiedDbContext> contextFactory,
    IOptionsMonitor<UserManagementFeatureFlags> featureFlagsMonitor
) : ISaveRule
{
    public async Task ExecuteAsync(DbContext context, CancellationToken cancellationToken)
    {
        // Skip validation if feature flag is not enabled
        if (!featureFlagsMonitor.CurrentValue.UserBadgeNumber.Enabled)
            return;

        // Get users being created or modified
        var modifiedUsers = context.ChangeTracker.Entries<User>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
            .ToList();

        if (!modifiedUsers.Any())
            return;

        var flags = featureFlagsMonitor.CurrentValue.UserBadgeNumber;

        // Validate required when flag is enabled
        if (flags.Required)
        {
            var missingBadgeNumbers = modifiedUsers
                .Where(e => string.IsNullOrWhiteSpace(e.Entity.BadgeNumber))
                .ToList();

            if (missingBadgeNumbers.Any())
            {
                var userIds = string.Join(", ", missingBadgeNumbers.Select(e => e.Entity.Id));
                throw new InvalidOperationException(
                    $"Badge number is required for user(s): {userIds}."
                );
            }
        }

        // Get users with badge numbers for uniqueness check
        var entriesWithBadges = modifiedUsers
            .Where(e => e.Entity.BadgeNumber != null)
            .ToList();

        if (!entriesWithBadges.Any())
            return;

        var badgeNumbers = entriesWithBadges
            .Select(e => e.Entity.BadgeNumber)
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .ToList();

        if (!badgeNumbers.Any())
            return;

        // Create separate DbContext for safe queries
        using (var queryContext = contextFactory.CreateDbContext())
        {
            // Check for duplicates in the database
            var existingBadgeNumbers = await queryContext.Users
                .Where(u => badgeNumbers.Contains(u.BadgeNumber))
                .Select(u => new { u.Id, u.BadgeNumber })
                .ToListAsync(cancellationToken);

            // For new users (Added state), check for any existing badge numbers
            var newUsers = entriesWithBadges.Where(e => e.State == EntityState.Added).ToList();
            if (newUsers.Any())
            {
                var newBadgeNumbers = newUsers
                    .Select(e => e.Entity.BadgeNumber)
                    .Where(b => !string.IsNullOrWhiteSpace(b))
                    .ToList();

                var duplicates = existingBadgeNumbers
                    .Where(e => newBadgeNumbers.Contains(e.BadgeNumber))
                    .ToList();

                if (duplicates.Any())
                {
                    var duplicateList = string.Join(", ", duplicates.Select(d => $"'{d.BadgeNumber}'"));
                    throw new InvalidOperationException(
                        $"Badge number(s) {duplicateList} already exist in the system."
                    );
                }
            }

            // For modified users, check for duplicates with other users (excluding self)
            var modifiedOnlyUsers = entriesWithBadges.Where(e => e.State == EntityState.Modified).ToList();
            if (modifiedOnlyUsers.Any())
            {
                foreach (var entry in modifiedOnlyUsers)
                {
                    var badgeNumber = entry.Entity.BadgeNumber;
                    if (string.IsNullOrWhiteSpace(badgeNumber))
                        continue;

                    var isDuplicate = existingBadgeNumbers
                        .Any(e => e.BadgeNumber == badgeNumber && e.Id != entry.Entity.Id);

                    if (isDuplicate)
                    {
                        throw new InvalidOperationException(
                            $"Badge number '{badgeNumber}' is already in use by another user."
                        );
                    }
                }
            }
        } // queryContext disposed automatically
    }
}
