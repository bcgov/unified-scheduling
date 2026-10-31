import type { BrandDefinition } from './types';
import { resolveBrandingConfig } from './brandingConfig';
import { useConfigStore } from '@/stores/config';

export type { BrandDefinition } from './types';
export { defaultBrand, resolveBrandingConfig } from './brandingConfig';

/** Resolve the active brand from API configuration, with default fallback. */

export const getBrandingConfig = (): BrandDefinition => {
  const configStore = useConfigStore();
  return resolveBrandingConfig(configStore.selectedBrand, configStore.config?.brandingOptions);
};
