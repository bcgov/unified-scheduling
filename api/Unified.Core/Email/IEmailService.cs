namespace Unified.Core.Email;

/// <summary>
/// Sends application email through the configured email provider.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Submits an email to the configured provider.
    /// A successful result means that the provider accepted the submission; it does not mean
    /// that the message was delivered to or read by a recipient.
    /// </summary>
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
