namespace Unified.Core.Email;

public sealed record EmailAttachment
{
    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    /// <summary>
    /// Readable attachment content. The caller owns the stream and must keep it open until
    /// <see cref="IEmailService.SendAsync"/> completes. The email service does not dispose it.
    /// </summary>
    public required Stream Content { get; init; }
}
