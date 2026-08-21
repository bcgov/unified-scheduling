using System.Net.Http.Headers;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Unified.Infrastructure.Email.Ches;

internal sealed class ChesOptionsValidator : IValidateOptions<ChesOptions>
{
    public ValidateOptionsResult Validate(string? name, ChesOptions options)
    {
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        var failures = new List<string>();

        ValidateRequired(options.ClientId, nameof(options.ClientId), failures);
        ValidateRequired(options.ClientSecret, nameof(options.ClientSecret), failures);
        ValidateHttpsUrl(options.AuthUrl, nameof(options.AuthUrl), failures);
        ValidateHttpsUrl(options.BaseUrl, nameof(options.BaseUrl), failures);
        ValidateSender(options.SenderName, options.SenderEmail, failures);

        if (options.TimeoutSeconds <= 0)
            failures.Add("Ches TimeoutSeconds must be greater than zero.");

        if (options.TokenRefreshSkewSeconds < 0)
            failures.Add("Ches TokenRefreshSkewSeconds cannot be negative.");

        if (options.MaxAttachmentSizeBytes <= 0)
            failures.Add("Ches MaxAttachmentSizeBytes must be greater than zero.");

        if (options.MaxRecipientsPerMessage <= 0)
            failures.Add("Ches MaxRecipientsPerMessage must be greater than zero.");

        ValidateAttachmentTypes(options.AllowedAttachmentTypes, failures);

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateRequired(string? value, string propertyName, ICollection<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
            failures.Add($"Ches {propertyName} is required.");
    }

    private static void ValidateHttpsUrl(string? value, string propertyName, ICollection<string> failures)
    {
        if (
            string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
        )
        {
            failures.Add($"Ches {propertyName} must be a valid absolute HTTPS URL.");
        }
    }

    private static void ValidateSender(string? senderName, string? senderEmail, ICollection<string> failures)
    {
        var senderNameIsValid = !string.IsNullOrWhiteSpace(senderName);
        if (!senderNameIsValid)
            failures.Add("Ches SenderName is required.");

        var trimmedEmail = senderEmail?.Trim();
        var senderEmailIsValid =
            !string.IsNullOrWhiteSpace(trimmedEmail)
            && MailAddress.TryCreate(trimmedEmail, out var address)
            && string.IsNullOrEmpty(address.DisplayName)
            && string.Equals(address.Address, trimmedEmail, StringComparison.OrdinalIgnoreCase);

        if (!senderEmailIsValid)
            failures.Add("Ches SenderEmail must be a valid email address without a display name.");

        if (!senderNameIsValid || !senderEmailIsValid)
            return;

        if (senderName!.Any(char.IsControl) || !TryCreateSenderAddress(trimmedEmail!, senderName))
        {
            failures.Add("Ches SenderName and SenderEmail must form a valid sender mailbox.");
        }
    }

    private static bool TryCreateSenderAddress(string senderEmail, string senderName)
    {
        try
        {
            _ = new MailAddress(senderEmail, senderName);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void ValidateAttachmentTypes(
        IReadOnlyCollection<ChesAttachmentTypeOptions>? attachmentTypes,
        ICollection<string> failures
    )
    {
        if (attachmentTypes is null || attachmentTypes.Count == 0)
        {
            failures.Add("Ches AllowedAttachmentTypes must contain at least one extension and MIME-type pair.");
            return;
        }

        var pairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;

        foreach (var attachmentType in attachmentTypes)
        {
            if (attachmentType is null)
            {
                failures.Add($"Ches AllowedAttachmentTypes[{index}] is required.");
                index++;
                continue;
            }

            var extension = attachmentType.Extension?.Trim();
            if (
                !IsValidExtension(extension)
                || !string.Equals(extension, attachmentType.Extension, StringComparison.Ordinal)
            )
                failures.Add($"Ches AllowedAttachmentTypes[{index}].Extension is malformed.");

            var contentType = attachmentType.ContentType?.Trim();
            if (
                !IsValidContentType(contentType)
                || !string.Equals(contentType, attachmentType.ContentType, StringComparison.Ordinal)
            )
                failures.Add($"Ches AllowedAttachmentTypes[{index}].ContentType is malformed.");

            if (extension is not null && contentType is not null && !pairs.Add($"{extension}|{contentType}"))
                failures.Add($"Ches AllowedAttachmentTypes[{index}] duplicates an existing pair.");

            index++;
        }
    }

    private static bool IsValidExtension(string? extension) =>
        !string.IsNullOrWhiteSpace(extension)
        && extension.Length > 1
        && extension[0] == '.'
        && extension.AsSpan(1).IndexOf('.') < 0
        && extension.AsSpan(1).ToArray().All(char.IsLetterOrDigit);

    private static bool IsValidContentType(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType)
        && MediaTypeHeaderValue.TryParse(contentType, out var parsed)
        && parsed.Parameters.Count == 0
        && parsed.MediaType?.Contains('/') == true;
}
