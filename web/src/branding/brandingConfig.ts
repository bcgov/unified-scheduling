import type { BrandDefinition, DashboardTileId } from './types';
import type { BrandSettingsOptions } from '@/api-access/generated/models';

import { resolveBrandAssetUrl } from './brandAssetUrl';

export const fixedPrimaryLogo = {
  url: '/images/bcid-logo-rev-en.svg',
  alt: 'B.C. Government Logo',
} as const;

/** Bundled fallback values when runtime branding configuration omits a setting. */
export const defaultBrand: BrandDefinition = {
  key: 'default',
  primaryColor: '#013366',
  backgroundColor: '#FAF9F8',
  activeColor: '#e3a82b',
  dashboardMessage: 'Select your work location, then choose a module below to get started.',

  dashboardTiles: ['myProfile', 'myTeam', 'stats', 'schedule', 'admin', 'reporting', 'training'],
  dashboardBanner: {
    title: 'Unified Operations Platform',
    subtitle: 'People, scheduling and operations',
    tagline: '',
    imageUrl: '',
  },

  secondaryLogoUrl: '',
  secondaryLogoAlt: '',

  backgroundImageUrl: '',

  applicationName: 'Unified Operations Platform',
};

const tileIds = new Map<string, DashboardTileId>([
  ['myteam', 'myTeam'],
  ['stats', 'stats'],
  ['schedule', 'schedule'],
  ['admin', 'admin'],
  ['myprofile', 'myProfile'],
  ['reporting', 'reporting'],
  ['reports', 'reporting'],
  ['training', 'training'],
]);

/** Resolve explicit server settings, then configured defaults, then bundled defaults. */
export function resolveBrandingConfig(
  brandKey?: string,
  definitions: Record<string, BrandSettingsOptions> = {},
): BrandDefinition {
  const key = brandKey?.trim().toLowerCase() || 'default';
  const findSettings = (name: string) =>
    Object.entries(definitions).find(([entry]) => entry.toLowerCase() === name)?.[1];
  const defaults = findSettings('default') ?? {};
  const settings = findSettings(key) ?? {};
  const header = defaultBrand.dashboardBanner;
  const selectedTileIds = settings.quickLinks ?? defaults.quickLinks;
  const color = (value?: string | null) => value?.trim() || undefined;

  return {
    ...defaultBrand,
    key: findSettings(key) ? key : defaultBrand.key,
    primaryColor: color(settings.primaryColor) ?? color(defaults.primaryColor) ?? defaultBrand.primaryColor,
    backgroundColor: color(settings.backgroundColor) ?? color(defaults.backgroundColor) ?? defaultBrand.backgroundColor,
    activeColor:
      color(settings.navigationSelectedColor) ?? color(defaults.navigationSelectedColor) ?? defaultBrand.activeColor,
    dashboardTiles:
      selectedTileIds == null
        ? defaultBrand.dashboardTiles
        : [
            ...new Set(
              selectedTileIds.flatMap((id) => {
                const tileId = tileIds.get(id.trim().toLowerCase());
                return tileId ? [tileId] : [];
              }),
            ),
          ],
    dashboardMessage: settings.dashboardMessage ?? defaults.dashboardMessage ?? defaultBrand.dashboardMessage,
    dashboardBanner: {
      ...header,
      title: settings.header?.title ?? defaults.header?.title ?? header?.title ?? defaultBrand.applicationName,
      subtitle: settings.header?.subtitle ?? defaults.header?.subtitle ?? header?.subtitle ?? '',
      tagline: settings.header?.poweredByText ?? defaults.header?.poweredByText ?? header?.tagline ?? '',
      imageUrl: resolveBrandAssetUrl(settings.header?.imageUrl ?? defaults.header?.imageUrl ?? header?.imageUrl),
      logoUrl: resolveBrandAssetUrl(settings.header?.logoUrl ?? defaults.header?.logoUrl ?? header?.logoUrl),
      logoAlt: settings.header?.logoAlt ?? defaults.header?.logoAlt ?? header?.logoAlt,
    },
    secondaryLogoUrl: resolveBrandAssetUrl(
      settings.secondaryLogoUrl ?? defaults.secondaryLogoUrl ?? defaultBrand.secondaryLogoUrl,
    ),
    secondaryLogoAlt: settings.secondaryLogoAlt ?? defaults.secondaryLogoAlt ?? defaultBrand.secondaryLogoAlt,
    backgroundImageUrl: resolveBrandAssetUrl(
      settings.backgroundImageUrl ?? defaults.backgroundImageUrl ?? defaultBrand.backgroundImageUrl,
    ),
    applicationName: settings.applicationName ?? defaults.applicationName ?? defaultBrand.applicationName,
  };
}
