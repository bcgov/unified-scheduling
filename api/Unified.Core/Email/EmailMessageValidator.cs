using System.Net.Mail;

namespace Unified.Core.Email;

/// <summary>
/// Validates provider-neutral email message requirements and normalizes recipient addresses.
/// </summary>
public sealed class EmailMessageValidator
{
    public ValidatedEmailMessage Validate(EmailMessage message)
    {
        if (message is null)
            throw new EmailValidationException("The email message is required.");

        if (string.IsNullOrWhiteSpace(message.Subject))
            throw new EmailValidationException("The email subject is required.");

        if (string.IsNullOrWhiteSpace(message.Body))
            throw new EmailValidationException("The email body is required.");

        if (!Enum.IsDefined(message.BodyType))
            throw new EmailValidationException("The email body type is invalid.");

        var seenRecipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var to = ValidateRecipients(message.To ?? [], nameof(message.To), seenRecipients);
        var cc = ValidateRecipients(message.Cc ?? [], nameof(message.Cc), seenRecipients);
        var bcc = ValidateRecipients(message.Bcc ?? [], nameof(message.Bcc), seenRecipients);
        var recipientCount = to.Count + cc.Count + bcc.Count;

        if (recipientCount == 0)
            throw new EmailValidationException("At least one email recipient is required.");

        return new ValidatedEmailMessage(to, cc, bcc, recipientCount);
    }

    private static IReadOnlyCollection<string> ValidateRecipients(
        IReadOnlyCollection<string> recipients,
        string collectionName,
        ISet<string> seenRecipients
    )
    {
        var validated = new List<string>(recipients.Count);
        var index = 0;

        foreach (var recipient in recipients)
        {
            var trimmed = recipient?.Trim();
            if (
                string.IsNullOrWhiteSpace(trimmed)
                || trimmed.Length > 320
                || !MailAddress.TryCreate(trimmed, out var address)
                || !string.IsNullOrEmpty(address.DisplayName)
                || !string.Equals(address.Address, trimmed, StringComparison.OrdinalIgnoreCase)
            )
            {
                throw new EmailValidationException($"Recipient {collectionName}[{index}] is invalid.");
            }

            if (seenRecipients.Add(address.Address))
                validated.Add(address.Address);

            index++;
        }

        return validated;
    }
}

public sealed record ValidatedEmailMessage(
    IReadOnlyCollection<string> To,
    IReadOnlyCollection<string> Cc,
    IReadOnlyCollection<string> Bcc,
    int RecipientCount
);
