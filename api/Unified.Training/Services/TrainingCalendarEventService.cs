using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Unified.Common.Time;
using Unified.Db;
using Unified.Training.Helpers;
using Unified.Training.Models.Calendar;

namespace Unified.Training.Services;

public sealed class TrainingCalendarEventService(UnifiedDbContext db)
{
    private static readonly ITimeZoneService TimeZones = new TimeZoneService();

    public async Task<IReadOnlyCollection<TrainingCalendarEvent>> GetEventsAsync(
        TrainingCalendarEventsRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var start = new DateTimeOffset(request.StartDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var exclusiveEnd = new DateTimeOffset(request.EndDate.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var timeZone = string.IsNullOrWhiteSpace(request.TimeZoneId)
            ? TimeZoneInfo.Utc
            : TimeZones.ResolveRequired(request.TimeZoneId);
        var timedRange = TimeZones.ConvertInclusiveLocalDateRangeToUtcRange(
            request.StartDate,
            request.EndDate,
            timeZone
        );
        // All-day events use UTC-stored calendar dates; timed events use the requested local range.
        var coarseStart = start < timedRange.StartAtUtc ? start : timedRange.StartAtUtc;
        var coarseEnd = exclusiveEnd > timedRange.EndAtUtc ? exclusiveEnd : timedRange.EndAtUtc;
        var minimum = DateTimeOffset.MinValue;

        var query = db
            .UserTrainings.AsNoTracking()
            .Where(ut =>
                ut.AwardedOn < coarseEnd
                && (ut.EndingOn == minimum ? ut.AwardedOn >= coarseStart : ut.EndingOn >= coarseStart)
            );

        if (request.UserIds is { Count: > 0 })
            query = query.Where(ut => request.UserIds.Contains(ut.UserId));
        else if (request.LocationId.HasValue)
            query = query.Where(ut => ut.User.HomeLocationId == request.LocationId.Value);

        var records = await query
            .OrderBy(ut => ut.AwardedOn)
            .ThenBy(ut => ut.Id)
            .Select(ut => new
            {
                ut.Id,
                ut.UserId,
                ut.TrainingId,
                ut.AwardedOn,
                ut.EndingOn,
                Code = ut.Training.Code,
                ut.User.HomeLocationId,
            })
            .ToListAsync(cancellationToken);

        return records
            .Select(ut =>
            {
                var allDay = UserTrainingHelper.IsAllDayTraining(ut.AwardedOn, ut.EndingOn);
                var eventStart = ut.AwardedOn;
                var eventEnd = ut.EndingOn;
                if (allDay)
                {
                    var inclusiveEnd = ut.EndingOn == minimum ? ut.AwardedOn : ut.EndingOn;
                    var startDay = DateOnly.FromDateTime(ut.AwardedOn.UtcDateTime);
                    var endDay = DateOnly.FromDateTime(inclusiveEnd.UtcDateTime);
                    eventStart = new DateTimeOffset(startDay.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                    // The day after DateOnly.MaxValue cannot be represented; cap to the requested date range.
                    eventEnd =
                        endDay == DateOnly.MaxValue
                            ? exclusiveEnd
                            : new DateTimeOffset(endDay.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                }
                return new TrainingCalendarEvent
                {
                    Id = $"training.user-training.{ut.Id.ToString(CultureInfo.InvariantCulture)}",
                    UserTrainingId = ut.Id,
                    UserId = ut.UserId,
                    TrainingId = ut.TrainingId,
                    Type = "training.user-training",
                    SourceModule = "training",
                    Title = $"Training: {ut.Code}",
                    Start = eventStart,
                    End = eventEnd,
                    AllDay = allDay,
                    IsReadOnly = true,
                    LocationId = ut.HomeLocationId,
                };
            })
            .Where(item =>
                item.AllDay
                    ? item.Start < exclusiveEnd && item.End > start
                    : item.Start < timedRange.EndAtUtc && item.End > timedRange.StartAtUtc
            )
            .ToList();
    }
}
