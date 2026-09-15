using Microsoft.EntityFrameworkCore;
using Unified.Common.PostSave;
using Unified.Db.Models.Training;
using Unified.Db.Models.UserManagement;
using Unified.Training.Helpers;

namespace Unified.Training.Handlers;

/// <summary>
/// Stages mandatory training after the user has been saved successfully, before the transaction commits.
/// </summary>
public sealed class AssignMandatoryTrainingOnUserCreationHandler : IPostSaveHandler
{
    public Type EntityType => typeof(User);

    public SaveAction Action => SaveAction.Create;

    public async Task HandleAsync(DbContext db, SaveContext context, CancellationToken cancellationToken)
    {
        if (context.Action != Action || context.Entity is not User user)
            return;

        var userId = user.Id;
        var now = context.OccurredAtUtc.ToUniversalTime();
        var mandatoryTrainings = await db.Set<Unified.Db.Models.Training.Training>()
            .AsNoTracking()
            .Where(training =>
                training.Mandatory
                && training.EffectiveDate <= now
                && (training.ExpiryDate == null || training.ExpiryDate > now)
            )
            .Select(training => new { training.Id, training.ValidityDays })
            .ToListAsync(cancellationToken);

        if (mandatoryTrainings.Count == 0)
            return;

        // Preserve supplied records and make repeated invocation safe before or after a save.
        var assignedTrainingIds = db
            .ChangeTracker.Entries<UserTraining>()
            .Where(entry => entry.State != EntityState.Deleted && entry.Entity.UserId == userId)
            .Select(entry => entry.Entity.TrainingId)
            .ToHashSet();
        var persistedTrainingIds = await db.Set<UserTraining>()
            .AsNoTracking()
            .Where(training => training.UserId == userId)
            .Select(training => training.TrainingId)
            .ToListAsync(cancellationToken);
        assignedTrainingIds.UnionWith(persistedTrainingIds);

        foreach (var training in mandatoryTrainings)
        {
            if (!assignedTrainingIds.Add(training.Id))
                continue;

            db.Set<UserTraining>()
                .Add(
                    new UserTraining
                    {
                        UserId = userId,
                        TrainingId = training.Id,
                        Version = 1,
                        AwardedOn = now,
                        EndingOn = now,
                        ExpiryDate = UserTrainingHelper.CalculateExpiryDate(now, training.ValidityDays),
                        NoticeState = UserTrainingNoticeStates.None,
                        Notes = null,
                    }
                );
        }
    }
}
