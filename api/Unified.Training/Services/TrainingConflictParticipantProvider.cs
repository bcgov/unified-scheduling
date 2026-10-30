using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Unified.Calendar.Services;
using Unified.Common.Calendar.Conflicts;
using Unified.Common.Time;
using Unified.Db;

namespace Unified.Training.Services;

public sealed class TrainingConflictParticipantProvider(
    UnifiedDbContext db,
    ICalendarTimeZoneResolver timeZoneResolver,
    ITimeZoneService timeZoneService
) : ICalendarConflictParticipantProvider
{
    private const string SourceModule = "training";
    private const string EventIdPrefix = "training.user-training.";
    private static readonly DateTimeOffset MinimumEnd = DateTimeOffset.MinValue;

    public static CalendarConflictEventIdentity IdentityFor(int userTrainingId) =>
        CalendarConflictEventIdentity.Create(
            SourceModule,
            EventIdPrefix + userTrainingId.ToString(CultureInfo.InvariantCulture)
        );

    public async Task<CalendarConflictParticipant?> GetParticipantAsync(
        CalendarConflictEventIdentity identity,
        Guid resourceId,
        CancellationToken cancellationToken = default
    )
    {
        if (
            identity.SourceModule != SourceModule
            || !identity.EventId.StartsWith(EventIdPrefix, StringComparison.Ordinal)
            || !int.TryParse(
                identity.EventId.AsSpan(EventIdPrefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var id
            )
            || id <= 0
            || identity != IdentityFor(id)
        )
            return null;

        var record = await db
            .UserTrainings.AsNoTracking()
            .Where(training => training.Id == id && training.UserId == resourceId)
            .Select(training => new TrainingRecord(
                training.Id,
                training.UserId,
                training.AwardedOn,
                training.EndingOn,
                training.Training.Code,
                training.User.HomeLocation != null ? training.User.HomeLocation.Timezone : null
            ))
            .SingleOrDefaultAsync(cancellationToken);

        return record is null ? null : ToParticipant(record);
    }

    public async Task<IReadOnlyCollection<CalendarConflictParticipant>> GetParticipantsAsync(
        CalendarConflictQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.EndAtUtc <= query.StartAtUtc)
            return [];

        // UTC-stored Training dates can occupy the preceding or following UTC day in a local time zone.
        var coarseStart = query.StartAtUtc.AddDays(-2);
        var coarseEnd = query.EndAtUtc.AddDays(2);
        var records = db
            .UserTrainings.AsNoTracking()
            .Where(training =>
                training.AwardedOn < coarseEnd
                && (
                    training.EndingOn == MinimumEnd
                        ? training.AwardedOn >= coarseStart
                        : training.EndingOn >= coarseStart
                )
            );

        if (query.ResourceIds is { Count: > 0 })
        {
            var resourceIds = query.ResourceIds.Distinct().ToList();
            records = records.Where(training => resourceIds.Contains(training.UserId));
        }

        var projected = await records
            .Select(training => new TrainingRecord(
                training.Id,
                training.UserId,
                training.AwardedOn,
                training.EndingOn,
                training.Training.Code,
                training.User.HomeLocation != null ? training.User.HomeLocation.Timezone : null
            ))
            .ToListAsync(cancellationToken);

        return projected
            .Select(ToParticipant)
            .Where(participant => participant.Start < query.EndAtUtc && participant.End > query.StartAtUtc)
            .ToList();
    }

    private CalendarConflictParticipant ToParticipant(TrainingRecord record)
    {
        var end = record.EndingOn == MinimumEnd ? record.AwardedOn : record.EndingOn;
        var startDay = DateOnly.FromDateTime(record.AwardedOn.UtcDateTime);
        var endDay = DateOnly.FromDateTime(end.UtcDateTime);
        var timeZone = timeZoneResolver.Resolve(null, record.TimeZoneId);
        var range = timeZoneService.ConvertInclusiveLocalDateRangeToUtcRange(startDay, endDay, timeZone);

        return new CalendarConflictParticipant(
            IdentityFor(record.Id).EventId,
            SourceModule,
            record.UserId,
            range.StartAtUtc,
            range.EndAtUtc,
            $"Training: {record.Code}",
            record.Id,
            timeZone.Id,
            EventTypeCode: "training"
        );
    }

    private sealed record TrainingRecord(
        int Id,
        Guid UserId,
        DateTimeOffset AwardedOn,
        DateTimeOffset EndingOn,
        string Code,
        string? TimeZoneId
    );
}
