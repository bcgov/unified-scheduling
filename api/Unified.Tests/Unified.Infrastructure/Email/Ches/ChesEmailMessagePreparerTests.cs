using Microsoft.Extensions.Options;
using Unified.Core.Email;
using Unified.Infrastructure.Email.Ches;

namespace Unified.Tests.Infrastructure.Email.Ches;

public sealed class ChesEmailMessagePreparerTests
{
    [Fact]
    public async Task PrepareAsync_ValidAttachments_PreservesContentAndAllowsMultipleFilesWithoutCountLimit()
    {
        var options = CreateOptions();
        var preparer = new ChesEmailMessagePreparer(Options.Create(options));
        var attachments = Enumerable
            .Range(0, 12)
            .Select(index => new EmailAttachment
            {
                FileName = index == 0 ? "REPORT.PDF" : $"report-{index}.pdf",
                ContentType = "APPLICATION/PDF",
                Content = new MemoryStream([1, 2, (byte)index]),
            })
            .ToArray();
        var message = CreateMessage(attachments);
        var validated = new EmailMessageValidator().Validate(message);

        var result = await preparer.PrepareAsync(message, validated, TestContext.Current.CancellationToken);

        Assert.Equal(12, result.Attachments.Count);
        var first = result.Attachments.First();
        Assert.Equal("REPORT.PDF", first.FileName);
        Assert.Equal("APPLICATION/PDF", first.ContentType);
        Assert.Equal([1, 2, 0], first.Content);
    }

    [Fact]
    public async Task PrepareAsync_RecipientCountExceedsConfiguredMaximum_ThrowsEmailValidationException()
    {
        var options = CreateOptions();
        options.MaxRecipientsPerMessage = 1;
        var preparer = new ChesEmailMessagePreparer(Options.Create(options));
        var message = CreateMessage([]) with { To = ["one@example.com", "two@example.com"] };
        var validated = new EmailMessageValidator().Validate(message);

        var exception = await Assert.ThrowsAsync<EmailValidationException>(() =>
            preparer.PrepareAsync(message, validated, TestContext.Current.CancellationToken)
        );

        Assert.Equal("The email exceeds the configured recipient limit.", exception.Message);
    }

    [Theory]
    [InlineData("blank-filename", "filename is required")]
    [InlineData("path-filename", "must not contain a path")]
    [InlineData("windows-path-filename", "must not contain a path")]
    [InlineData("missing-extension", "must have an extension")]
    [InlineData("null-content", "readable stream")]
    [InlineData("empty-content", "content is required")]
    [InlineData("oversized-content", "exceeds the configured size limit")]
    [InlineData("unsupported-extension", "type is not allowed")]
    [InlineData("unsupported-content-type", "type is not allowed")]
    public async Task PrepareAsync_InvalidAttachment_ThrowsEmailValidationException(
        string scenario,
        string expectedMessage
    )
    {
        var options = CreateOptions();
        var preparer = new ChesEmailMessagePreparer(Options.Create(options));
        var attachment = ApplyScenario(CreateAttachment(), scenario);
        var message = CreateMessage([attachment]);
        var validated = new EmailMessageValidator().Validate(message);

        var exception = await Assert.ThrowsAsync<EmailValidationException>(() =>
            preparer.PrepareAsync(message, validated, TestContext.Current.CancellationToken)
        );

        Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ChesOptions CreateOptions() =>
        new()
        {
            MaxRecipientsPerMessage = 500,
            MaxAttachmentSizeBytes = 4,
            AllowedAttachmentTypes =
            [
                new ChesAttachmentTypeOptions { Extension = ".pdf", ContentType = "application/pdf" },
            ],
        };

    private static EmailMessage CreateMessage(IReadOnlyCollection<EmailAttachment> attachments) =>
        new()
        {
            To = ["recipient@example.com"],
            Subject = "Subject",
            Body = "Body",
            Attachments = attachments,
        };

    private static EmailAttachment CreateAttachment() =>
        new()
        {
            FileName = "report.pdf",
            ContentType = "application/pdf",
            Content = new MemoryStream([1, 2, 3]),
        };

    private static EmailAttachment ApplyScenario(EmailAttachment attachment, string scenario)
    {
        return scenario switch
        {
            "blank-filename" => attachment with { FileName = "   " },
            "path-filename" => attachment with { FileName = "folder/report.pdf" },
            "windows-path-filename" => attachment with { FileName = "folder\\report.pdf" },
            "missing-extension" => attachment with { FileName = "report" },
            "null-content" => attachment with { Content = null! },
            "empty-content" => attachment with { Content = new MemoryStream() },
            "oversized-content" => attachment with { Content = new MemoryStream([1, 2, 3, 4, 5]) },
            "unsupported-extension" => attachment with { FileName = "report.txt" },
            "unsupported-content-type" => attachment with { ContentType = "text/plain" },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown test scenario."),
        };
    }
}
