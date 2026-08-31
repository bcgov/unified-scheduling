namespace Unified.Core.Email;

/// <summary>
/// Represents a definite failure to submit an email.
/// </summary>
public sealed class EmailDeliveryException : Exception
{
    public EmailDeliveryException(
        string tag,
        string? correlationId,
        int recipientCount,
        int attachmentCount,
        int? statusCode = null,
        Exception? innerException = null
    )
        : base(CreateMessage(statusCode), innerException)
    {
        Tag = tag;
        CorrelationId = correlationId;
        RecipientCount = recipientCount;
        AttachmentCount = attachmentCount;
        StatusCode = statusCode;
    }

    /// <summary>
    /// Tag associated with the message, which can be used for querying the message in the CHES service later.
    /// </summary>
    public string Tag { get; }

    /// <summary>
    /// Optional application or business correlation value. This is not used as the provider message tag, and is not sent to CHES.
    /// </summary>
    public string? CorrelationId { get; }

    public int RecipientCount { get; }

    public int AttachmentCount { get; }

    public int? StatusCode { get; }

    private static string CreateMessage(int? statusCode) =>
        statusCode is null
            ? "The email submission failed before the provider accepted it."
            : $"The email provider rejected the submission with HTTP status {statusCode}.";
}
