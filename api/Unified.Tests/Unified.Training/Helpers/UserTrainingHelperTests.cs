using System.Text.Json;
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
            AllDay = false,
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

    [Theory]
    [InlineData("", null)]
    [InlineData(",\"allDay\":null", null)]
    [InlineData(",\"allDay\":false", false)]
    [InlineData(",\"allDay\":true", true)]
    public void NormalizeToUtc_JsonClassification_PreservesOmissionAndDefaultsLegacyCreates(
        string classification,
        bool? expected
    )
    {
        var json =
            """
                {"userId":"95f91fd1-1111-2222-3333-9c0aeb4ca44b","trainingId":1,
                "awardedOn":"2026-07-20T23:00:00-07:00","endingOn":"2026-07-21T01:00:00-07:00"
                """
            + classification
            + "}";
        var request = JsonSerializer.Deserialize<UserTrainingRequest>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
        );
        Assert.NotNull(request);
        Assert.Equal(expected, request.AllDay);

        var normalized = UserTrainingHelper.NormalizeToUtc(request);

        Assert.Equal(expected ?? true, normalized.AllDay);
        Assert.Equal(
            expected == false
                ? new DateTimeOffset(2026, 7, 21, 6, 0, 0, TimeSpan.Zero)
                : new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
            normalized.AwardedOn
        );
        Assert.Equal(
            expected == false
                ? new DateTimeOffset(2026, 7, 21, 8, 0, 0, TimeSpan.Zero)
                : new DateTimeOffset(2026, 7, 21, 0, 0, 0, TimeSpan.Zero),
            normalized.EndingOn
        );
    }

    [Theory]
    [InlineData(null, false, false)]
    [InlineData(null, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    public void NormalizeToUtc_UpdateClassification_UsesStoredValueOnlyWhenOmitted(
        bool? allDay,
        bool existingAllDay,
        bool expected
    )
    {
        var request = new UserTrainingRequest
        {
            UserId = Guid.NewGuid(),
            TrainingId = 1,
            AwardedOn = new DateTimeOffset(2026, 7, 20, 9, 0, 0, TimeSpan.FromHours(-7)),
            EndingOn = new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.FromHours(-7)),
            AllDay = allDay,
        };

        var normalized = UserTrainingHelper.NormalizeToUtc(request, existingAllDay);

        Assert.Equal(expected, normalized.AllDay);
        Assert.Equal(expected ? 0 : 16, normalized.AwardedOn.Hour);
        Assert.Equal(expected ? 0 : 17, normalized.EndingOn.Hour);
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

        Assert.Equal(new DateTimeOffset(2025, 12, 31, 23, 59, 59, TimeSpan.Zero), expiry);
    }

    [Fact]
    public void CalculateExpiryDate_WhenValidityDaysProvidedAsMultipleYears_ReturnsEndOfFinalYear()
    {
        var awardedOn = new DateTimeOffset(2025, 10, 1, 0, 0, 0, TimeSpan.Zero);

        var expiry = UserTrainingHelper.CalculateExpiryDate(awardedOn, 730);

        Assert.Equal(new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero), expiry);
    }

    [Fact]
    public void CalculateExpiryDate_WhenAwardedOnIsStartOfDec31_AnnualValidityIsNotImmediateExpiry()
    {
        var awardedOn = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero);

        var expiry = UserTrainingHelper.CalculateExpiryDate(awardedOn, 365);

        Assert.Equal(new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero), expiry);
        Assert.True(expiry > awardedOn);
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
