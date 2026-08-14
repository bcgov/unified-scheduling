using Unified.Training.Models;

namespace Unified.Training.Helpers;

public static class UserTrainingHelper
{
    private const int DaysPerYear = 365;
    private static readonly TimeSpan EndOfDayTime = new(23, 59, 59);

    public static UserTrainingRequest NormalizeToUtc(UserTrainingRequest request) =>
        request with
        {
            AwardedOn = request.AwardedOn.ToUniversalTime(),
            EndingOn = request.EndingOn.ToUniversalTime(),
            ExpiryDate = request.ExpiryDate?.ToUniversalTime(),
        };

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
