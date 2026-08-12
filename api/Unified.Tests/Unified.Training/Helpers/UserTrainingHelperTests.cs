using Unified.Training.Helpers;
using Unified.Training.Models;

namespace Unified.Tests.Training.Helpers;

public class UserTrainingHelperTests
{
    [Fact]
    public void NormalizeToUtc_WhenOffsetsProvided_NormalizesAllDateFields()
    {
        var request = new UserTrainingRequest
        {
            UserId = Guid.NewGuid(),
            TrainingId = 1,
            AwardedOn = new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.FromHours(-7)),
            EndingOn = new DateTimeOffset(2026, 7, 2, 10, 30, 0, TimeSpan.FromHours(-7)),
            ExpiryDate = new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.FromHours(-7)),
            Notes = "note",
        };

        var normalized = UserTrainingHelper.NormalizeToUtc(request);

        Assert.Equal(request.AwardedOn.ToUniversalTime(), normalized.AwardedOn);
        Assert.Equal(request.EndingOn.ToUniversalTime(), normalized.EndingOn);
        Assert.Equal(request.ExpiryDate?.ToUniversalTime(), normalized.ExpiryDate);
        Assert.Equal(TimeSpan.Zero, normalized.AwardedOn.Offset);
        Assert.Equal(TimeSpan.Zero, normalized.EndingOn.Offset);
        Assert.Equal(TimeSpan.Zero, normalized.ExpiryDate!.Value.Offset);
    }

    [Fact]
    public void NormalizeToUtc_WhenExpiryDateIsNull_LeavesExpiryDateNull()
    {
        var request = new UserTrainingRequest
        {
            UserId = Guid.NewGuid(),
            TrainingId = 1,
            AwardedOn = new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.FromHours(-7)),
            EndingOn = new DateTimeOffset(2026, 7, 2, 10, 30, 0, TimeSpan.FromHours(-7)),
            ExpiryDate = null,
        };

        var normalized = UserTrainingHelper.NormalizeToUtc(request);

        Assert.Null(normalized.ExpiryDate);
    }

    [Fact]
    public void CalculateExpiryDate_WhenValidityDaysProvidedAsMonths_ReturnsAwardedOnPlusDays()
    {
        var awardedOn = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

        var expiry = UserTrainingHelper.CalculateExpiryDate(awardedOn, 60);

        Assert.Equal(awardedOn.AddDays(60), expiry);
    }

    [Fact]
    public void CalculateExpiryDate_WhenValidityDaysProvidedAsYears_ReturnsEndOfAwardYear()
    {
        var awardedOn = new DateTimeOffset(2025, 10, 1, 0, 0, 0, TimeSpan.Zero);

        var expiry = UserTrainingHelper.CalculateExpiryDate(awardedOn, 365);

        Assert.Equal(new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero), expiry);
    }

    [Fact]
    public void CalculateExpiryDate_WhenValidityDaysProvidedAsMultipleYears_ReturnsEndOfFinalYear()
    {
        var awardedOn = new DateTimeOffset(2025, 10, 1, 0, 0, 0, TimeSpan.Zero);

        var expiry = UserTrainingHelper.CalculateExpiryDate(awardedOn, 730);

        Assert.Equal(new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero), expiry);
    }

    [Fact]
    public void CalculateExpiryDate_WhenValidityDaysIsNull_ReturnsNull()
    {
        var awardedOn = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

        var expiry = UserTrainingHelper.CalculateExpiryDate(awardedOn, null);

        Assert.Null(expiry);
    }

    [Fact]
    public void CalculateExpiryDate_WhenValidityDaysIsZero_ReturnsAwardedOn()
    {
        var awardedOn = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

        var expiry = UserTrainingHelper.CalculateExpiryDate(awardedOn, 0);

        Assert.Equal(awardedOn, expiry);
    }
}
