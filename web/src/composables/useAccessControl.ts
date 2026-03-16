import type { Pinia } from 'pinia';
import { useConfigStore } from '@/stores/config';
import type { FeatureFlagsOptions } from '@/api-access/generated/models';

export const useAccessControl = (pinia?: Pinia) => {
  const configStore = useConfigStore(pinia);

  const isFeatureFlagEnabled = (moduleKey: keyof FeatureFlagsOptions): boolean => {
    if (configStore.config?.featureFlags?.[`${moduleKey}`]) {
      return true;
    }

    return false;
  };

  return {
    configStore,
    isFeatureFlagEnabled,
  };
};
