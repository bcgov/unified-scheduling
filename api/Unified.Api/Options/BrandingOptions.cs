using Microsoft.Extensions.Configuration;

namespace Unified.Api.Options;

public sealed class BrandingOptions
{
    public const string SectionName = "Branding";

    public string SelectedBrand { get; set; } = "default";

    [ConfigurationKeyName("brandingOptions")]
    public Dictionary<string, BrandSettingsOptions> Brands { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class BrandSettingsOptions
{
    public string? PrimaryColor { get; set; }
    public string? BackgroundColor { get; set; }
    public string? NavigationSelectedColor { get; set; }
    public string[]? QuickLinks { get; set; }
    public string? DashboardMessage { get; set; }
    public string? SecondaryLogoUrl { get; set; }
    public string? SecondaryLogoAlt { get; set; }
    public string? BackgroundImageUrl { get; set; }
    public string? ApplicationName { get; set; }
    public BrandHeaderOptions? Header { get; set; }
}

public sealed class BrandHeaderOptions
{
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? PoweredByText { get; set; }
    public string? ImageUrl { get; set; }
    public string? LogoUrl { get; set; }
    public string? LogoAlt { get; set; }
}
