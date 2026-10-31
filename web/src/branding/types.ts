export type DashboardTileId = 'myTeam' | 'stats' | 'schedule' | 'admin' | 'myProfile' | 'reporting' | 'training';
export interface DashboardBanner {
  title: string;
  subtitle: string;
  tagline: string;
  /** Optional decorative image URL. An empty value displays the gradient placeholder. */
  imageUrl: string;
  /** Optional foreground logo, displayed without cropping. */
  logoUrl?: string;
  logoAlt?: string;
}
/** A single named brand's full configuration. */
export interface BrandDefinition {
  /** Unique key selected by the /api/config selectedBrand value. */
  key: string;
  /** Primary UI colour, expressed as a CSS hex colour. */
  primaryColor: string;
  /** Main application background colour, expressed as a CSS hex colour. */
  backgroundColor: string;
  /** Highlight colour for active navigation, tabs and selected list options. */
  activeColor: string;
  /** Ordered list of dashboard tile IDs allowed for this brand. */
  dashboardTiles?: DashboardTileId[];
  dashboardBanner?: DashboardBanner;
  dashboardMessage: string;

  /** Secondary logo shown in the app bar once the user is authenticated. */
  secondaryLogoUrl: string;
  /** Alt text for the header logo. */
  secondaryLogoAlt: string;

  /** Optional background overlay image shown behind authenticated page content when a URL is provided. */
  backgroundImageUrl: string;

  /** Application name used in branded headings (e.g. dashboard welcome banner). */
  applicationName: string;
}
