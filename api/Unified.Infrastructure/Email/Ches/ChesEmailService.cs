using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Unified.Common.Logging;
using Unified.Core.Email;
using Unified.Infrastructure.Email.Ches.Generated;

namespace Unified.Infrastructure.Email.Ches;

internal sealed class ChesEmailService(
    ChesClient client,
    EmailMessageValidator validator,
    ChesEmailMessagePreparer preparer,
    IOptions<ChesOptions> options,
    ILogger<ChesEmailService> logger
) : IEmailService
{
    private static readonly Regex EmailAddressPattern = new(
        @"(?<![\w.+-])[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}(?![\w.-])",
        RegexOptions.CultureInvariant
    );

    private readonly ChesOptions _options = options.Value;

    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var validated = validator.Validate(message);
        var prepared = await preparer.PrepareAsync(message, validated, cancellationToken);
        var tag = Guid.NewGuid().ToString("D");
        var request = MapMessage(message, prepared, tag);

        TransactionResponse response;
        try
        {
            response = await client.PostEmailAsync(request, cancellationToken);
        }
        catch (ChesPostOutcomeUnknownException exception)
        {
            var deliveryException = new EmailDeliveryStateUnknownException(
                tag,
                message.UnifiedCorrelationId,
                prepared.Message.RecipientCount,
                prepared.Attachments.Count,
                exception
            );
            LogUnknownDeliveryState(deliveryException);
            throw deliveryException;
        }
        catch (ChesApiException exception) when (IsSuccessStatusCode(exception.StatusCode))
        {
            // A successful status proves CHES accepted the request, but the generated exception
            // can retain response content. Preserve only the safe status when reporting the
            // non-retryable unknown result to the application boundary.
            var deliveryException = new EmailDeliveryStateUnknownException(
                tag,
                message.UnifiedCorrelationId,
                prepared.Message.RecipientCount,
                prepared.Attachments.Count,
                new ChesAcceptedResponseException(exception.StatusCode)
            );
            LogUnknownDeliveryState(deliveryException);
            throw deliveryException;
        }
        catch (ChesApiException<ValidationError> exception) when (exception.StatusCode == 422)
        {
            var deliveryException = new EmailDeliveryException(
                tag,
                message.UnifiedCorrelationId,
                prepared.Message.RecipientCount,
                prepared.Attachments.Count,
                exception.StatusCode
            );
            LogValidationFailure(deliveryException, exception.Result.Errors);
            throw deliveryException;
        }
        catch (ChesApiException exception)
        {
            // Do not retain the generated exception as an inner exception: its response body can
            // contain recipient or message data and may be logged by an outer exception handler.
            var deliveryException = new EmailDeliveryException(
                tag,
                message.UnifiedCorrelationId,
                prepared.Message.RecipientCount,
                prepared.Attachments.Count,
                exception.StatusCode
            );
            LogDeliveryFailure(deliveryException);
            throw deliveryException;
        }
        catch (ChesResponseReadException exception)
        {
            var deliveryException = new EmailDeliveryException(
                tag,
                message.UnifiedCorrelationId,
                prepared.Message.RecipientCount,
                prepared.Attachments.Count,
                exception.StatusCode,
                exception
            );
            LogDeliveryFailure(deliveryException);
            throw deliveryException;
        }
        catch (ChesAuthenticationException exception)
        {
            var deliveryException = new EmailDeliveryException(
                tag,
                message.UnifiedCorrelationId,
                prepared.Message.RecipientCount,
                prepared.Attachments.Count,
                exception.StatusCode,
                exception
            );
            LogDeliveryFailure(deliveryException);
            throw deliveryException;
        }

        var messageResults = response
            .Messages.Select(item => new EmailMessageSendResult { MessageId = item.MsgId.ToString("D") })
            .ToArray();

        var result = new EmailSendResult
        {
            TransactionId = response.TxId.ToString("D"),
            Tag = tag,
            Messages = messageResults,
        };

        logger.LogInformation(
            "CHES accepted email transaction {ChesTransactionId} with tag {ChesTag}, message IDs {ChesMessageIds}, correlation {CorrelationId}, {RecipientCount} recipients, {AttachmentCount} attachments, attachment MIME types {AttachmentContentTypes}, and attachment sizes {AttachmentSizesBytes}",
            result.TransactionId,
            result.Tag,
            string.Join(',', messageResults.Select(item => item.MessageId)),
            LogSanitizer.UserText(message.UnifiedCorrelationId),
            prepared.Message.RecipientCount,
            prepared.Attachments.Count,
            string.Join(',', prepared.Attachments.Select(item => item.ContentType)),
            string.Join(',', prepared.Attachments.Select(item => item.Content.LongLength))
        );

        return result;
    }

    private MessageObject MapMessage(EmailMessage message, PreparedChesEmailMessage prepared, string tag) =>
        new()
        {
            From = new MailAddress(_options.SenderEmail, _options.SenderName).ToString(),
            To = prepared.Message.To.Count > 0 ? prepared.Message.To.ToList() : [_options.SenderEmail],
            Cc = prepared.Message.Cc.ToList(),
            Bcc = prepared.Message.Bcc.ToList(),
            Subject = message.Subject,
            Body = message.Body,
            BodyType = message.BodyType switch
            {
                EmailBodyType.Text => MessageObjectBodyType.Text,
                EmailBodyType.Html => MessageObjectBodyType.Html,
                _ => throw new EmailValidationException("The email body type is invalid."),
            },
            Encoding = MessageObjectEncoding.Utf8,
            Priority = MessageObjectPriority.Normal,
            DelayTS = 0,
            Tag = tag,
            Attachments = prepared
                .Attachments.Select(item => new AttachmentObject
                {
                    Content = Convert.ToBase64String(item.Content),
                    ContentType = item.ContentType,
                    Encoding = AttachmentObjectEncoding.Base64,
                    Filename = item.FileName,
                })
                .ToList(),
        };

    private static bool IsSuccessStatusCode(int statusCode) => statusCode is >= 200 and <= 299;

    private void LogDeliveryFailure(EmailDeliveryException exception)
    {
        logger.LogError(
            exception,
            "CHES email submission failed with tag {ChesTag}, correlation {CorrelationId}, status {ChesStatusCode}, {RecipientCount} recipients, and {AttachmentCount} attachments",
            exception.Tag,
            LogSanitizer.UserText(exception.CorrelationId),
            exception.StatusCode,
            exception.RecipientCount,
            exception.AttachmentCount
        );
    }

    private void LogValidationFailure(EmailDeliveryException exception, IEnumerable<Errors> errors)
    {
        var validationMessages = errors.Select(error => SanitizeValidationMessage(error.Message)).ToArray();
        logger.LogError(
            exception,
            "CHES email submission failed validation with tag {ChesTag}, correlation {CorrelationId}, status {ChesStatusCode}, {RecipientCount} recipients, {AttachmentCount} attachments, {ValidationErrorCount} validation errors: {ValidationErrors}",
            exception.Tag,
            LogSanitizer.UserText(exception.CorrelationId),
            exception.StatusCode,
            exception.RecipientCount,
            exception.AttachmentCount,
            validationMessages.Length,
            string.Join("; ", validationMessages)
        );
    }

    private static string SanitizeValidationMessage(string? value)
    {
        var sanitized = LogSanitizer.UserText(value, maxLength: 256) ?? "Unspecified validation error.";
        return EmailAddressPattern.Replace(sanitized, "[redacted-email]");
    }

    private void LogUnknownDeliveryState(EmailDeliveryStateUnknownException exception)
    {
        logger.LogError(
            exception,
            "CHES email submission outcome is unknown for tag {ChesTag}, correlation {CorrelationId}, {RecipientCount} recipients, and {AttachmentCount} attachments",
            exception.Tag,
            LogSanitizer.UserText(exception.CorrelationId),
            exception.RecipientCount,
            exception.AttachmentCount
        );
    }
}

internal sealed class ChesAcceptedResponseException(int statusCode)
    : Exception(
        $"CHES returned an invalid response body after accepting the submission with HTTP status {statusCode}."
    );
