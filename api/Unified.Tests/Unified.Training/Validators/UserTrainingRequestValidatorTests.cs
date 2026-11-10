using FluentValidation;
using Unified.Training.Models;
using Unified.Training.Validators;

namespace Unified.Tests.Training.Validators;

public class UserTrainingRequestValidatorTests
{
    private readonly UserTrainingRequestValidator _validator = new();

    private static UserTrainingRequest ValidRequest() =>
        new()
        {
            UserId = Guid.NewGuid(),
            TrainingId = 1,
            AwardedOn = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            EndingOn = new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero),
        };

    [Fact]
    public async Task Validate_WhenRequestIsValid_PassesValidation()
    {
        var result = await _validator.ValidateAsync(ValidRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(15, 18, false, true)]
    [InlineData(18, 15, false, false)]
    [InlineData(15, 15, false, false)]
    [InlineData(0, 0, true, true)]
    public async Task Validate_WhenTrainingIsOnSameDay_ValidatesTimeRange(
        int startHour,
        int endHour,
        bool allDay,
        bool expectedValid
    )
    {
        var request = ValidRequest() with
        {
            AwardedOn = new DateTimeOffset(2026, 7, 20, startHour, 0, 0, TimeSpan.FromHours(-7)),
            EndingOn = new DateTimeOffset(2026, 7, 20, endHour, 0, 0, TimeSpan.FromHours(-7)),
            AllDay = allDay,
        };

        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public async Task Validate_WhenUserIdIsEmpty_FailsValidation()
    {
        var request = ValidRequest() with { UserId = Guid.Empty };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _validator.ValidateAndThrowAsync(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Validate_WhenTrainingIdIsZero_FailsValidation()
    {
        var request = ValidRequest() with { TrainingId = 0 };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _validator.ValidateAndThrowAsync(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Validate_WhenTrainingIdIsNegative_FailsValidation()
    {
        var request = ValidRequest() with { TrainingId = -1 };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _validator.ValidateAndThrowAsync(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Validate_WhenEndingOnIsDefault_FailsValidation()
    {
        var request = ValidRequest() with { EndingOn = default };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _validator.ValidateAndThrowAsync(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Validate_WhenEndingOnIsBeforeAwardedOn_FailsValidation()
    {
        var awardedOn = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var request = ValidRequest() with { AwardedOn = awardedOn, EndingOn = awardedOn.AddDays(-1) };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _validator.ValidateAndThrowAsync(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Validate_WhenExpiryDateIsAfterEndingOn_PassesValidation()
    {
        var awardedOn = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var endingOn = awardedOn.AddDays(2);
        var request = ValidRequest() with
        {
            AwardedOn = awardedOn,
            EndingOn = endingOn,
            ExpiryDate = endingOn.AddDays(365),
        };

        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_WhenExpiryDateIsAfterAwardedOnButBeforeEndingOn_FailsValidation()
    {
        var awardedOn = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var endingOn = awardedOn.AddDays(2);
        var request = ValidRequest() with
        {
            AwardedOn = awardedOn,
            EndingOn = endingOn,
            ExpiryDate = awardedOn.AddDays(1),
        };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _validator.ValidateAndThrowAsync(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Validate_WhenExpiryDateEqualsEndingOn_FailsValidation()
    {
        var endingOn = new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero);
        var request = ValidRequest() with { EndingOn = endingOn, ExpiryDate = endingOn };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _validator.ValidateAndThrowAsync(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Validate_AllDayExpiryBeforeNormalizedEnd_ReturnsExistingValidationError()
    {
        var request = ValidRequest() with
        {
            AllDay = true,
            AwardedOn = new DateTimeOffset(2026, 7, 19, 0, 0, 0, TimeSpan.FromHours(14)),
            EndingOn = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.FromHours(14)),
            ExpiryDate = new DateTimeOffset(2026, 7, 19, 18, 0, 0, TimeSpan.Zero),
        };

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _validator.ValidateAndThrowAsync(request, TestContext.Current.CancellationToken)
        );
        var error = Assert.Single(exception.Errors);
        Assert.Equal(nameof(UserTrainingRequest.ExpiryDate), error.PropertyName);
        Assert.Equal("Expiry date must be after the ending date.", error.ErrorMessage);
    }

    [Theory]
    [InlineData(true, 14, "2026-07-20T00:00:00Z", false)]
    [InlineData(true, 14, "2026-07-20T01:00:00Z", true)]
    [InlineData(true, -7, "2026-07-20T01:00:00Z", true)]
    [InlineData(false, 14, "2026-07-19T18:00:00Z", true)]
    [InlineData(false, -7, "2026-07-20T01:00:00Z", false)]
    [InlineData(false, -7, "2026-07-20T08:00:00Z", true)]
    public async Task Validate_ExpiryWithOffsets_UsesEffectiveEnd(
        bool allDay,
        int offsetHours,
        string expiryDate,
        bool expected
    )
    {
        var request = ValidRequest() with
        {
            AllDay = allDay,
            AwardedOn = new DateTimeOffset(2026, 7, 19, 0, 0, 0, TimeSpan.FromHours(offsetHours)),
            EndingOn = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.FromHours(offsetHours)),
            ExpiryDate = DateTimeOffset.Parse(expiryDate, System.Globalization.CultureInfo.InvariantCulture),
        };

        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.IsValid);
    }

    [Fact]
    public async Task Validate_WhenNotesExceedsMaxLength_FailsValidation()
    {
        var request = ValidRequest() with { Notes = new string('x', 2001) };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _validator.ValidateAndThrowAsync(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Validate_WhenNotesIsAtMaxLength_PassesValidation()
    {
        var request = ValidRequest() with { Notes = new string('x', 2000) };

        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_WhenNotesIsNull_PassesValidation()
    {
        var request = ValidRequest() with { Notes = null };

        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }
}
