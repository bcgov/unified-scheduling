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
        };

    [Fact]
    public async Task Validate_WhenRequestIsValid_PassesValidation()
    {
        var result = await _validator.ValidateAsync(ValidRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
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
    public async Task Validate_WhenExpiryDateIsAfterAwardedOn_PassesValidation()
    {
        var awardedOn = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var request = ValidRequest() with { AwardedOn = awardedOn, ExpiryDate = awardedOn.AddDays(365) };

        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_WhenExpiryDateIsBeforeAwardedOn_FailsValidation()
    {
        var awardedOn = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var request = ValidRequest() with { AwardedOn = awardedOn, ExpiryDate = awardedOn.AddDays(-1) };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _validator.ValidateAndThrowAsync(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Validate_WhenExpiryDateEqualsAwardedOn_FailsValidation()
    {
        var awardedOn = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var request = ValidRequest() with { AwardedOn = awardedOn, ExpiryDate = awardedOn };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _validator.ValidateAndThrowAsync(request, TestContext.Current.CancellationToken)
        );
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
