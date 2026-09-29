namespace Unified.Api.Options;

public sealed class BrandingOptions
{
    public const string SectionName = "Branding";

    public string SelectedBrand { get; set; } = "default";
}
