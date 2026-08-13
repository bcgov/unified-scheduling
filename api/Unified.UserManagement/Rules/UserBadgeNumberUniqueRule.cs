using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Unified.Common.Interceptors;
using Unified.Db;
using Unified.Db.Models.UserManagement;
using Unified.UserManagement.FeatureFlags;

namespace Unified.UserManagement.Rules;

/// <summary>
/// Validates that badge numbers are required and unique when feature flags are enabled.
/// Runs inside a transaction - any exception causes rollback.
/// </summary>
public sealed class UserBadgeNumberUniqueRule(IOptionsMonitor<UserManagementFeatureFlags> featureFlagsMonitor)
    : ISaveRule
{
    public async Task ExecuteAsync(DbContext context, CancellationToken cancellationToken)
    {
        // Skip validation if feature flag is not enabled
        if (!featureFlagsMonitor.CurrentValue.UserBadgeNumber.Enabled)
            return;

        // Get users being created or modified
        var modifiedUsers = context
            .ChangeTracker.Entries<User>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
            .ToList();

        if (!modifiedUsers.Any())
            return;

        var missingBadgeNumbers = modifiedUsers.Where(e => string.IsNullOrWhiteSpace(e.Entity.BadgeNumber)).ToList();

        if (missingBadgeNumbers.Any())
        {
            var userIds = string.Join(", ", missingBadgeNumbers.Select(e => e.Entity.Id));
            throw new InvalidOperationException($"Badge number is required for user(s): {userIds}.");
        }

        var entriesWithBadges = modifiedUsers
            .Where(e => !string.IsNullOrWhiteSpace(e.Entity.BadgeNumber))
            .Select(e => new { Entry = e, BadgeNumber = e.Entity.BadgeNumber!.Trim() })
            .ToList();

        if (!entriesWithBadges.Any())
            return;

        var duplicatePendingBadgeNumbers = entriesWithBadges
            .GroupBy(x => x.BadgeNumber, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicatePendingBadgeNumbers.Count != 0)
        {
            var duplicateList = string.Join(", ", duplicatePendingBadgeNumbers.Select(b => $"'{b}'"));
            throw new InvalidOperationException(
                $"Duplicate badge number(s) detected in pending changes: {duplicateList}."
            );
        }

        var badgeNumbers = entriesWithBadges
            .Select(x => x.BadgeNumber)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var normalizedBadgeNumbers = badgeNumbers.Select(badge => badge.ToUpperInvariant()).ToList();

        if (!badgeNumbers.Any())
            return;

        var existingBadgeNumbers = await context
            .Set<User>()
            .AsNoTracking()
            .Where(u => u.BadgeNumber != null && normalizedBadgeNumbers.Contains(u.BadgeNumber.ToUpper()))
            .Select(u => new { u.Id, u.BadgeNumber })
            .ToListAsync(cancellationToken);

        var duplicateBadgeNumbersInSystem = entriesWithBadges
            .Where(entry =>
                existingBadgeNumbers.Any(existing =>
                    string.Equals(existing.BadgeNumber, entry.BadgeNumber, StringComparison.OrdinalIgnoreCase)
                    && existing.Id != entry.Entry.Entity.Id
                )
            )
            .Select(entry => entry.BadgeNumber)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(badge => badge, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (duplicateBadgeNumbersInSystem.Count != 0)
        {
            var duplicateList = string.Join(", ", duplicateBadgeNumbersInSystem.Select(b => $"'{b}'"));
            throw new InvalidOperationException(
                $"Badge number(s) {duplicateList} are already in use by other user(s)."
            );
        }
    }
}
