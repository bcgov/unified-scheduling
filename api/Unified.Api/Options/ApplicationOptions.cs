namespace Unified.Api.Options;

public sealed class ApplicationOptions
{
    public const string SectionName = "Application";

    public string? Name { get; set; }

    public string? SupportEmail { get; set; }
}
