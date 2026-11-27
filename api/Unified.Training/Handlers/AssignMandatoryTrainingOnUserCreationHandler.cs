using Microsoft.EntityFrameworkCore;
using Unified.Common.Contracts;
using Unified.Common.Events;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models.Training;
using Unified.Training.Helpers;
using Unified.Training.Services;

namespace Unified.Training.Handlers;

/// <summary>
/// Stages mandatory training assignments when a user-creation signal is published.
/// </summary>
public sealed class AssignMandatoryTrainingOnUserCreationHandler(UnifiedDbContext DB, ITimeZoneService timeZoneService)
    : IEntitySideEffect<UserCreatedSignal>
{
    public async Task HandleAsync(UserCreatedSignal signal, CancellationToken cancellationToken)
    {
        var userId = signal.UserId;
        var now = signal.OccurredAtUtc.ToUniversalTime();
        var userDetails = await DB
            .Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new
            {
                user.TrainingProfileId,
                TimeZoneId = user.HomeLocation != null ? user.HomeLocation.Timezone : null,
            })
            .SingleOrDefaultAsync(cancellationToken);
        var userTrainingProfileId = userDetails?.TrainingProfileId;
        var timeZone = timeZoneService.ResolveOrUtc(userDetails?.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
        var allDayDate = new DateTimeOffset(localNow.Date, TimeSpan.Zero);

        var mandatoryTrainings = await DB
            .Trainings.AsNoTracking()
            .Where(training =>
                training.Mandatory
                && training.EffectiveDate <= now
                && (training.ExpiryDate == null || training.ExpiryDate > now)
            )
            .WhereApplicableToTrainingProfile(userTrainingProfileId)
            .Select(training => new
            {
                training.Id,
                training.ValidityDays,
                training.AnnualValidityFromCompletionDate,
            })
            .ToListAsync(cancellationToken);

        if (mandatoryTrainings.Count == 0)
            return;

        // Preserve supplied records and make repeated invocation safe before or after a save.
        var assignedTrainingIds = DB
            .ChangeTracker.Entries<UserTraining>()
            .Where(entry => entry.State != EntityState.Deleted && entry.Entity.UserId == userId)
            .Select(entry => entry.Entity.TrainingId)
            .ToHashSet();
        var persistedTrainingIds = await DB
            .UserTrainings.AsNoTracking()
            .Where(training => training.UserId == userId)
            .Select(training => training.TrainingId)
            .ToListAsync(cancellationToken);
        assignedTrainingIds.UnionWith(persistedTrainingIds);

        foreach (var training in mandatoryTrainings)
        {
            if (!assignedTrainingIds.Add(training.Id))
                continue;

            DB.UserTrainings.Add(
                new UserTraining
                {
                    UserId = userId,
                    TrainingId = training.Id,
                    Version = 1,
                    AwardedOn = allDayDate,
                    EndingOn = allDayDate,
                    AllDay = true,
                    ExpiryDate = UserTrainingHelper.CalculateExpiryDate(
                        localNow,
                        training.ValidityDays,
                        allDay: true,
                        timeZone,
                        timeZoneService,
                        training.AnnualValidityFromCompletionDate
                    ),
                    NoticeState = UserTrainingNoticeStates.None,
                    Notes = null,
                }
            );
        }
    }
}
