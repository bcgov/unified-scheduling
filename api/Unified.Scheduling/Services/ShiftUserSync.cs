using Unified.Db;
using Unified.Db.Models.Scheduling;

namespace Unified.Scheduling.Services;

internal static class ShiftUserSync
{
    public static IReadOnlyCollection<Guid> GetDistinctUserIds(IReadOnlyCollection<Guid> userIds)
    {
        return userIds.Distinct().ToList();
    }

    public static bool UserSetsEqual(IEnumerable<Guid> left, IEnumerable<Guid> right) =>
        left.Distinct().Order().SequenceEqual(right.Distinct().Order());

    public static void SyncSeriesUsers(UnifiedDbContext db, ShiftSeries shiftSeries, IReadOnlyCollection<Guid> userIds)
    {
        var requestedUserIds = GetDistinctUserIds(userIds);
        var usersToRemove = shiftSeries.Users.Where(user => !requestedUserIds.Contains(user.UserId)).ToList();
        db.ShiftSeriesUsers.RemoveRange(usersToRemove);
        foreach (var user in usersToRemove)
            shiftSeries.Users.Remove(user);

        var existingUserIds = shiftSeries.Users.Select(user => user.UserId).ToHashSet();
        var usersToAdd = requestedUserIds
            .Where(userId => !existingUserIds.Contains(userId))
            .Select(userId => new ShiftSeriesUser { ShiftSeriesId = shiftSeries.Id, UserId = userId })
            .ToList();
        foreach (var user in usersToAdd)
            shiftSeries.Users.Add(user);
    }

    public static void SyncEntryUsers(UnifiedDbContext db, ShiftEntry shiftEntry, IReadOnlyCollection<Guid> userIds)
    {
        var requestedUserIds = GetDistinctUserIds(userIds);
        var usersToRemove = shiftEntry.Users.Where(user => !requestedUserIds.Contains(user.UserId)).ToList();
        db.ShiftEntryUsers.RemoveRange(usersToRemove);
        foreach (var user in usersToRemove)
            shiftEntry.Users.Remove(user);

        var existingUserIds = shiftEntry.Users.Select(user => user.UserId).ToHashSet();
        var usersToAdd = requestedUserIds
            .Where(userId => !existingUserIds.Contains(userId))
            .Select(userId => new ShiftEntryUser { ShiftEntryId = shiftEntry.Id, UserId = userId })
            .ToList();
        foreach (var user in usersToAdd)
            shiftEntry.Users.Add(user);
    }
}
