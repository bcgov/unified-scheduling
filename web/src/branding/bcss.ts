import type { BrandDefinition } from './types';

const resolveAssetUrl = (path: string): string => new URL(path, import.meta.url).href;

const BRAND_KEY = 'bcss';

/** B.C. Sheriff Service branding. */
export const bcssBrand: BrandDefinition = {
  key: BRAND_KEY,

  primaryLogoEnabled: true,
  primaryLogoUrl: resolveAssetUrl('/images/bcid-logo-rev-en.svg'),
  primaryLogoAlt: 'B.C. Government Logo',

  secondaryLogoEnabled: true,
  secondaryLogoUrl: resolveAssetUrl('/images/branding/bcss/BCSS_Crest-wText_Horizontal_cmyk_pos_white.png'),
  secondaryLogoAlt: 'B.C. Sheriff Service Crest',

  backgroundImageEnabled: true,
  backgroundImageUrl: resolveAssetUrl('/images/branding/bcss/BCSS_Crest_cmyk_pos.png'),

  applicationName: 'Unified Operations Platform',
};
