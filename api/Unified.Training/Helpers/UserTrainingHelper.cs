using Unified.Common.Time;
using Unified.Training.Models;

namespace Unified.Training.Helpers;

public static class UserTrainingHelper
{
    private const int DaysPerYear = 365;
    private static readonly TimeSpan EndOfDayTime = new(23, 59, 59);

    public static UserTrainingRequest NormalizeToUtc(UserTrainingRequest request) =>
        request with
        {
            AwardedOn = request.AllDay
                ? new DateTimeOffset(request.AwardedOn.Date, TimeSpan.Zero)
                : request.AwardedOn.ToUniversalTime(),
            EndingOn = request.AllDay
                ? new DateTimeOffset(request.EndingOn.Date, TimeSpan.Zero)
                : request.EndingOn.ToUniversalTime(),
            ExpiryDate = request.ExpiryDate?.ToUniversalTime(),
        };

    public static DateTimeOffset GetLocalDayStartUtc(
        DateOnly date,
        TimeZoneInfo timeZone,
        ITimeZoneService timeZoneService
    )
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        if (timeZone.IsInvalidTime(local))
        {
            // Locate the first valid wall-clock instant after a midnight gap, including a skipped day.
            var invalidTicks = local.Ticks;
            var validTicks = local.AddDays(1).Ticks;
            while (validTicks - invalidTicks > 1)
            {
                var ticks = invalidTicks + (validTicks - invalidTicks) / 2;
                if (timeZone.IsInvalidTime(new DateTime(ticks, DateTimeKind.Unspecified)))
                    invalidTicks = ticks;
                else
                    validTicks = ticks;
            }
            local = new DateTime(validTicks, DateTimeKind.Unspecified);
        }
        // The larger offset is the earlier occurrence of an ambiguous local midnight.
        return timeZone.IsAmbiguousTime(local)
            ? new DateTimeOffset(local, timeZone.GetAmbiguousTimeOffsets(local).Max()).ToUniversalTime()
            : timeZoneService.ToUtcInstant(local, timeZone);
    }

    /// <summary>
    /// Auto-calculates the expiry date from the training type's <c>ValidityDays</c>.
    /// Returns <c>null</c> when the training has no validity period.
    /// </summary>
    public static DateTimeOffset? CalculateExpiryDate(DateTimeOffset awardedOn, int? validityDays)
    {
        if (!validityDays.HasValue)
            return null;

        if (IsAnnualValidity(validityDays.Value))
            return CalculateEndOfYearExpiryDate(awardedOn, validityDays.Value);

        return awardedOn.AddDays(validityDays.Value);
    }

    private static bool IsAnnualValidity(int validityDays) =>
        validityDays >= DaysPerYear && validityDays % DaysPerYear == 0;

    private static DateTimeOffset CalculateEndOfYearExpiryDate(DateTimeOffset awardedOn, int validityDays)
    {
        var yearCount = validityDays / DaysPerYear;
        var expiryYear = awardedOn.Year + yearCount - 1;
        return new DateTimeOffset(expiryYear, 12, 31, 0, 0, 0, awardedOn.Offset).Add(EndOfDayTime);
    }
}
