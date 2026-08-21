namespace Unified.Core.Email;

public sealed record EmailMessage
{
    public IReadOnlyCollection<string> To { get; init; } = [];

    public IReadOnlyCollection<string> Cc { get; init; } = [];

    public IReadOnlyCollection<string> Bcc { get; init; } = [];

    public required string Subject { get; init; }

    public required string Body { get; init; }

    public EmailBodyType BodyType { get; init; } = EmailBodyType.Text;

    public IReadOnlyCollection<EmailAttachment> Attachments { get; init; } = [];

    /// <summary>
    /// Optional internal application or business correlation value. This is not used as the provider message tag and is not sent to CHES.
    /// </summary>
    public string? UnifiedCorrelationId { get; init; }
}
