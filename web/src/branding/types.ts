/** A single named brand's full configuration. */
export interface BrandDefinition {
  /** Unique key used to select this brand via VITE_BRANDING_BRAND. */
  key: string;
  /** Whether the custom primary header logo is enabled. When false, the default B.C. Government logo is always shown. */
  primaryLogoEnabled: boolean;
  /** Secondary logo shown in the app bar once the user is authenticated. */
  primaryLogoUrl: string;
  /** Alt text for the header logo. */
  primaryLogoAlt: string;

  /** Whether the custom header logo is enabled. When false, the default B.C. Government logo is always shown. */
  secondaryLogoEnabled: boolean;
  /** Secondary logo shown in the app bar once the user is authenticated. */
  secondaryLogoUrl: string;
  /** Alt text for the header logo. */
  secondaryLogoAlt: string;

  /** Whether the background overlay image is enabled. When false, no overlay is rendered. */
  backgroundImageEnabled: boolean;
  /** Background overlay image shown behind page content once the user is authenticated. */
  backgroundImageUrl: string;

  /** Application name used in branded headings (e.g. dashboard welcome banner). */
  applicationName: string;
}
