using FluentValidation;
using FluentValidation.Results;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.Db;
using Unified.Leave.Models.Scheduling;
using Unified.Leave.Validators;

namespace Unified.Leave.Services;

public sealed class LeaveRequestNormalizer(
    UnifiedDbContext db,
    ITimeZoneService timeZoneService,
    SchedulingLeaveRequestValidator entryValidator,
    SchedulingLeaveSeriesRequestValidator seriesValidator,
    IRecurrenceRuleValidator recurrenceValidator
)
{
    public async Task<NormalizedLeaveEntryRequest> NormalizeAsync(
        SchedulingLeaveEntryRequest request,
        CancellationToken cancellationToken
    )
    {
        await entryValidator.ValidateAndThrowAsync(request, cancellationToken);
        var interval = await NormalizeIntervalAsync(request, cancellationToken);
        return request.Adapt<NormalizedLeaveEntryRequest>() with
        {
            StartAtUtc = interval.StartAtUtc!.Value,
            EndAtUtc = interval.EndAtUtc,
            TimeZoneId = interval.TimeZoneId,
        };
    }

    public async Task<NormalizedLeaveSeriesRequest> NormalizeAsync(
        SchedulingLeaveSeriesRequest request,
        CancellationToken cancellationToken
    )
    {
        await seriesValidator.ValidateAndThrowAsync(request, cancellationToken);
        var interval = await NormalizeIntervalAsync(request, cancellationToken);
        var normalized = request.Adapt<NormalizedLeaveSeriesRequest>() with
        {
            StartAtUtc = interval.StartAtUtc!.Value,
            EndAtUtc = interval.EndAtUtc,
            TimeZoneId = interval.TimeZoneId,
        };
        // All-day recurrence can only be validated after resolving the authoritative location zone.
        if (request.AllDay)
        {
            var result = recurrenceValidator.Validate(
                normalized.RecurrenceRule!,
                normalized.StartAtUtc,
                normalized.EndAtUtc,
                normalized.TimeZoneId,
                new RecurrenceValidationOptions
                {
                    MaximumDuration = TimeSpan.FromDays(365),
                    MaximumOccurrences = 400,
                    RequireBoundedRule = true,
                }
            );
            if (!result.IsValid)
                throw new ValidationException(
                    result.Errors.Select(error => new ValidationFailure(nameof(request.RecurrenceRule), error))
                );
        }
        return normalized;
    }

    private async Task<SchedulingLeaveRequest> NormalizeIntervalAsync(
        SchedulingLeaveRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!request.AllDay)
            return request;

        var location = await db
            .Locations.AsNoTracking()
            .Where(location => location.Id == request.LocationId)
            .Select(location => new { location.Timezone })
            .SingleOrDefaultAsync(cancellationToken);
        if (location is null)
            throw Invalid(nameof(request.LocationId), "Select an existing location for all-day leave.");
        if (string.IsNullOrWhiteSpace(location.Timezone) || !TimeZoneService.IsValidTimeZoneId(location.Timezone))
            throw Invalid(nameof(request.LocationId), "The leave location must have a valid operational time zone.");

        var zone = timeZoneService.ResolveRequired(location.Timezone);
        var range = timeZoneService.ConvertInclusiveLocalDateRangeToUtcRange(
            request.LeaveDate!.Value,
            request.LeaveDate.Value,
            zone
        );
        return request with { StartAtUtc = range.StartAtUtc, EndAtUtc = range.EndAtUtc, TimeZoneId = zone.Id };
    }

    private static ValidationException Invalid(string property, string message) =>
        new([new ValidationFailure(property, message)]);
}
