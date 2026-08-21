using Unified.Core.Email;

namespace Unified.Tests.Core.Email;

public sealed class EmailMessageValidatorTests
{
    private readonly EmailMessageValidator _validator = new();

    [Fact]
    public void Validate_ToCcAndBccRecipients_TrimsAndDeduplicatesCaseInsensitively()
    {
        var message = CreateMessage() with
        {
            To = [" First@example.com ", "first@example.com", "second@example.com"],
            Cc = ["FIRST@example.com", "cc@example.com"],
            Bcc = ["bcc@example.com", "CC@example.com"],
        };

        var result = _validator.Validate(message);

        Assert.Equal(["First@example.com", "second@example.com"], result.To);
        Assert.Equal(["cc@example.com"], result.Cc);
        Assert.Equal(["bcc@example.com"], result.Bcc);
        Assert.Equal(4, result.RecipientCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("Display Name <recipient@example.com>")]
    public void Validate_InvalidRecipient_ThrowsEmailValidationException(string recipient)
    {
        var message = CreateMessage() with { To = [recipient] };

        var exception = Assert.Throws<EmailValidationException>(() => _validator.Validate(message));

        Assert.Contains("Recipient To[0] is invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_NoRecipients_ThrowsEmailValidationException()
    {
        var message = CreateMessage() with { To = [], Cc = [], Bcc = [] };

        var exception = Assert.Throws<EmailValidationException>(() => _validator.Validate(message));

        Assert.Equal("At least one email recipient is required.", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankSubject_ThrowsEmailValidationException(string subject)
    {
        var message = CreateMessage() with { Subject = subject };

        var exception = Assert.Throws<EmailValidationException>(() => _validator.Validate(message));

        Assert.Equal("The email subject is required.", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankBody_ThrowsEmailValidationException(string body)
    {
        var message = CreateMessage() with { Body = body };

        var exception = Assert.Throws<EmailValidationException>(() => _validator.Validate(message));

        Assert.Equal("The email body is required.", exception.Message);
    }

    [Fact]
    public void Validate_InvalidBodyType_ThrowsEmailValidationException()
    {
        var message = CreateMessage() with { BodyType = (EmailBodyType)999 };

        var exception = Assert.Throws<EmailValidationException>(() => _validator.Validate(message));

        Assert.Equal("The email body type is invalid.", exception.Message);
    }

    private static EmailMessage CreateMessage() =>
        new()
        {
            To = ["recipient@example.com"],
            Subject = "Subject",
            Body = "Body",
        };
}
