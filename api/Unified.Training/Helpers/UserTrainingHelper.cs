using Unified.Training.Models;

namespace Unified.Training.Helpers;

public static class UserTrainingHelper
{
    public static UserTrainingRequest NormalizeToUtc(UserTrainingRequest request) =>
        request with
        {
            AwardedOn = request.AwardedOn.ToUniversalTime(),
            EndingOn = request.EndingOn.ToUniversalTime(),
            ExpiryDate = request.ExpiryDate?.ToUniversalTime(),
        };

    /// <summary>
    /// Auto-calculates the expiry date from the training type's <c>ValidityDays</c>.
    /// Returns <paramref name="awardedOn"/> when the training has no validity period.
    /// </summary>
    public static DateTimeOffset? CalculateExpiryDate(DateTimeOffset awardedOn, int? validityDays) =>
        validityDays.HasValue ? awardedOn.AddDays(validityDays.Value) : awardedOn;
}
