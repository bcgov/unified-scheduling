import type { BrandDefinition } from './types';
import { defaultBrand } from './default';
import { bcssBrand } from './bcss';
import { useConfigStore } from '@/stores/config';

export type { BrandDefinition } from './types';

/** All brands available for selection via the /api/config `selectedBrand` value. */
const brands: Record<string, BrandDefinition> = {
  [defaultBrand.key]: defaultBrand,
  [bcssBrand.key]: bcssBrand,
};

/**
 * Resolves the active brand configuration based on the `selectedBrand` value
 * returned by /api/config (see Unified.Api BrandingOptions). Falls back to the
 * default B.C. Government branding when unset or unrecognized.
 */
export const getBrandingConfig = (): BrandDefinition => {
  const configStore = useConfigStore();
  const selectedBrand = brands[configStore.selectedBrand ?? ''] ?? defaultBrand;

  return {
    ...selectedBrand,
    primaryLogoEnabled: true,
    secondaryLogoEnabled: selectedBrand.secondaryLogoEnabled,
    backgroundImageEnabled: selectedBrand.backgroundImageEnabled,
  };
};
