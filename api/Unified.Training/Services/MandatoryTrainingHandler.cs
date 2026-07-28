using Microsoft.EntityFrameworkCore;
using Unified.Common.PostSave;
using Unified.Db;
using Unified.Db.Models.Training;
using Unified.Training.Helpers;
using TrainingEntity = Unified.Db.Models.Training.Training;

namespace Unified.Training.Services;

/// <summary>
/// On create: provisions a <c>UserTraining</c> for every enabled user when the new training is mandatory.
/// On update: provisions a <c>UserTraining</c> for every enabled user only when the training
/// has transitioned from non-mandatory to mandatory.
/// Users who already have an active record are skipped in both cases.
/// </summary>
public sealed class MandatoryTrainingHandler(UnifiedDbContext db)
    : IEntityPostCreateHandler<TrainingEntity>,
        IEntityPostUpdateHandler<TrainingEntity>
{
    /// <inheritdoc/>
    public async Task HandleAsync(TrainingEntity entity, CancellationToken cancellationToken = default)
    {
        if (!entity.Mandatory)
            return;

        await ProvisionAllUsersAsync(entity, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task HandleAsync(
        TrainingEntity entity,
        TrainingEntity previous,
        CancellationToken cancellationToken = default
    )
    {
        // Only act when the training has just become mandatory.
        if (previous.Mandatory || !entity.Mandatory)
            return;

        await ProvisionAllUsersAsync(entity, cancellationToken);
    }

    private async Task ProvisionAllUsersAsync(TrainingEntity entity, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var usersWithExistingActive = await db
            .UserTrainings.Where(ut => ut.TrainingId == entity.Id && (ut.ExpiryDate == null || ut.ExpiryDate > now))
            .Select(ut => ut.UserId)
            .ToListAsync(cancellationToken);

        var userIdsToProvision = await db
            .Users.Where(u => u.IsEnabled && !usersWithExistingActive.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        if (userIdsToProvision.Count == 0)
            return;

        var expiryDate = UserTrainingHelper.CalculateExpiryDate(now, entity.ValidityDays);

        var latestVersionsByUser = await db
            .UserTrainings.Where(ut => ut.TrainingId == entity.Id && userIdsToProvision.Contains(ut.UserId))
            .GroupBy(ut => ut.UserId)
            .Select(g => new { UserId = g.Key, MaxVersion = g.Max(ut => ut.Version) })
            .ToDictionaryAsync(x => x.UserId, x => x.MaxVersion, cancellationToken);

        var newRecords = userIdsToProvision.Select(userId => new UserTraining
        {
            UserId = userId,
            TrainingId = entity.Id,
            Version = latestVersionsByUser.TryGetValue(userId, out var maxVersion) ? maxVersion + 1 : 1,
            AwardedOn = now,
            EndingOn = now.AddDays(1),
            ExpiryDate = expiryDate,
            NoticeState = UserTrainingNoticeStates.None,
        });

        db.UserTrainings.AddRange(newRecords);
        await db.SaveChangesAsync(cancellationToken);
    }
}
