using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Training.Models.Calendar;

namespace Unified.Training.Services;

public sealed class TrainingCalendarEventService(UnifiedDbContext db)
{
    public async Task<IReadOnlyCollection<TrainingCalendarEvent>> GetEventsAsync(
        TrainingCalendarEventsRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var start = new DateTimeOffset(request.StartDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var exclusiveEnd = new DateTimeOffset(request.EndDate.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var minimum = DateTimeOffset.MinValue;

        var query = db
            .UserTrainings.AsNoTracking()
            .Where(ut =>
                ut.AwardedOn < exclusiveEnd && (ut.EndingOn == minimum ? ut.AwardedOn >= start : ut.EndingOn >= start)
            );

        if (request.LocationId.HasValue)
            query = query.Where(ut => ut.User.HomeLocationId == request.LocationId.Value);

        if (request.UserIds is { Count: > 0 })
            query = query.Where(ut => request.UserIds.Contains(ut.UserId));

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
                var inclusiveEnd = ut.EndingOn == minimum ? ut.AwardedOn : ut.EndingOn;
                var startDay = DateOnly.FromDateTime(ut.AwardedOn.UtcDateTime);
                var endDay = DateOnly.FromDateTime(inclusiveEnd.UtcDateTime);
                return new TrainingCalendarEvent
                {
                    Id = $"training.user-training.{ut.Id.ToString(CultureInfo.InvariantCulture)}",
                    UserTrainingId = ut.Id,
                    UserId = ut.UserId,
                    TrainingId = ut.TrainingId,
                    Type = "training.user-training",
                    SourceModule = "training",
                    Title = $"Training: {ut.Code}",
                    Start = new DateTimeOffset(startDay.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                    End = new DateTimeOffset(endDay.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                    AllDay = true,
                    IsReadOnly = true,
                    LocationId = ut.HomeLocationId,
                };
            })
            .ToList();
    }
}
