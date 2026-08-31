using Unified.Infrastructure.Email.Ches;

namespace Unified.Tests.Infrastructure.Email.Ches;

public sealed class ChesOptionsValidatorTests
{
    private readonly ChesOptionsValidator _validator = new();

    [Fact]
    public void Validate_ValidConfiguration_Succeeds()
    {
        var result = _validator.Validate(null, CreateValidOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_DisabledConfiguration_DoesNotRequireProviderSettings()
    {
        var result = _validator.Validate(null, new ChesOptions { Enabled = false });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("missing-client-id", "ClientId")]
    [InlineData("blank-client-id", "ClientId")]
    [InlineData("missing-client-secret", "ClientSecret")]
    [InlineData("blank-client-secret", "ClientSecret")]
    [InlineData("invalid-auth-url", "AuthUrl")]
    [InlineData("invalid-base-url", "BaseUrl")]
    [InlineData("blank-sender-name", "SenderName")]
    [InlineData("invalid-sender-email", "SenderEmail")]
    [InlineData("invalid-sender-mailbox", "sender mailbox")]
    [InlineData("non-positive-timeout", "TimeoutSeconds")]
    [InlineData("negative-refresh-skew", "TokenRefreshSkewSeconds")]
    [InlineData("non-positive-attachment-size", "MaxAttachmentSizeBytes")]
    [InlineData("non-positive-recipient-limit", "MaxRecipientsPerMessage")]
    [InlineData("missing-attachment-types", "AllowedAttachmentTypes")]
    [InlineData("blank-extension", ".Extension")]
    [InlineData("malformed-extension", ".Extension")]
    [InlineData("blank-content-type", ".ContentType")]
    [InlineData("malformed-content-type", ".ContentType")]
    [InlineData("duplicate-attachment-pair", "duplicates")]
    public void Validate_InvalidConfiguration_ReturnsExpectedFailure(string scenario, string expectedFailure)
    {
        var options = CreateValidOptions();
        ApplyScenario(options, scenario);

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains(expectedFailure, StringComparison.OrdinalIgnoreCase)
        );
    }

    private static ChesOptions CreateValidOptions() =>
        new()
        {
            Enabled = true,
            BaseUrl = "https://ches.example.com/api/v1/",
            AuthUrl = "https://auth.example.com/token",
            ClientId = "client-id",
            ClientSecret = "client-secret",
            SenderName = "Unified Scheduling",
            SenderEmail = "sender@example.com",
            TokenRefreshSkewSeconds = 60,
            TimeoutSeconds = 30,
            AllowedAttachmentTypes =
            [
                new ChesAttachmentTypeOptions { Extension = ".pdf", ContentType = "application/pdf" },
            ],
            MaxAttachmentSizeBytes = 1024,
            MaxRecipientsPerMessage = 500,
        };

    private static void ApplyScenario(ChesOptions options, string scenario)
    {
        switch (scenario)
        {
            case "missing-client-id":
                options.ClientId = null!;
                break;
            case "blank-client-id":
                options.ClientId = "   ";
                break;
            case "missing-client-secret":
                options.ClientSecret = null!;
                break;
            case "blank-client-secret":
                options.ClientSecret = "   ";
                break;
            case "invalid-auth-url":
                options.AuthUrl = "http://auth.example.com/token";
                break;
            case "invalid-base-url":
                options.BaseUrl = "not-a-url";
                break;
            case "blank-sender-name":
                options.SenderName = "   ";
                break;
            case "invalid-sender-email":
                options.SenderEmail = "not-an-email";
                break;
            case "invalid-sender-mailbox":
                options.SenderName = "Unified\nScheduling";
                break;
            case "non-positive-timeout":
                options.TimeoutSeconds = 0;
                break;
            case "negative-refresh-skew":
                options.TokenRefreshSkewSeconds = -1;
                break;
            case "non-positive-attachment-size":
                options.MaxAttachmentSizeBytes = 0;
                break;
            case "non-positive-recipient-limit":
                options.MaxRecipientsPerMessage = 0;
                break;
            case "missing-attachment-types":
                options.AllowedAttachmentTypes = [];
                break;
            case "blank-extension":
                options.AllowedAttachmentTypes[0].Extension = "   ";
                break;
            case "malformed-extension":
                options.AllowedAttachmentTypes[0].Extension = "pdf";
                break;
            case "blank-content-type":
                options.AllowedAttachmentTypes[0].ContentType = "   ";
                break;
            case "malformed-content-type":
                options.AllowedAttachmentTypes[0].ContentType = "not-a-mime-type";
                break;
            case "duplicate-attachment-pair":
                options.AllowedAttachmentTypes.Add(
                    new ChesAttachmentTypeOptions { Extension = ".PDF", ContentType = "APPLICATION/PDF" }
                );
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown test scenario.");
        }
    }
}
