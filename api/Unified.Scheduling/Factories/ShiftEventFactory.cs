using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Options;
using Unified.Calendar.Factories;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;
using Unified.Scheduling.Options;
using Unified.Scheduling.Services;
using Unified.Scheduling.Validators;

namespace Unified.Scheduling.Factories;

public sealed class ShiftEventFactory(IOptions<WorkingHoursOptions>? workingHoursOptions = null)
    : ICalendarEventEntryFactory<ShiftEntryRequest, ShiftEntry>,
        ICalendarEventSeriesFactory<ShiftSeriesRequest, ShiftSeries>
{
    private readonly WorkingHoursOptions _workingHoursOptions = workingHoursOptions?.Value ?? new();

    public ShiftEntry CreateEntry(ShiftEntryRequest request, Event eventEntity)
    {
        var lunchAvailableMinutes = request.PreserveLunchValues
            ? request.LunchAvailableMinutes
            : request.LunchAvailableMinutes ?? _workingHoursOptions.DefaultLunchMinutes;
        var workedLunchMinutes = request.PreserveLunchValues
            ? request.WorkedLunchMinutes
            : request.WorkedLunchMinutes ?? 0;
        ValidateLunchMinutes(lunchAvailableMinutes, workedLunchMinutes, allowUnknownLunch: request.PreserveLunchValues);
        return new()
        {
            Event = eventEntity,
            ShiftSeriesId = request.ShiftSeriesId,
            LunchAvailableMinutes = lunchAvailableMinutes,
            WorkedLunchMinutes = workedLunchMinutes,
            Users = request.UserIds.Distinct().Select(userId => new ShiftEntryUser { UserId = userId }).ToList(),
        };
    }

    public Event GetEvent(ShiftEntry entity) =>
        entity.Event ?? throw new InvalidOperationException($"Shift entry {entity.Id} has no associated event.");

    public ShiftEntry UpdateEntry(ShiftEntryRequest request, ShiftEntry existing)
    {
        var lunchAvailableMinutes = request.LunchAvailableMinutes ?? existing.LunchAvailableMinutes;
        var workedLunchMinutes = request.WorkedLunchMinutes ?? existing.WorkedLunchMinutes;
        ValidateLunchMinutes(lunchAvailableMinutes, workedLunchMinutes);
        existing.ShiftSeriesId = request.ShiftSeriesId;
        existing.LunchAvailableMinutes = lunchAvailableMinutes;
        existing.WorkedLunchMinutes = workedLunchMinutes;
        SyncUsers(existing, request.UserIds);
        return existing;
    }

    private static void SyncUsers(ShiftEntry entity, IReadOnlyCollection<Guid> requestedUserIds)
    {
        var userIds = requestedUserIds.Distinct().ToHashSet();
        entity.Users = entity.Users.Where(user => userIds.Contains(user.UserId)).ToList();
        var existingUserIds = entity.Users.Select(user => user.UserId).ToHashSet();
        foreach (var userId in userIds.Where(userId => !existingUserIds.Contains(userId)))
            entity.Users.Add(new ShiftEntryUser { UserId = userId });
    }

    public ShiftSeries CreateSeries(ShiftSeriesRequest request, EventSeries eventSeries)
    {
        var lunchAvailableMinutes = request.PreserveLunchValues
            ? request.LunchAvailableMinutes
            : request.LunchAvailableMinutes ?? _workingHoursOptions.DefaultLunchMinutes;
        var workedLunchMinutes = request.PreserveLunchValues
            ? request.WorkedLunchMinutes
            : request.WorkedLunchMinutes ?? 0;
        ValidateLunchMinutes(lunchAvailableMinutes, workedLunchMinutes, allowUnknownLunch: request.PreserveLunchValues);
        return new()
        {
            EventSeries = eventSeries,
            LunchAvailableMinutes = lunchAvailableMinutes,
            WorkedLunchMinutes = workedLunchMinutes,
            Users = request.UserIds.Distinct().Select(userId => new ShiftSeriesUser { UserId = userId }).ToList(),
        };
    }

    public EventSeries GetEventSeries(ShiftSeries entity) =>
        entity.EventSeries
        ?? throw new InvalidOperationException($"Shift series {entity.Id} has no associated event series.");

    // Series relationship synchronization belongs to the feature service, which owns EF tracking.
    public ShiftSeries UpdateSeries(ShiftSeriesRequest request, ShiftSeries existing)
    {
        var lunchAvailableMinutes = request.LunchAvailableMinutes ?? existing.LunchAvailableMinutes;
        var workedLunchMinutes = request.WorkedLunchMinutes ?? existing.WorkedLunchMinutes;
        ValidateLunchMinutes(lunchAvailableMinutes, workedLunchMinutes);
        existing.LunchAvailableMinutes = lunchAvailableMinutes;
        existing.WorkedLunchMinutes = workedLunchMinutes;
        return existing;
    }

    internal static void ValidateLunchMinutes(
        int? lunchAvailableMinutes,
        int? workedLunchMinutes,
        bool allowUnknownLunch = false
    )
    {
        var result = new ShiftEntryRequestValidator().Validate(
            new ShiftEntryRequest
            {
                LunchAvailableMinutes = lunchAvailableMinutes,
                WorkedLunchMinutes = workedLunchMinutes,
            },
            validation =>
                validation.IncludeProperties(
                    request => request.LunchAvailableMinutes,
                    request => request.WorkedLunchMinutes
                )
        );
        if (!result.IsValid)
            throw new ValidationException(result.Errors);
        if (!allowUnknownLunch && !lunchAvailableMinutes.HasValue && workedLunchMinutes.HasValue)
            throw new ValidationException([
                new ValidationFailure(
                    nameof(ShiftEntryRequest.LunchAvailableMinutes),
                    "Lunch allowance is required when worked lunch is specified."
                )
                {
                    ErrorCode = "NotNullValidator",
                },
            ]);
    }
}
