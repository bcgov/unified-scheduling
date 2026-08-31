using Unified.Api.Models;
using Unified.Api.Validators;

namespace Unified.Tests.Api.Validators;

public sealed class TestEmailRequestValidatorTests
{
    private readonly TestEmailRequestValidator _validator = new();

    [Fact]
    public async Task Validate_ValidRecipient_Succeeds()
    {
        var result = await _validator.ValidateAsync(
            new TestEmailRequest { Recipient = "recipient@example.com" },
            TestContext.Current.CancellationToken
        );

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    public async Task Validate_InvalidRecipient_Fails(string recipient)
    {
        var result = await _validator.ValidateAsync(
            new TestEmailRequest { Recipient = recipient },
            TestContext.Current.CancellationToken
        );

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(TestEmailRequest.Recipient));
    }
}
