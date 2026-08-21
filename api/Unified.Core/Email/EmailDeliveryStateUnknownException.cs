namespace Unified.Core.Email;

/// <summary>
/// Represents a transport failure after submission began. The provider may have accepted the
/// message, so retrying can create a duplicate.
/// </summary>
public sealed class EmailDeliveryStateUnknownException : Exception
{
    public EmailDeliveryStateUnknownException(
        string tag,
        string? correlationId,
        int recipientCount,
        int attachmentCount,
        Exception innerException
    )
        : base("The email submission outcome is unknown; retrying may create a duplicate.", innerException)
    {
        Tag = tag;
        CorrelationId = correlationId;
        RecipientCount = recipientCount;
        AttachmentCount = attachmentCount;
    }

    public string Tag { get; }

    public string? CorrelationId { get; }

    public int RecipientCount { get; }

    public int AttachmentCount { get; }
}
