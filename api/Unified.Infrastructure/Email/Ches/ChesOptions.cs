namespace Unified.Infrastructure.Email.Ches;

public sealed class ChesOptions
{
    public const string SectionName = "Ches";

    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "https://ches.api.gov.bc.ca/api/v1/";

    public string AuthUrl { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string SenderName { get; set; } = "Unified Scheduling";

    public string SenderEmail { get; set; } = string.Empty;

    public int TokenRefreshSkewSeconds { get; set; } = 60;

    public int TimeoutSeconds { get; set; } = 30;

    public List<ChesAttachmentTypeOptions> AllowedAttachmentTypes { get; set; } = [];

    public long MaxAttachmentSizeBytes { get; set; } = 20L * 1024 * 1024;

    public int MaxRecipientsPerMessage { get; set; } = 500;
}

public sealed class ChesAttachmentTypeOptions
{
    public string Extension { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;
}
