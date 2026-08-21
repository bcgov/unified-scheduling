namespace Unified.Core.Email;

/// <summary>
/// Identifiers returned after the email provider accepts a submission. This does not represent
/// delivery to a recipient mailbox.
/// </summary>
public sealed record EmailSendResult
{
    public required string TransactionId { get; init; }

    public required string Tag { get; init; }

    public IReadOnlyCollection<EmailMessageSendResult> Messages { get; init; } = [];
}

public sealed record EmailMessageSendResult
{
    public required string MessageId { get; init; }
}
