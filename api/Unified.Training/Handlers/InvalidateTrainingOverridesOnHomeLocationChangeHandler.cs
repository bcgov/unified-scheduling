using Microsoft.EntityFrameworkCore;
using Unified.Calendar.Conflicts;
using Unified.Common.Contracts;
using Unified.Common.Events;
using Unified.Db;
using Unified.Training.Services;

namespace Unified.Training.Handlers;

/// <summary>
/// Rechecks training overrides after a persisted home-location change alters the user's training time zone.
/// </summary>
public sealed class InvalidateTrainingOverridesOnHomeLocationChangeHandler(
    UnifiedDbContext db,
    ICalendarConflictService conflictService
) : IEntitySideEffect<UserHomeLocationChangedSignal>
{
    public async Task HandleAsync(UserHomeLocationChangedSignal signal, CancellationToken cancellationToken)
    {
        var trainingIds = await db
            .UserTrainings.AsNoTracking()
            .Where(training => training.UserId == signal.UserId)
            .Select(training => training.Id)
            .ToListAsync(cancellationToken);
        var identities = trainingIds.Select(TrainingConflictParticipantProvider.IdentityFor).ToArray();
        await conflictService.InvalidateResolvedOverridesAsync(identities, cancellationToken: cancellationToken);
    }
}
