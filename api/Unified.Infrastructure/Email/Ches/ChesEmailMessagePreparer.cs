using System.Buffers;
using Microsoft.Extensions.Options;
using Unified.Core.Email;

namespace Unified.Infrastructure.Email.Ches;

/// <summary>
/// Applies CHES-specific limits and materializes attachment streams for the generated JSON client.
/// </summary>
internal sealed class ChesEmailMessagePreparer(IOptions<ChesOptions> options)
{
    private readonly ChesOptions _options = options.Value;

    public async Task<PreparedChesEmailMessage> PrepareAsync(
        EmailMessage message,
        ValidatedEmailMessage validatedMessage,
        CancellationToken cancellationToken
    )
    {
        if (validatedMessage.RecipientCount > _options.MaxRecipientsPerMessage)
            throw new EmailValidationException("The email exceeds the configured recipient limit.");

        var attachments = await PrepareAttachmentsAsync(message.Attachments ?? [], cancellationToken);
        return new PreparedChesEmailMessage(validatedMessage, attachments);
    }

    private async Task<IReadOnlyCollection<PreparedChesEmailAttachment>> PrepareAttachmentsAsync(
        IReadOnlyCollection<EmailAttachment> attachments,
        CancellationToken cancellationToken
    )
    {
        var prepared = new List<PreparedChesEmailAttachment>(attachments.Count);
        var index = 0;

        foreach (var attachment in attachments)
        {
            if (attachment is null)
                throw new EmailValidationException($"Attachment[{index}] is required.");

            var fileName = attachment.FileName?.Trim();
            if (string.IsNullOrWhiteSpace(fileName))
                throw new EmailValidationException($"Attachment[{index}] filename is required.");

            if (fileName.Contains('/') || fileName.Contains('\\'))
            {
                throw new EmailValidationException($"Attachment[{index}] filename must not contain a path.");
            }

            if (attachment.Content is null || !attachment.Content.CanRead)
                throw new EmailValidationException($"Attachment[{index}] content must be a readable stream.");

            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(extension))
                throw new EmailValidationException($"Attachment[{index}] filename must have an extension.");

            var contentType = attachment.ContentType?.Trim();
            if (string.IsNullOrWhiteSpace(contentType))
                throw new EmailValidationException($"Attachment[{index}] content type is required.");

            var isAllowedPair = _options.AllowedAttachmentTypes.Any(type =>
                string.Equals(type.Extension, extension, StringComparison.OrdinalIgnoreCase)
                && string.Equals(type.ContentType, contentType, StringComparison.OrdinalIgnoreCase)
            );

            if (!isAllowedPair)
                throw new EmailValidationException($"Attachment[{index}] type is not allowed.");

            var content = await ReadAttachmentContentAsync(attachment.Content, index, cancellationToken);
            prepared.Add(new PreparedChesEmailAttachment(fileName, contentType, content));
            index++;
        }

        return prepared;
    }

    private async Task<byte[]> ReadAttachmentContentAsync(
        Stream content,
        int attachmentIndex,
        CancellationToken cancellationToken
    )
    {
        var readBuffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            using var bufferedContent = new MemoryStream();
            long totalBytes = 0;

            while (true)
            {
                var bytesRead = await content.ReadAsync(readBuffer.AsMemory(), cancellationToken);
                if (bytesRead == 0)
                    break;

                totalBytes += bytesRead;
                if (totalBytes > _options.MaxAttachmentSizeBytes)
                    throw new EmailValidationException(
                        $"Attachment[{attachmentIndex}] exceeds the configured size limit."
                    );

                await bufferedContent.WriteAsync(readBuffer.AsMemory(0, bytesRead), cancellationToken);
            }

            if (totalBytes == 0)
                throw new EmailValidationException($"Attachment[{attachmentIndex}] content is required.");

            return bufferedContent.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(readBuffer, clearArray: true);
        }
    }
}

internal sealed record PreparedChesEmailMessage(
    ValidatedEmailMessage Message,
    IReadOnlyCollection<PreparedChesEmailAttachment> Attachments
);

internal sealed record PreparedChesEmailAttachment(string FileName, string ContentType, byte[] Content);
