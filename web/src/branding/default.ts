import type { BrandDefinition } from './types';

const resolveAssetUrl = (path: string): string => new URL(path, import.meta.url).href;

const BRAND_KEY = 'default';

/** Default B.C. Government branding — used when no BC Sheriff Service branding is configured. */
export const defaultBrand: BrandDefinition = {
  key: BRAND_KEY,

  primaryLogoEnabled: true,
  primaryLogoUrl: resolveAssetUrl('/images/bcid-logo-rev-en.svg'),
  primaryLogoAlt: 'B.C. Government Logo',

  secondaryLogoEnabled: false,
  secondaryLogoUrl: '',
  secondaryLogoAlt: '',

  backgroundImageEnabled: false,
  backgroundImageUrl: '',

  applicationName: 'Unified Operations Platform',
};
